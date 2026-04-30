using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    public class RhythmicMasterClock : MonoBehaviour
    {
        public static RhythmicMasterClock Instance { get; private set; }

        [SerializeField] private float bpm = 120f;
        [SerializeField] private Sequencer sequencer;

        private float _masterTimer    = 0f;
        private float _quarterTimer   = 0f;

        private List<RhythmicBounceSphere> _spheres   = new();
        private List<PlatterSpinner> _platters  = new();

        public float BPM => bpm;
        public float MasterTimer  => _masterTimer;
        public float BeatDuration => 60f / bpm;

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