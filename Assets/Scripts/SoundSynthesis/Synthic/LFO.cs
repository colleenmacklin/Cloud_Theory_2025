using UnityEngine;

namespace Synthic
{
    [System.Serializable]
    public class LFO
    {
        public enum LFOTarget { Frequency, Amplitude }
        public enum WaveformType { Sine, Saw, Square, Triangle }

        [Tooltip("What the LFO modulates:\n• Frequency = vibrato / pitch wobble\n• Amplitude = tremolo / volume pulse")]
        [SerializeField] public LFOTarget lfoTarget = LFOTarget.Frequency;

        [Tooltip("Shape of the LFO oscillation:\n• Sine = smooth, natural\n• Saw = rising ramp, robotic\n• Square = hard on/off gate\n• Triangle = linear up/down")]
        [SerializeField] public WaveformType waveform = WaveformType.Sine;

        [Tooltip("Speed of the LFO in Hz (cycles per second).\n• 0.1–1 Hz = slow, expressive sweep\n• 1–6 Hz = vibrato / tremolo range\n• 6–20 Hz = fast flutter")]
        [SerializeField, Range(0.1f, 20f)] public float rate = 1f;

        [Tooltip("How much the LFO moves the parameter, as a fraction of the resting level.\n• 0 = no modulation\n• 0.01–0.03 = subtle vibrato\n• 0.1–0.3 = noticeable wobble\n• 0.5–1.0 = extreme / full sweep")]
        [SerializeField, Range(0f, 1f)] public float depth = 0.5f;

        [Tooltip("The resting (center) value the LFO oscillates around, normalised 0–1.\n\nFor Frequency LFO:\n• 0.5 = center of pitch range (recommended for vibrato)\n• Leave at 0.5 unless you want pitch biased up or down\n\nFor Amplitude LFO:\n• 1.0 = full volume at rest, LFO ducks it down (tremolo)\n• 0.5 = half volume at rest\n• 0.0 = silent at rest, LFO opens it up")]
        [SerializeField, Range(0f, 1f)] public float restingLevel = 0.5f;

        private float _phase;

        public float Update(float deltaTime)
        {
            _phase = (_phase + rate * deltaTime) % 1f;

            float lfoValue = waveform switch
            {
                WaveformType.Sine     => Mathf.Sin(_phase * 2f * Mathf.PI),
                WaveformType.Saw      => _phase * 2f - 1f,
                WaveformType.Square   => _phase < 0.5f ? 1f : -1f,
                WaveformType.Triangle => _phase < 0.5f ? (_phase * 4f - 1f) : (3f - _phase * 4f),
                _                     => 0f
            };

            return restingLevel + lfoValue * depth * restingLevel;
        }

        // ── Backward-compat property so existing code using .baseValue still compiles ──
        public float baseValue
        {
            get => restingLevel;
            set => restingLevel = value;
        }
    }
}
