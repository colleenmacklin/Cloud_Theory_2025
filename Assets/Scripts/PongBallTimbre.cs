using UnityEngine;
using UnityEngine.InputSystem;

namespace Synthic
{
    /// <summary>
    /// Attach to the same GameObject as PolyphonicGenerator + AudioEffectsLFO
    /// for each paddle.
    ///
    /// When a PongBall that was launched by this paddle hits a paddle, the
    /// collision sends an impact velocity back here. This component uses that
    /// velocity to momentarily modulate the filter parameters, giving the
    /// two paddles distinct timbral identities — one might get a sharper
    /// attack with high-pass emphasis, the other a warmer low-pass sweep.
    ///
    /// The modulation fades back to the resting state over `returnTime`
    /// seconds.
    ///
    /// This is intentionally lightweight — it works through AudioEffectsLFO
    /// settings rather than audio-thread DSP, so no native code is needed.
    /// </summary>
    [RequireComponent(typeof(AudioEffectsLFO))]
    public class PongBallTimbre : MonoBehaviour
    {
        [Header("Low-pass sweep on hit")]
        [Tooltip("Cutoff frequency at rest (Hz)")]
        [SerializeField] private float restCutoff   = 5000f;
        [Tooltip("Cutoff added at full-velocity impact")]
        [SerializeField] private float peakCutoff   = 18000f;
        [Tooltip("Resonance at peak")]
        [SerializeField] private float peakResonance = 3f;
        [Tooltip("Seconds for the sweep to return to rest")]
        [SerializeField] private float returnTime   = 0.4f;

        private AudioLowPassFilter _lpf;
        private float _currentCutoff;
        private float _currentResonance;
        private float _targetCutoff;
        private float _targetResonance;
        private float _restResonance = 1f;

        private void Awake()
        {
            _lpf = GetComponent<AudioLowPassFilter>();
            if (_lpf == null)
                _lpf = gameObject.AddComponent<AudioLowPassFilter>();

            _currentCutoff    = restCutoff;
            _currentResonance = _restResonance;
            _targetCutoff     = restCutoff;
            _targetResonance  = _restResonance;
        }

        private void Update()
        {
            if (_lpf == null) return;

            float t = Time.deltaTime / Mathf.Max(returnTime, 0.001f);
            _currentCutoff    = Mathf.Lerp(_currentCutoff,    _targetCutoff,    t);
            _currentResonance = Mathf.Lerp(_currentResonance, _targetResonance, t);

            _lpf.cutoffFrequency      = _currentCutoff;
            _lpf.lowpassResonanceQ    = _currentResonance;
        }

        /// <summary>
        /// Called by PongBall on a paddle collision. velocity is 0–1.
        /// </summary>
        public void OnBallHit(float velocity)
        {
            _currentCutoff    = Mathf.Lerp(restCutoff, peakCutoff,    velocity);
            _currentResonance = Mathf.Lerp(_restResonance, peakResonance, velocity);
            // let them decay back to rest
            _targetCutoff    = restCutoff;
            _targetResonance = _restResonance;
        }
    }
}
