using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    public class Sequencer : MonoBehaviour
    {
        public enum PlayMode { Loop, Chain }

        [Header("Transport")]
        [SerializeField] private bool playOnAwake = false;
        [SerializeField, Range(20f, 300f)] private float bpm = 120f;
        [SerializeField] private PlayMode playMode = PlayMode.Loop;

        [Header("Target")]
        [SerializeField] private NoteInput noteInput;
        [SerializeField, Range(0f, 1f)] private float accentVelocity = 1f;

        [Header("Patterns")]
        [SerializeField] private List<Sequence> patterns = new();
        [SerializeField] private int currentPatternIndex = 0;

        // transport state
        private bool _playing = false;
        private int  _currentStep = 0;
        private int  _nextPatternIndex = -1; // -1 means no pattern queued
        private float _stepTimer = 0f;
        private float _currentStepDuration = 0f;
        private float _gateTimer = 0f;
        private bool  _noteActive = false;
        private SequenceStep _lastStep = null;

        // public properties for UI/code access
        public bool    Playing            => _playing;
        public int     CurrentStep        => _currentStep;
        public int     CurrentPattern     => currentPatternIndex;
        public float   BPM
        {
            get => bpm;
            set => bpm = Mathf.Clamp(value, 20f, 300f);
        }
        //for UI
public int PatternCount => patterns.Count;

private void SyncRhythmicSpheres()
{
    var spheres = FindObjectsByType<RhythmicBounceSphere>(FindObjectsSortMode.None);
    foreach (var sphere in spheres)
        sphere.SetBPM(bpm);
}

public Sequence GetPattern(int index)
{
    if (index < 0 || index >= patterns.Count) return null;
    return patterns[index];
}
private void Awake()
{
    if (playOnAwake && patterns != null && patterns.Count > 0)
        Play();
}
        private void Update()
        {
    if (!_playing) return;
    if (patterns == null || patterns.Count == 0)
    {
        _playing = false;
        return;
    }
            Sequence pattern = patterns[currentPatternIndex];
            if (pattern.steps == null || pattern.steps.Count == 0) return;

            _stepTimer  += Time.deltaTime;
            _gateTimer  += Time.deltaTime;

            // turn off note when gate expires
            if (_noteActive && _gateTimer >= _currentStepDuration * _lastStep.gateLength)
            {
                // don't send NoteOff if next step is a Tie
                int nextStep = (_currentStep + 1) % pattern.steps.Count;
                SequenceStep next = pattern.steps[nextStep];
                if (next.stepType != SequenceStep.StepType.Tie)
                    TriggerNoteOff(_lastStep);
            }

            // advance to next step
            if (_stepTimer >= _currentStepDuration)
            {
                _stepTimer -= _currentStepDuration;
                AdvanceStep();
            }
        }

        private void AdvanceStep()
        {
            Sequence pattern = patterns[currentPatternIndex];

            _currentStep = (_currentStep + 1) % pattern.steps.Count;

            // at the end of the pattern, check for pattern change
            if (_currentStep == 0)
            {
                if (playMode == PlayMode.Chain && _nextPatternIndex >= 0
                    && _nextPatternIndex < patterns.Count)
                {
                    currentPatternIndex = _nextPatternIndex;
                    _nextPatternIndex   = -1;
                    pattern             = patterns[currentPatternIndex];
                }
                else if (playMode == PlayMode.Loop)
                {
                    // stay on current pattern - nothing to do
                }
            }

            // calculate step duration accounting for swing
            _currentStepDuration = GetStepDuration(pattern, _currentStep);
            _gateTimer           = 0f;

            // trigger the step
            SequenceStep step = pattern.steps[_currentStep];
            TriggerStep(step);
        }

        private void TriggerStep(SequenceStep step)
        {
            if (!step.active || step.stepType == SequenceStep.StepType.Rest)
            {
                // silent step - make sure any held note is released
                if (_noteActive && _lastStep != null)
                {
                    TriggerNoteOff(_lastStep);
                    _noteActive = false;
                }
                return;
            }

            if (step.stepType == SequenceStep.StepType.Tie)
            {
                // extend the previous note - don't retrigger
                // just update gate timer so the note holds through this step too
                return;
            }

            // release previous note if one is held
            if (_noteActive && _lastStep != null)
                TriggerNoteOff(_lastStep);

            // trigger new note
            float velocity = step.stepType == SequenceStep.StepType.Accent
                ? accentVelocity
                : step.velocity;

            TriggerNoteOn(step, velocity);
            _lastStep   = step;
            _noteActive = true;
        }

private void TriggerNoteOn(SequenceStep step, float velocity)
{
    if (noteInput == null) return;
    float frequency = Note.Get(step.note, step.octave);
    noteInput.NoteOn(frequency, velocity);
}

        private void TriggerNoteOff(SequenceStep step)
        {
            if (noteInput == null) return;
            float frequency = Note.Get(step.note, step.octave);
            noteInput.NoteOff(frequency);
            _noteActive = false;
        }

        private float GetStepDuration(Sequence pattern, int stepIndex)
        {
            // base step duration in seconds - 16th notes at given BPM
            // BPM is in quarter notes, so 16th note = 60 / (BPM * 4)
            float baseDuration = 60f / (bpm * 4f);

            // apply swing to even steps (0-indexed so odd indices are even steps)
            if (stepIndex % 2 == 1)
                return baseDuration * (1f + pattern.swing);
            else
                return baseDuration * (1f - pattern.swing * 0.5f);
        }

        // --- transport controls ---

public void Play()
{
    if (patterns == null || patterns.Count == 0)
    {
        Debug.LogWarning("Sequencer has no patterns — add at least one pattern before playing.");
        return;
    }

    if (_playing) return;
    _playing     = true;
    _currentStep = -1;
    _stepTimer   = GetStepDuration(patterns[currentPatternIndex], 0);
}
        public void Stop()
        {
            _playing = false;
            if (_noteActive && _lastStep != null)
            {
                TriggerNoteOff(_lastStep);
                _noteActive = false;
            }
            _currentStep = 0;
            _stepTimer   = 0f;
        }

        public void Pause()
        {
            _playing = false;
            if (_noteActive && _lastStep != null)
            {
                TriggerNoteOff(_lastStep);
                _noteActive = false;
            }
        }

        public void Resume()
        {
            if (!_playing) _playing = true;
        }

        // queue a pattern to play after the current one finishes
        public void QueuePattern(int index)
        {
            if (index < 0 || index >= patterns.Count) return;
            if (playMode == PlayMode.Loop)
            {
                // in loop mode, switch immediately
                currentPatternIndex = index;
            }
            else
            {
                // in chain mode, queue for end of current pattern
                _nextPatternIndex = index;
            }
        }

        // jump immediately to a pattern
        public void JumpToPattern(int index)
        {
            if (index < 0 || index >= patterns.Count) return;
            if (_noteActive && _lastStep != null)
            {
                TriggerNoteOff(_lastStep);
                _noteActive = false;
            }
            currentPatternIndex = index;
            _currentStep        = -1;
            _stepTimer          = GetStepDuration(patterns[currentPatternIndex], 0);
        }

        // add a new empty pattern
        public void AddPattern()
        {
            patterns.Add(new Sequence());
        }

        // duplicate current pattern
        public void DuplicateCurrentPattern()
        {
            if (patterns.Count == 0) return;
            Sequence source = patterns[currentPatternIndex];
            Sequence copy   = new Sequence { name = source.name + " (copy)", swing = source.swing };
            foreach (var step in source.steps)
            {
                copy.steps.Add(new SequenceStep
                {
                    active     = step.active,
                    stepType   = step.stepType,
                    note       = step.note,
                    octave     = step.octave,
                    gateLength = step.gateLength,
                    velocity   = step.velocity
                });
            }
            patterns.Insert(currentPatternIndex + 1, copy);
        }
    }
}