using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Synthic
{
    [System.Serializable]
    public class SynthPreset
    {
        public string name = "New Preset";

        [Header("Waveform")]
        [Tooltip("Sine = pure, flute-like, no harmonics\nSaw = bright, buzzy, rich harmonics\nSquare = hollow, reedy, odd harmonics only\nTriangle = softer than square, warm")]
        public PolyphonicGenerator.WaveformType waveform = PolyphonicGenerator.WaveformType.Sine;

        [Header("ADSR — all values in seconds (sustain is 0–1 level)")]
        [Tooltip("Ramp-up time in seconds after NoteOn.\n0.001 = instant  |  0.1 = soft attack  |  1+ = slow swell")]
        [Range(0.001f, 5f)] public float attack  = 0.01f;

        [Tooltip("Time in seconds to fall to the Sustain level.\n0.05 = tight pluck  |  0.3 = medium  |  1+ = long bloom")]
        [Range(0.001f, 5f)] public float decay   = 0.1f;

        [Tooltip("Volume held while note is active, after Decay. 0 = note dies (pluck). 1 = holds at full (organ).")]
        [Range(0f, 1f)]     public float sustain = 0.7f;

        [Tooltip("Fade-out time in seconds after NoteOff.\n0.05 = staccato  |  0.3 = short tail  |  2+ = long reverberant fade")]
        [Range(0.001f, 5f)] public float release = 0.3f;

        [Header("Frequency LFO — vibrato / pitch modulation")]
        [Tooltip("Enable vibrato / pitch wobble")]
        public bool freqLfoEnabled = false;

        [Tooltip("Shape of the pitch oscillation")]
        public LFO.WaveformType freqLfoWaveform = LFO.WaveformType.Sine;

        [Tooltip("Speed in Hz.\n0.1–1 = slow sweep  |  4–6 = classic vibrato  |  10+ = fast flutter")]
        [Range(0.1f, 20f)] public float freqLfoRate = 1f;

        [Tooltip("Modulation amount as a fraction of the resting pitch level.\n0.005–0.02 = subtle vibrato  |  0.1+ = wide wobble")]
        [Range(0f, 1f)]    public float freqLfoDepth = 0f;

        [Tooltip("Resting pitch center, normalised 0–1. Leave at 0.5 for vibrato centered on the played note.")]
        [Range(0f, 1f)]    public float freqLfoBaseValue = 0.5f;

        [Header("Amplitude LFO — tremolo / volume modulation")]
        [Tooltip("Enable tremolo / rhythmic volume pulsing")]
        public bool ampLfoEnabled = false;

        [Tooltip("Shape of the volume oscillation")]
        public LFO.WaveformType ampLfoWaveform = LFO.WaveformType.Sine;

        [Tooltip("Speed in Hz.\n1–4 = slow tremolo  |  6–10 = fast flutter  |  12+ = stutter/gate")]
        [Range(0.1f, 20f)] public float ampLfoRate = 1f;

        [Tooltip("How much the volume dips, as a fraction of the resting level.\n0.1–0.3 = gentle pulse  |  0.5–0.8 = strong tremolo  |  1.0 = full gate (silent at trough)")]
        [Range(0f, 1f)]    public float ampLfoDepth = 0f;

        [Tooltip("Resting volume level, normalised 0–1.\n1.0 = full volume at rest, LFO dips it down (tremolo)\n0.5 = half volume at rest\n0.0 = silent at rest, LFO opens it up")]
        [Range(0f, 1f)]    public float ampLfoBaseValue = 1f;
    }

    public class SynthPresets : MonoBehaviour
    {
        [SerializeField] private PolyphonicGenerator generator;

        [Tooltip("Primary preset bank - triggered by 1-9 (no Caps Lock)")]
        [SerializeField] private List<SynthPreset> presets = new();

        [Tooltip("Alternate preset bank - triggered by 1-9 while Caps Lock is held")]
        [SerializeField] private List<SynthPreset> capsLockPresets = new();

        private int _currentPresetIndex     = -1;
        private bool _currentBankIsCapsLock = false;

        private void Awake()
        {
            InitializeDefaultPresets();
            InitializeDefaultCapsLockPresets();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            bool capsLockOn = Keyboard.current.capsLockKey.isPressed;

            // route digit keys to whichever bank is active
            if (Keyboard.current.digit1Key.wasPressedThisFrame) ApplyPreset(0, capsLockOn);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) ApplyPreset(1, capsLockOn);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) ApplyPreset(2, capsLockOn);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) ApplyPreset(3, capsLockOn);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) ApplyPreset(4, capsLockOn);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) ApplyPreset(5, capsLockOn);
            if (Keyboard.current.digit7Key.wasPressedThisFrame) ApplyPreset(6, capsLockOn);
            if (Keyboard.current.digit8Key.wasPressedThisFrame) ApplyPreset(7, capsLockOn);
            if (Keyboard.current.digit9Key.wasPressedThisFrame) ApplyPreset(8, capsLockOn);
        }

        /// <summary>Apply a preset from the primary or caps-lock bank.</summary>
        public void ApplyPreset(int index, bool capsLock = false)
        {
            if (generator == null) return;

            List<SynthPreset> bank = capsLock ? capsLockPresets : presets;
            if (index < 0 || index >= bank.Count) return;

            SynthPreset preset    = bank[index];
            _currentPresetIndex   = index;
            _currentBankIsCapsLock = capsLock;

            generator.Waveform = preset.waveform;
            generator.SetAttack(preset.attack);
            generator.SetDecay(preset.decay);
            generator.SetSustain(preset.sustain);
            generator.SetRelease(preset.release);
            ApplyLFOSettings(preset);

            Debug.Log($"Preset {index + 1}{(capsLock ? " [ALT]" : "")} applied: {preset.name}");
        }

        // ── LFO application ───────────────────────────────────────────────────

        private void ApplyLFOSettings(SynthPreset preset)
        {
            var lfos = generator.GetLFOs();
            if (lfos == null) return;

            LFO freqLfo = lfos.Find(l => l.lfoTarget == LFO.LFOTarget.Frequency);
            LFO ampLfo  = lfos.Find(l => l.lfoTarget == LFO.LFOTarget.Amplitude);

            if (preset.freqLfoEnabled)
            {
                if (freqLfo == null) { freqLfo = new LFO(); lfos.Add(freqLfo); }
                freqLfo.lfoTarget = LFO.LFOTarget.Frequency;
                freqLfo.waveform  = preset.freqLfoWaveform;
                freqLfo.rate      = preset.freqLfoRate;
                freqLfo.depth     = preset.freqLfoDepth;
                freqLfo.baseValue = preset.freqLfoBaseValue;
            }
            else if (freqLfo != null)
            {
                lfos.Remove(freqLfo);
            }

            if (preset.ampLfoEnabled)
            {
                if (ampLfo == null) { ampLfo = new LFO(); lfos.Add(ampLfo); }
                ampLfo.lfoTarget = LFO.LFOTarget.Amplitude;
                ampLfo.waveform  = preset.ampLfoWaveform;
                ampLfo.rate      = preset.ampLfoRate;
                ampLfo.depth     = preset.ampLfoDepth;
                ampLfo.baseValue = preset.ampLfoBaseValue;
            }
            else if (ampLfo != null)
            {
                lfos.Remove(ampLfo);
            }
        }

        // ── Default primary presets (unchanged) ───────────────────────────────

        private void InitializeDefaultPresets()
        {
            if (presets.Count > 0) return;

            presets = new List<SynthPreset>
            {
                new SynthPreset // 1 - clean sine
                {
                    name = "Clean Sine", waveform = PolyphonicGenerator.WaveformType.Sine,
                    attack = 0.01f, decay = 0.1f, sustain = 0.7f, release = 0.3f,
                },
                new SynthPreset // 2 - pluck
                {
                    name = "Pluck", waveform = PolyphonicGenerator.WaveformType.Saw,
                    attack = 0.001f, decay = 0.3f, sustain = 0f, release = 0.1f,
                },
                new SynthPreset // 3 - pad
                {
                    name = "Pad", waveform = PolyphonicGenerator.WaveformType.Sine,
                    attack = 0.8f, decay = 0.5f, sustain = 0.8f, release = 1.5f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 0.5f, freqLfoDepth = 0.02f, freqLfoBaseValue = 0.5f,
                },
                new SynthPreset // 4 - wobble bass
                {
                    name = "Wobble Bass", waveform = PolyphonicGenerator.WaveformType.Saw,
                    attack = 0.01f, decay = 0.2f, sustain = 0.8f, release = 0.2f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 4f, freqLfoDepth = 0.3f, freqLfoBaseValue = 0.5f,
                },
                new SynthPreset // 5 - tremolo
                {
                    name = "Tremolo", waveform = PolyphonicGenerator.WaveformType.Triangle,
                    attack = 0.05f, decay = 0.1f, sustain = 0.9f, release = 0.3f,
                    ampLfoEnabled = true, ampLfoWaveform = LFO.WaveformType.Sine,
                    ampLfoRate = 6f, ampLfoDepth = 0.8f, ampLfoBaseValue = 0.8f,
                },
                new SynthPreset // 6 - organ
                {
                    name = "Organ", waveform = PolyphonicGenerator.WaveformType.Square,
                    attack = 0.01f, decay = 0.01f, sustain = 1f, release = 0.05f,
                },
                new SynthPreset // 7 - bell
                {
                    name = "Bell", waveform = PolyphonicGenerator.WaveformType.Sine,
                    attack = 0.001f, decay = 1.5f, sustain = 0f, release = 0.5f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 0.2f, freqLfoDepth = 0.01f, freqLfoBaseValue = 0.5f,
                },
                new SynthPreset // 8 - stutter
                {
                    name = "Stutter", waveform = PolyphonicGenerator.WaveformType.Square,
                    attack = 0.001f, decay = 0.05f, sustain = 0f, release = 0.01f,
                    ampLfoEnabled = true, ampLfoWaveform = LFO.WaveformType.Square,
                    ampLfoRate = 8f, ampLfoDepth = 1f, ampLfoBaseValue = 1f,
                },
                new SynthPreset // 9 - saw lead
                {
                    name = "Saw Lead", waveform = PolyphonicGenerator.WaveformType.Saw,
                    attack = 0.05f, decay = 0.2f, sustain = 0.6f, release = 0.4f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 5f, freqLfoDepth = 0.02f, freqLfoBaseValue = 0.5f,
                },
            };
        }

        // ── Default caps-lock alternate presets ───────────────────────────────

        private void InitializeDefaultCapsLockPresets()
        {
            if (capsLockPresets.Count > 0) return;

            capsLockPresets = new List<SynthPreset>
            {
                new SynthPreset // CL-1 - sub bass
                {
                    name = "Sub Bass", waveform = PolyphonicGenerator.WaveformType.Sine,
                    attack = 0.02f, decay = 0.3f, sustain = 0.9f, release = 0.4f,
                },
                new SynthPreset // CL-2 - acid bass
                {
                    name = "Acid Bass", waveform = PolyphonicGenerator.WaveformType.Saw,
                    attack = 0.001f, decay = 0.15f, sustain = 0.3f, release = 0.1f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Saw,
                    freqLfoRate = 2f, freqLfoDepth = 0.5f, freqLfoBaseValue = 0.3f,
                },
                new SynthPreset // CL-3 - soft pad
                {
                    name = "Soft Pad", waveform = PolyphonicGenerator.WaveformType.Triangle,
                    attack = 1.2f, decay = 0.8f, sustain = 0.9f, release = 2f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 0.3f, freqLfoDepth = 0.01f, freqLfoBaseValue = 0.5f,
                    ampLfoEnabled = true, ampLfoWaveform = LFO.WaveformType.Sine,
                    ampLfoRate = 0.4f, ampLfoDepth = 0.15f, ampLfoBaseValue = 0.9f,
                },
                new SynthPreset // CL-4 - flute
                {
                    name = "Flute", waveform = PolyphonicGenerator.WaveformType.Sine,
                    attack = 0.1f, decay = 0.05f, sustain = 0.8f, release = 0.3f,
                    ampLfoEnabled = true, ampLfoWaveform = LFO.WaveformType.Sine,
                    ampLfoRate = 5.5f, ampLfoDepth = 0.1f, ampLfoBaseValue = 1f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 5f, freqLfoDepth = 0.005f, freqLfoBaseValue = 0.5f,
                },
                new SynthPreset // CL-5 - glass
                {
                    name = "Glass", waveform = PolyphonicGenerator.WaveformType.Triangle,
                    attack = 0.001f, decay = 0.8f, sustain = 0.1f, release = 1.2f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 0.1f, freqLfoDepth = 0.003f, freqLfoBaseValue = 0.5f,
                },
                new SynthPreset // CL-6 - brass
                {
                    name = "Brass", waveform = PolyphonicGenerator.WaveformType.Square,
                    attack = 0.08f, decay = 0.2f, sustain = 0.7f, release = 0.25f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 6f, freqLfoDepth = 0.01f, freqLfoBaseValue = 0.5f,
                },
                new SynthPreset // CL-7 - drone
                {
                    name = "Drone", waveform = PolyphonicGenerator.WaveformType.Saw,
                    attack = 2f, decay = 0.5f, sustain = 1f, release = 3f,
                    ampLfoEnabled = true, ampLfoWaveform = LFO.WaveformType.Sine,
                    ampLfoRate = 0.15f, ampLfoDepth = 0.3f, ampLfoBaseValue = 0.9f,
                },
                new SynthPreset // CL-8 - glitch
                {
                    name = "Glitch", waveform = PolyphonicGenerator.WaveformType.Square,
                    attack = 0.001f, decay = 0.02f, sustain = 0.5f, release = 0.02f,
                    ampLfoEnabled = true, ampLfoWaveform = LFO.WaveformType.Square,
                    ampLfoRate = 16f, ampLfoDepth = 1f, ampLfoBaseValue = 1f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Square,
                    freqLfoRate = 8f, freqLfoDepth = 0.15f, freqLfoBaseValue = 0.5f,
                },
                new SynthPreset // CL-9 - string ensemble
                {
                    name = "Strings", waveform = PolyphonicGenerator.WaveformType.Saw,
                    attack = 0.4f, decay = 0.3f, sustain = 0.8f, release = 1f,
                    freqLfoEnabled = true, freqLfoWaveform = LFO.WaveformType.Sine,
                    freqLfoRate = 4.5f, freqLfoDepth = 0.008f, freqLfoBaseValue = 0.5f,
                    ampLfoEnabled = true, ampLfoWaveform = LFO.WaveformType.Sine,
                    ampLfoRate = 5f, ampLfoDepth = 0.05f, ampLfoBaseValue = 1f,
                },
            };
        }

        // ── Public accessors ──────────────────────────────────────────────────

        public int         CurrentPresetIndex     => _currentPresetIndex;
        public bool        CurrentBankIsCapsLock  => _currentBankIsCapsLock;
        public SynthPreset CurrentPreset
        {
            get
            {
                var bank = _currentBankIsCapsLock ? capsLockPresets : presets;
                return _currentPresetIndex >= 0 && _currentPresetIndex < bank.Count
                    ? bank[_currentPresetIndex] : null;
            }
        }
    }
}
