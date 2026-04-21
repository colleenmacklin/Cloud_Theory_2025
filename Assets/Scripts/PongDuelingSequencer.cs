using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace Synthic
{
    /// <summary>
    /// Dueling Banjos Pong sequencer.
    ///
    /// Flow:
    ///   1. Left paddle plays the current shared phrase (all measures accumulated so far).
    ///   2. Right paddle echoes the exact same phrase, then appends a new 16-step measure.
    ///   3. Left paddle plays the now-longer phrase, appends a measure.
    ///   4. Repeat — each score event (ball through opponent goal) triggers the active
    ///      paddle to append a new measure after its current phrase finishes.
    ///
    /// A "measure" is always 16 sixteenth-notes at the current BPM.
    /// Ties, rests, and notes-with-balls are all supported per step.
    /// </summary>
    public class PongDuelingSequencer : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Paddles")]
        [SerializeField] private PongPaddle leftPaddle;
        [SerializeField] private PongPaddle rightPaddle;

        [Header("Generators (one per paddle)")]
        [SerializeField] private PolyphonicGenerator leftGenerator;
        [SerializeField] private PolyphonicGenerator rightGenerator;

        [Header("Ball Prefab")]
        [SerializeField] private GameObject ballPrefab;

        [Header("Launch Settings")]
        [SerializeField, Range(2f, 30f)] private float launchSpeed  = 12f;
        [SerializeField, Range(0f, 30f)] private float launchSpread = 8f;
        [SerializeField, Range(0.1f, 2f)] private float ballRadius  = 0.2f;

        [Header("Score UI")]
        [SerializeField] private TextMeshProUGUI leftScoreLabel;
        [SerializeField] private TextMeshProUGUI rightScoreLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;

        [Header("Starting Measure (16 steps — edit in Inspector)")]
        [SerializeField] private List<PongSequenceStep> startingMeasure = new();

        // ── Runtime state ─────────────────────────────────────────────────────

        // The shared phrase — grows by one measure each round
        private readonly List<PongSequenceStep> _phrase = new();

        // Which paddle is currently playing
        private enum Turn { Left, Right }
        private Turn _currentTurn = Turn.Left;

        // Sequencer clock
        private float _sixteenthDuration; // seconds per 16th note
        private float _stepTimer;
        private int   _currentStep;       // index into _phrase

        // Scoring
        private int _leftScore;
        private int _rightScore;

        // Pending new measure to append after phrase finishes
        private bool _appendPending;

        // Track last live ball per step (for tie continuation)
        private PongBall _lastBall;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Seed the phrase with the starting measure
            if (startingMeasure.Count == 0)
                GenerateDefaultMeasure();

            _phrase.AddRange(startingMeasure);
            UpdateSixteenthDuration();
            UpdateUI();
        }

        private void Start()
        {
            SetStatus("Left paddle playing");
        }

        private void Update()
        {
            UpdateSixteenthDuration();

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= _sixteenthDuration)
            {
                _stepTimer -= _sixteenthDuration;
                TickStep();
            }
        }

        // ── Clock ─────────────────────────────────────────────────────────────

        private void UpdateSixteenthDuration()
        {
            float bpm = RhythmicMasterClock.Instance != null
                ? RhythmicMasterClock.Instance.BPM
                : 120f;
            // 16th note = quarter note / 4
            _sixteenthDuration = (60f / bpm) / 4f;
        }

        // ── Step execution ────────────────────────────────────────────────────

        private void TickStep()
        {
            if (_phrase.Count == 0) return;

            var step = _phrase[_currentStep];

            if (!step.isRest && !step.isTie)
            {
                // spawn a new ball
                SpawnBall(step);
            }
            // ties: do nothing — the previous ball keeps bouncing

            _currentStep++;

            if (_currentStep >= _phrase.Count)
            {
                // phrase finished
                _currentStep = 0;

                if (_appendPending)
                {
                    // append the new measure and hand over to the other paddle
                    AppendNewMeasure();
                    _appendPending = false;
                }

                // switch turns
                _currentTurn = _currentTurn == Turn.Left ? Turn.Right : Turn.Left;
                SetStatus(_currentTurn == Turn.Left
                    ? "Left paddle playing"
                    : "Right paddle echoing + adding");
            }
        }

        // ── Ball spawning ─────────────────────────────────────────────────────

        private void SpawnBall(PongSequenceStep step)
        {
            bool isLeft = _currentTurn == Turn.Left;
            PongPaddle paddle = isLeft ? leftPaddle : rightPaddle;
            PolyphonicGenerator gen = isLeft ? leftGenerator : rightGenerator;

            if (paddle == null || gen == null || ballPrefab == null)
            {
                Debug.LogWarning($"SpawnBall aborted — paddle={paddle}, gen={gen}, ballPrefab={ballPrefab}");
                return;
            }

            float xSign  = isLeft ? 1f : -1f;
            Vector3 pos  = paddle.transform.position
                         + new Vector3(xSign * (ballRadius + 0.05f), 0f, 0f);

            GameObject ballObj = Instantiate(ballPrefab, pos, Quaternion.identity);
            ballObj.transform.localScale = Vector3.one * ballRadius * 2f;

            // launch angle: base direction + step angle offset + small random spread
            float spreadAngle = (step.angleOffset
                + Random.Range(-launchSpread, launchSpread)) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(
                xSign * Mathf.Cos(spreadAngle),
                Mathf.Sin(spreadAngle),
                0f).normalized;

            var ball = ballObj.GetComponent<PongBall>();
            if (ball == null) ball = ballObj.AddComponent<PongBall>();

            // pass which side launched this ball so scoring works
            ball.Initialize(gen, step.Frequency, dir * launchSpeed, isLeft);
            ball.OnScored += OnBallScored;

            _lastBall = ball;
        }

        // ── Scoring ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called by PongBall when it exits through a goal.
        /// launchedByLeft = true if the ball was launched by the left paddle.
        /// </summary>
        private void OnBallScored(bool launchedByLeft)
        {
            if (launchedByLeft)
                _leftScore++;
            else
                _rightScore++;

            // after the current phrase finishes, the active paddle will append a measure
            _appendPending = true;

            UpdateUI();
        }

        // ── Phrase management ─────────────────────────────────────────────────

        private void AppendNewMeasure()
        {
            // generate a new 16-step measure and add it to the shared phrase
            var newMeasure = GenerateNewMeasure();
            _phrase.AddRange(newMeasure);
        }

        /// <summary>
        /// Generates a new 16-step measure procedurally.
        /// Override or extend this for more musical variety.
        /// </summary>
        private List<PongSequenceStep> GenerateNewMeasure()
        {
            var measure = new List<PongSequenceStep>(16);

            // musical note pool — pentatonic to keep it pleasant
            Note.Name[] pool = {
                Note.Name.C, Note.Name.D, Note.Name.E,
                Note.Name.G, Note.Name.A
            };
            int octave = _currentTurn == Turn.Left ? 3 : 4;

            for (int i = 0; i < 16; i++)
            {
                float r = Random.value;
                if (r < 0.25f)
                {
                    measure.Add(PongSequenceStep.Rest());
                }
                else if (r < 0.45f && i > 0 && !measure[i - 1].isRest && !measure[i - 1].isTie)
                {
                    measure.Add(PongSequenceStep.Tie());
                }
                else
                {
                    var note = pool[Random.Range(0, pool.Length)];
                    measure.Add(PongSequenceStep.NoteStep(note, octave));
                }
            }

            return measure;
        }

        // ── Default starting measure ──────────────────────────────────────────

        private void GenerateDefaultMeasure()
        {
            // Simple pentatonic seed — C D E G A across 16 steps
            Note.Name[] seed = {
                Note.Name.C, Note.Name.E, Note.Name.G, Note.Name.E,
                Note.Name.C, Note.Name.E, Note.Name.G, Note.Name.A,
                Note.Name.G, Note.Name.E, Note.Name.C, Note.Name.E,
                Note.Name.G, Note.Name.E, Note.Name.C, Note.Name.C,
            };
            for (int i = 0; i < 16; i++)
                startingMeasure.Add(PongSequenceStep.NoteStep(seed[i], 3));
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private void UpdateUI()
        {
            if (leftScoreLabel  != null) leftScoreLabel.text  = _leftScore.ToString();
            if (rightScoreLabel != null) rightScoreLabel.text = _rightScore.ToString();
        }

        private void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
            Debug.Log($"[PongSequencer] {msg}");
        }

        // ── Public API for external access ────────────────────────────────────

        public int LeftScore  => _leftScore;
        public int RightScore => _rightScore;
        public int PhraseLength => _phrase.Count;
    }
}
