using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    public class RhythmicMasterClock : MonoBehaviour
    {
        public static RhythmicMasterClock Instance { get; private set; }

        [SerializeField] private float bpm = 120f;
        [SerializeField] private Sequencer sequencer;
        [Tooltip("How many quarter-note beats make up one bar/measure. OnBar fires once every this many OnBeat ticks.")]
        [SerializeField] private int beatsPerBar = 4;

        private float _masterTimer    = 0f;
        private float _quarterTimer   = 0f;
        private int _beatCount        = 0;

        private List<RhythmicBounceSphere> _spheres   = new();
        private List<PlatterSpinner> _platters  = new();

        public float BPM => bpm;
        public float MasterTimer  => _masterTimer;
        public float BeatDuration => 60f / bpm;
        public int BeatsPerBar => beatsPerBar;
        // How many quarter-note beats have elapsed since this clock started, incremented
        // just before OnBeat fires each tick.
        public int BeatCount => _beatCount;

        public event System.Action OnBeat;
        // Fires once every beatsPerBar quarter notes, right after OnBeat on that tick —
        // a generic "one measure has elapsed" signal for anything gated on a full measure
        // (e.g. "the player stayed still for one measure").
        public event System.Action OnBar;

        // Converts a rhythmic note value into seconds at the clock's current BPM.
        // ThirtySecond = 1/8 of a beat, Whole = 4 beats, etc.
        public float SecondsForDuration(NoteDuration duration)
        {
            float beats = duration switch
            {
                NoteDuration.ThirtySecond => 0.125f,
                NoteDuration.Sixteenth    => 0.25f,
                NoteDuration.Eighth       => 0.5f,
                NoteDuration.Quarter      => 1f,
                NoteDuration.Half         => 2f,
                NoteDuration.Whole        => 4f,
                _ => 1f
            };
            return beats * BeatDuration;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            RefreshSphereList();
        }

        private void Update()
        {
                // push master clock BPM to sequencer instead of pulling
    if (sequencer != null)
        sequencer.BPM = bpm;

    float quarterNote = 60f / bpm;

            _masterTimer  += Time.deltaTime;
            _quarterTimer += Time.deltaTime;

            // fire on quarter note boundary
            if (_quarterTimer >= quarterNote)
            {
                _quarterTimer -= quarterNote;
                OnQuarterNote();
            }

            // sync bounce spheres every beat
            if (_masterTimer >= quarterNote)
            {
                _masterTimer -= quarterNote;
                foreach (var sphere in _spheres)
                    if (sphere != null)
                        sphere.Resync(_masterTimer);
            }
        }

        private void OnQuarterNote()
        {
            _beatCount++;
            OnBeat?.Invoke();

            if (beatsPerBar > 0 && _beatCount % beatsPerBar == 0)
                OnBar?.Invoke();

            // start any platters waiting for sync
            foreach (var platter in _platters)
            {
                if (platter != null && platter.WaitingForSync)
                    platter.StartNow();
            }
        }

        public void RegisterPlatter(PlatterSpinner platter)
        {
            if (!_platters.Contains(platter))
                _platters.Add(platter);
        }

        public void UnregisterPlatter(PlatterSpinner platter)
        {
            _platters.Remove(platter);
        }

        public void RefreshSphereList()
        {
            _spheres.Clear();
            _spheres.AddRange(
                FindObjectsByType<RhythmicBounceSphere>(FindObjectsSortMode.None));
            foreach (var sphere in _spheres)
                sphere.SetBPM(bpm);
        }

public void SetBPM(float newBpm)
{
    bpm = Mathf.Clamp(newBpm, 60f, 180f);
    if (sequencer != null)
        sequencer.BPM = bpm;
    foreach (var sphere in _spheres)
        sphere.SetBPM(bpm);
    foreach (var platter in _platters)
        platter.SetBPM(bpm);
}
    }
}