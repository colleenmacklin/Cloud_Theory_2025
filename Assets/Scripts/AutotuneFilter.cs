using UnityEngine;

/// <summary>
/// Autotune filter: pitch-shifts the RTVoice output to the nearest note
/// in the CarrierSynth chord.
///
/// Setup: Same GameObject as SpeakerAudio. Must appear ABOVE VocoderFilter
///        in the Inspector component list (Unity processes top-to-bottom).
///
/// Key design decisions vs. previous version:
///   - Dual-playhead OLA: two read heads 180° out of phase, cross-faded by
///     a Hann window. This is the textbook "pitch shift without FFT" approach
///     and is much more robust than single-grain OLA.
///   - Forced minimum correction: TTS voices are often monotone (flat pitch),
///     so instead of detecting the voice pitch and shifting to nearest note,
///     we ALWAYS pitch-shift to the root note of the chord. This is more
///     reliable and actually sounds more musical with synthetic TTS.
///   - The "natural" mode (autocorrelation → nearest chord note) is available
///     as a fallback if your TTS has enough pitch variation.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AutotuneFilter : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────
    [Header("References")]
    public CarrierSynth Carrier;

    [Header("On/Off")]
    public bool AutotuneEnabled = true;

    [Header("Pitch Mode")]
    [Tooltip("Forced: always shift to root note (reliable with flat TTS). " +
             "Detected: autocorrelation → nearest chord note (needs pitched voice).")]
    public PitchMode Mode = PitchMode.Forced;

    [Tooltip("When Mode=Forced, which chord interval index to target (0 = root).")]
    [Range(0, 4)]
    public int TargetIntervalIndex = 0;

    [Header("Correction Amount")]
    [Range(0f, 1f)]
    [Tooltip("0 = no shift, 1 = full shift to target note. Start at 1.0.")]
    public float CorrectionStrength = 1.0f;

    [Range(0f, 0.15f)]
    [Tooltip("Portamento glide time in seconds between note targets.")]
    public float PitchSmoothing = 0.04f;

    [Header("Detection (Mode=Detected only)")]
    [Range(0.001f, 0.05f)]
    public float SilenceThreshold = 0.008f;

    [Header("Debug (read-only)")]
    [SerializeField] private float _debugDetectedHz;
    [SerializeField] private float _debugTargetHz;
    [SerializeField] private float _debugCurrentRatio;

    // ── Mode enum ────────────────────────────────────────────────────────
    public enum PitchMode { Forced, Detected }

    // ── Dual-playhead OLA constants ───────────────────────────────────────
    // Period = the window length for each playhead cycle.
    // Must be long enough to contain the longest expected pitch period
    // (lowest voice ~80Hz @ 44100 = ~551 samples) but short enough to
    // avoid smearing transients. 1024 is a good middle ground.
    private const int Period   = 1024;
    private const int BufSize  = 65536; // must be >> Period * max_ratio

    // ── Buffers ───────────────────────────────────────────────────────────
    private float[] _inBuf;     // circular input buffer
    private int     _inWrite;

    private float[] _outBuf;    // circular output accumulation buffer
    private int     _outWrite;
    private int     _outRead;

    private float[] _window;    // Hann window, length = Period

    // ── Playhead state ────────────────────────────────────────────────────
    // Two playheads read from _inBuf at different rates (controlled by ratio).
    // They are Period/2 apart and cross-faded, so one always fades in as the
    // other fades out — seamless.
    private float _ph0 = 0f;    // playhead 0 position (fractional sample index)
    private float _ph1 = 0f;    // playhead 1 position

    // Input read pointer that advances at the NATURAL rate (ratio=1)
    // The playheads advance at ratio * natural_rate.
    private float _inRead = 0f;

    // ── Pitch state ───────────────────────────────────────────────────────
    private float _smoothedRatio = 1f;
    private float _detectedHz    = 0f;
    private float _targetHz      = 0f;

    // ── Autocorrelation buffer ────────────────────────────────────────────
    private const int AcSize   = 4096;
    private const int MinPer   = 20;    // ~2205 Hz
    private const int MaxPer   = 800;   // ~55 Hz
    private float[]   _acBuf;
    private int       _acWrite;

    private int  _sampleRate;
    private bool _ready;

    // ─────────────────────────────────────────────────────────────────────

    void Start()
    {
        _sampleRate = AudioSettings.outputSampleRate;

        _inBuf  = new float[BufSize];
        _outBuf = new float[BufSize];
        _window = new float[Period];
        _acBuf  = new float[AcSize];

        for (int i = 0; i < Period; i++)
            _window[i] = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (Period - 1)));

        // Stagger the two playheads half a period apart
        _ph0 = 0f;
        _ph1 = Period * 0.5f;

        // Prime output ring with latency headroom
        _outWrite = Period;
        _outRead  = 0;

        _ready = true;
    }

    // ── DSP callback ─────────────────────────────────────────────────────
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_ready || !AutotuneEnabled || Carrier == null) return;

        int frames = data.Length / channels;

        // Update smoothed ratio once per buffer (not per sample — saves CPU)
        float targetRatio  = GetTargetRatio();
        float smoothSamples = Mathf.Max(1f, PitchSmoothing * _sampleRate);
        float k = 1f - Mathf.Exp(-frames / smoothSamples);
        _smoothedRatio = Mathf.Lerp(_smoothedRatio, targetRatio, k);
        _smoothedRatio = Mathf.Clamp(_smoothedRatio, 0.25f, 4.0f);

        _debugCurrentRatio = _smoothedRatio;

        float ratio = _smoothedRatio;

        for (int f = 0; f < frames; f++)
        {
            // Read mono input
            float input = 0f;
            for (int c = 0; c < channels; c++)
                input += data[f * channels + c];
            input /= channels;

            // Write to input buffer and autocorrelation buffer
            _inBuf[_inWrite % BufSize] = input;
            _inWrite++;

            _acBuf[_acWrite % AcSize] = input;
            _acWrite++;

            // ── Dual-playhead synthesis ───────────────────────────────────
            // Playhead 0
            float s0    = ReadInBuf(_ph0);
            int   wi0   = (int)((_ph0 % Period + Period) % Period);
            float w0    = _window[wi0];

            // Playhead 1 (half period offset → always cross-fading with ph0)
            float s1    = ReadInBuf(_ph1);
            int   wi1   = (int)((_ph1 % Period + Period) % Period);
            float w1    = _window[wi1];

            float shifted = s0 * w0 + s1 * w1;

            // Advance playheads at ratio rate
            _ph0 = (_ph0 + ratio + BufSize) % BufSize;
            _ph1 = (_ph1 + ratio + BufSize) % BufSize;

            // Advance natural input pointer at rate 1
            _inRead = (_inRead + 1f + BufSize) % BufSize;

            // Keep playheads anchored to the input pointer's neighbourhood
            // (prevents them drifting too far ahead or behind the live input)
            WrapPlayhead(ref _ph0);
            WrapPlayhead(ref _ph1);

            // Write to output ring
            _outBuf[(int)(_outWrite % BufSize)] = shifted;
            _outWrite = (_outWrite + 1) % BufSize;

            // Read from output ring
            float outSample = _outBuf[(int)(_outRead % BufSize)];
            _outRead = (_outRead + 1) % BufSize;

            for (int c = 0; c < channels; c++)
                data[f * channels + c] = outSample;
        }

        // Periodically detect pitch (every ~1024 samples — no need per-sample)
        if (_acWrite % 1024 < frames)
            DetectPitch();
    }

    // ── Playhead wrap ─────────────────────────────────────────────────────
    // If a playhead has drifted more than Period away from the current input
    // write position, snap it back. This prevents the pitch shift from
    // chasing a position that no longer exists in the buffer.
    private void WrapPlayhead(ref float ph)
    {
        float diff = (_inWrite - ph + BufSize) % BufSize;
        if (diff > Period * 2f)
            ph = (_inWrite - Period + BufSize) % BufSize;
        if (diff < 1f)
            ph = (_inWrite - Period * 0.5f + BufSize) % BufSize;
    }

    // ── Interpolated read from input ring ─────────────────────────────────
    private float ReadInBuf(float pos)
    {
        int   i0   = (int)(pos) % BufSize;
        int   i1   = (i0 + 1)   % BufSize;
        float frac = pos - Mathf.Floor(pos);
        return Mathf.Lerp(_inBuf[i0], _inBuf[i1], frac);
    }

    // ── Target ratio calculation ──────────────────────────────────────────
    private float GetTargetRatio()
    {
        if (Carrier == null) return 1f;

        float targetHz;

        if (Mode == PitchMode.Forced)
        {
            // Always target a specific chord note, regardless of detected pitch.
            // Default: target the ROOT note in the vocal octave range (200–500 Hz).
            int idx  = Mathf.Clamp(TargetIntervalIndex, 0, Carrier.chordIntervals.Length - 1);
            int midi = Carrier.rootMidi + Carrier.chordIntervals[idx];
            targetHz = CarrierSynth.MidiToHz(midi);

            // Fold into the vocal range (roughly 150–450 Hz for TTS voices)
            while (targetHz < 150f) targetHz *= 2f;
            while (targetHz > 450f) targetHz *= 0.5f;

            // In Forced mode we need a reference "detected" pitch to compute
            // the ratio. Use the last detected pitch, or assume 220 Hz (A3)
            // if detection hasn't produced a confident result.
            float refHz = (_detectedHz > 50f) ? _detectedHz : 220f;

            _targetHz       = targetHz;
            _debugTargetHz  = targetHz;

            float fullRatio = targetHz / refHz;
            return Mathf.Exp(Mathf.Log(fullRatio) * CorrectionStrength);
        }
        else
        {
            // Detected mode: snap detected pitch to nearest chord note
            if (_detectedHz < 50f) return 1f;

            float[] chordFreqs = GetChordFreqsNear(_detectedHz);
            targetHz = chordFreqs[0];
            float minCents = float.MaxValue;
            foreach (float cf in chordFreqs)
            {
                float cents = Mathf.Abs(1200f * Mathf.Log(cf / _detectedHz) / Mathf.Log(2f));
                if (cents < minCents) { minCents = cents; targetHz = cf; }
            }

            _targetHz      = targetHz;
            _debugTargetHz = targetHz;

            float ratio = targetHz / _detectedHz;
            return Mathf.Exp(Mathf.Log(ratio) * CorrectionStrength);
        }
    }

    // ── Autocorrelation pitch detection ───────────────────────────────────
    private void DetectPitch()
    {
        float rms = 0f;
        for (int i = 0; i < AcSize; i++) rms += _acBuf[i] * _acBuf[i];
        rms = Mathf.Sqrt(rms / AcSize);
        if (rms < SilenceThreshold) { _detectedHz = 0f; _debugDetectedHz = 0f; return; }

        float best = -1f;
        int   bestP = MinPer;
        for (int p = MinPer; p <= MaxPer; p++)
        {
            float c = 0f;
            int   n = AcSize - p;
            for (int i = 0; i < n; i++)
                c += _acBuf[i % AcSize] * _acBuf[(i + p) % AcSize];
            c /= n;
            if (c > best) { best = c; bestP = p; }
        }

        _detectedHz      = best > 0.05f ? (float)_sampleRate / bestP : 0f;
        _debugDetectedHz = _detectedHz;
    }

    // ── Chord note octave-folding ─────────────────────────────────────────
    private float[] GetChordFreqsNear(float refHz)
    {
        var intervals = Carrier.chordIntervals;
        var result    = new float[intervals.Length];
        for (int i = 0; i < intervals.Length; i++)
        {
            float f = CarrierSynth.MidiToHz(Carrier.rootMidi + intervals[i]);
            while (f < refHz * 0.7071f) f *= 2f;
            while (f > refHz * 1.4142f) f *= 0.5f;
            result[i] = f;
        }
        return result;
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void EnableAutotune()  => AutotuneEnabled = true;
    public void DisableAutotune() => AutotuneEnabled = false;
    public void ToggleAutotune()  => AutotuneEnabled = !AutotuneEnabled;

    public float DetectedHz => _detectedHz;
    public float TargetHz   => _targetHz;
}
