using UnityEngine;
using UnityEngine.InputSystem;

namespace Synthic
{
    public class BPMSlider : MonoBehaviour
    {
        [Header("BPM Settings")]
        [SerializeField] private float minBPM        = 60f;
        [SerializeField] private float maxBPM        = 180f;
        [SerializeField] private float interactionRange = 30f;

        [Header("Targets")]
        [SerializeField] private PlatterSpinner      platter;
        [SerializeField] private Sequencer           sequencer;
        [SerializeField] private RhythmicMasterClock masterClock;

        [Header("Visual")]
        [SerializeField] private Renderer sliderRenderer;
        [SerializeField] private Color    normalColor   = Color.white;
        [SerializeField] private Color    inRangeColor  = Color.cyan;

        private float _currentBPM = 120f;
        private bool  _inRange    = false;

        public float CurrentBPM    => _currentBPM;
        public float InteractionRange => interactionRange;

        private void Awake()
        {
            SetBPM(120f);
        }

        public void SetInRange(bool inRange)
        {
            _inRange = inRange;
            SetColor(_inRange ? inRangeColor : normalColor);
        }

        public void IncrementBPM(float amount)
        {
            SetBPM(Mathf.Clamp(_currentBPM + amount, minBPM, maxBPM));
        }

        private void SetBPM(float bpm)
        {
            _currentBPM = Mathf.Round(bpm);

            if (platter     != null) platter.SetBPM(_currentBPM);
            if (sequencer   != null) sequencer.BPM = _currentBPM;
            if (masterClock != null) masterClock.SetBPM(_currentBPM);
        }

        private void SetColor(Color color)
        {
            if (sliderRenderer != null)
                sliderRenderer.material.color = color;
        }
    }
}