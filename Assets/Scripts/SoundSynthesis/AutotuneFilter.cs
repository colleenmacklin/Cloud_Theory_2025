using UnityEngine;

/// <summary>
/// Pitch shifter (time-domain OLA) for RTVoice.
/// Attach to the same GameObject as SpeakerAudio, ABOVE VocoderFilter.
///
/// OLA pitch shifting principle:
///   - Read input at a FIXED rate (1 sample per output sample)
///   - Write output grains with a hop size scaled by 1/ratio
///   - ratio > 1 = shorter output hops = higher pitch, same duration
///   - ratio < 1 = longer output hops = lower pitch, same duration
///
/// Chord source priority:
///   If ChordSource (CloudChordController) is assigned, it is used for all
///   chord-tone data in Forced / Sweep / Detected / WordAligned modes.
///   If ChordSource is null, the legacy Carrier (CarrierSynth) is used instead,
///   so existing VocoderController setups continue to work without changes.
///
/// WordAligned mode:
///   Each spoken word advances the target pitch to the next chord tone.
///   CloudChordController handles the word-event subscription and exposes
///   WordNoteIndex. Simply set Mode = WordAligned and assign ChordSource.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AutotuneFilter : MonoBehaviour
{
    public enum PitchMode  { Test, Forced, Sweep, Detected, WordAligned }
    public enum SweepShape { Forward, PingPong, Random }

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Primary chord source — assign a CloudChordController for the cloud scene.")]
    public CloudChordController ChordSource;

    [Tooltip("Legacy chord source — used when ChordSource is null (VocoderController pipeline).")]
    public CarrierSynth Carrier;

    [Header("On/Off")]
    public bool AutotuneEnabled = true;

    [Header("Pitch Mode")]
    [Tooltip("Test: manual semitone offset to verify shifter is working.\n" +
             "Forced: fixed chord interval.\n" +
             "Sweep: LFO through chord notes.\n" +
             "Detected: autocorrelation snap.\n" +
             "WordAligned: advances chord tone on each spoken word (requires ChordSource).")]
    public PitchMode Mode = PitchMode.Test;

    [Header("Test Mode")]
    [Range(-12f, 12f)]
    [Tooltip("+7 = fifth up (obvious test). +12 = octave up.")]
    public float TestSemitones = 7f;

    [Header("Forced Mode")]
    [Range(0, 4)]
    public int TargetIntervalIndex = 0;

    [Header("Sweep Mode")]
    [Range(0.5f, 16f)]
    public float SweepNoteDuration = 3f;
    public SweepShape Shape = SweepShape.PingPong;

    [Header("Correction (Forced / Sweep / Detected / WordAligned)")]
    [Range(0f, 1f)]
    public float CorrectionStrength = 0.8f;

    [Range(0.05f, 0.5f)]
    public float PitchSmoothing = 0.15f;

    [Header("Debug (read-only)")]
    [SerializeField] private float _debugTargetRatio;
    [SerializeField] private float _debugSmoothedRatio;
    [SerializeField] private float _debugDetectedHz;
    [SerializeField] private int   _debugSweepStep;

    // ── OLA buffers ───────────────────────────────────────────────────────
    private const int GrainSize = 512;   // ~11.6ms @ 44100
    private const int BufSize   = 65536; // must be >> GrainSize * max_ratio

    private float[] _inBuf;
    private float[] _outBuf;
    private float[] _window;  // Hann window

    private int _inWrite;
    private int _inRead;
    private int _outWrite;
    private int _outRead;

    private float _hopAccum;

    // ── Pitch state ───────────────────────────────────────────────────────
    private float _smoothedRatio = 1f;
    private float _detectedHz    = 0f;

    // ── Sweep state ───────────────────────────────────────────────────────
    private int   _sweepStep   = 0;
    private float _sweepTimer  = 0f;
    private int   _pingPongDir = 1;
    private volatile int _sweepIndex = 0;

    // ── Autocorrelation ───────────────────────────────────────────────────
    private const int AcSize = 4096;
    private const int MinPer = 20;
    private const int MaxPer = 800;
    private float[] _acBuf;
    private int     _acWrite;

    private int  _sampleRate;
    private bool _ready;

    // ─────────────────────────────────────────────────────────────────────

    void Start()
    {
        _sampleRate = AudioSettings.outputSampleRate;

        _inBuf  = new float[BufSize];
        _outBuf = new float[BufSize];
        _window = new float[GrainSize];
        _acBuf  = new float[AcSize];

        for (int i = 0; i < GrainSize; i++)
            _window[i] = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (GrainSize - 1)));

        _outWrite = GrainSize;
        _outRead  = 0;
        _inRead   = 0;
        _inWrite  = GrainSize;

        _ready = true;
        Debug.Log($"AutotuneFilter ready. Mode={Mode}, TestSemitones={TestSemitones}");
    }

    // ── Sweep LFO (main thread) ───────────────────────────────────────────
    void Update()
    {
        if (!AutotuneEnabled || Mode != PitchMode.Sweep) return;

        int n = GetChordNoteCount();
        if (n == 0) return;

        _sweepTimer += Time.deltaTime;
        if (_sweepTimer >= SweepNoteDuration)
        {
            _sweepTimer = 0f;
            AdvanceSweep(n);
            _sweepIndex     = Mathf.Clamp(_sweepStep, 0, n - 1);
            _debugSweepStep = _sweepIndex;
        }
    }

    private void AdvanceSweep(int count)
    {
        if (count <= 1) { _sweepStep = 0; return; }
        switch (Shape)
        {
            case SweepShape.Forward:
                _sweepStep = (_sweepStep + 1) % count;
                break;
            case SweepShape.PingPong:
                _sweepStep += _pingPongDir;
                if (_sweepStep >= count - 1) { _sweepStep = count - 1; _pingPongDir = -1; }
                else if (_sweepStep <= 0)    { _sweepStep = 0;         _pingPongDir =  1; }
                break;
            case SweepShape.Random:
                int next = _sweepStep;
                while (next == _sweepStep && count > 1) next = Random.Range(0, count);
                _sweepStep = next;
                break;
        }
    }

    // ── DSP callback ──────────────────────────────────────────────────────
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_ready || !AutotuneEnabled) return;

        int frames = data.Length / channels;

        float targetRatio = ComputeTargetRatio();
        _debugTargetRatio = targetRatio;

        float smoothSamples = Mathf.Max(1f, PitchSmoothing * _sampleRate);
        float k = 1f - Mathf.Exp(-(float)frames / smoothSamples);
        _smoothedRatio = Mathf.Lerp(_smoothedRatio, targetRatio, k);
        _smoothedRatio = Mathf.Clamp(_smoothedRatio, 0.25f, 4.0f);
        _debugSmoothedRatio = _smoothedRatio;

        for (int f = 0; f < frames; f++)
        {
            float input = 0f;
            for (int c = 0; c < channels; c++) input += data[f * channels + c];
            input /= channels;

            _inBuf[_inWrite & (BufSize - 1)] = input;
            _inWrite++;

            _acBuf[_acWrite & (AcSize - 1)] = input;
            _acWrite++;

            _hopAccum += _smoothedRatio;
            if (_hopAccum >= GrainSize)
            {
                _hopAccum -= GrainSize;
                WriteGrain();
            }

            int   ri   = _outRead & (BufSize - 1);
            float out_ = _outBuf[ri];
            _outBuf[ri] = 0f;
            _outRead++;

            for (int c = 0; c < channels; c++)
                data[f * channels + c] = out_;
        }

        if ((_acWrite & (AcSize - 1)) < frames) DetectPitch();
    }

    // ── Grain writing ─────────────────────────────────────────────────────
    private void WriteGrain()
    {
        int inputHop = Mathf.Max(1, Mathf.RoundToInt(GrainSize / _smoothedRatio));

        for (int i = 0; i < GrainSize; i++)
        {
            int   idx = (_inRead + i) & (BufSize - 1);
            float s   = _inBuf[idx] * _window[i];

            int outIdx = (_outWrite + i) & (BufSize - 1);
            _outBuf[outIdx] += s;
        }

        _inRead  = (_inRead  + inputHop)         & (BufSize - 1);
        _outWrite = (_outWrite + GrainSize / 2)  & (BufSize - 1);
    }

    // ── Target ratio ──────────────────────────────────────────────────────
    private float ComputeTargetRatio()
    {
        switch (Mode)
        {
            case PitchMode.Test:
                return Mathf.Pow(2f, TestSemitones / 12f);

            case PitchMode.Forced:
            {
                float hz = GetChordNoteHz(TargetIntervalIndex);
                if (hz <= 0f) return 1f;
                float ref_ = _detectedHz > 50f ? _detectedHz : 220f;
                return Mathf.Exp(Mathf.Log(FoldToVocalRange(hz) / ref_) * CorrectionStrength);
            }

            case PitchMode.Sweep:
            {
                float hz = GetChordNoteHz(_sweepIndex);
                if (hz <= 0f) return 1f;
                float ref_ = _detectedHz > 50f ? _detectedHz : 220f;
                return Mathf.Exp(Mathf.Log(FoldToVocalRange(hz) / ref_) * CorrectionStrength);
            }

            case PitchMode.WordAligned:
            {
                if (ChordSource == null) return 1f;
                float[] freqs = ChordSource.CurrentChordFrequencies;
                if (freqs == null || freqs.Length == 0) return 1f;
                int idx = Mathf.Clamp(ChordSource.WordNoteIndex, 0, freqs.Length - 1);
                float ref_ = _detectedHz > 50f ? _detectedHz : 220f;
                return Mathf.Exp(Mathf.Log(FoldToVocalRange(freqs[idx]) / ref_) * CorrectionStrength);
            }

            case PitchMode.Detected:
            {
                if (_detectedHz < 50f) return 1f;
                float[] freqs = GetNearestChordFreqs(_detectedHz);
                if (freqs == null || freqs.Length == 0) return 1f;
                float tgt  = freqs[0];
                float minC = float.MaxValue;
                foreach (float cf in freqs)
                {
                    float cents = Mathf.Abs(1200f * Mathf.Log(cf / _detectedHz) / Mathf.Log(2f));
                    if (cents < minC) { minC = cents; tgt = cf; }
                }
                return Mathf.Exp(Mathf.Log(tgt / _detectedHz) * CorrectionStrength);
            }

            default: return 1f;
        }
    }

    // ── Chord source helpers ──────────────────────────────────────────────

    // Returns the Hz of chord tone at the given index, preferring ChordSource over Carrier.
    private float GetChordNoteHz(int index)
    {
        if (ChordSource != null)
        {
            float[] freqs = ChordSource.CurrentChordFrequencies;
            if (freqs != null && freqs.Length > 0)
                return freqs[Mathf.Clamp(index, 0, freqs.Length - 1)];
        }
        if (Carrier != null && Carrier.chordIntervals.Length > 0)
        {
            int idx = Mathf.Clamp(index, 0, Carrier.chordIntervals.Length - 1);
            return CarrierSynth.MidiToHz(Carrier.rootMidi + Carrier.chordIntervals[idx]);
        }
        return 0f;
    }

    // How many chord tones are available from whichever source is active.
    private int GetChordNoteCount()
    {
        if (ChordSource != null)
        {
            float[] freqs = ChordSource.CurrentChordFrequencies;
            return freqs != null ? freqs.Length : 0;
        }
        return Carrier != null ? Carrier.chordIntervals.Length : 0;
    }

    // Builds a frequency array transposed to sit near refHz, for Detected-mode snapping.
    private float[] GetNearestChordFreqs(float refHz)
    {
        float[] baseFreqs;

        if (ChordSource != null && ChordSource.CurrentChordFrequencies != null
            && ChordSource.CurrentChordFrequencies.Length > 0)
        {
            baseFreqs = ChordSource.CurrentChordFrequencies;
        }
        else if (Carrier != null)
        {
            var intervals = Carrier.chordIntervals;
            baseFreqs = new float[intervals.Length];
            for (int i = 0; i < intervals.Length; i++)
                baseFreqs[i] = CarrierSynth.MidiToHz(Carrier.rootMidi + intervals[i]);
        }
        else return null;

        var result = new float[baseFreqs.Length];
        for (int i = 0; i < baseFreqs.Length; i++)
        {
            float f = baseFreqs[i];
            while (f < refHz * 0.7071f) f *= 2f;
            while (f > refHz * 1.4142f) f *= 0.5f;
            result[i] = f;
        }
        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private float FoldToVocalRange(float hz)
    {
        while (hz < 150f) hz *= 2f;
        while (hz > 450f) hz *= 0.5f;
        return hz;
    }

    // ── Pitch detection ───────────────────────────────────────────────────
    private void DetectPitch()
    {
        float rms = 0f;
        for (int i = 0; i < AcSize; i++) rms += _acBuf[i] * _acBuf[i];
        rms = Mathf.Sqrt(rms / AcSize);
        if (rms < 0.008f) { _detectedHz = 0f; _debugDetectedHz = 0f; return; }

        float best = -1f; int bestP = MinPer;
        for (int p = MinPer; p <= MaxPer; p++)
        {
            float c = 0f; int n = AcSize - p;
            for (int i = 0; i < n; i++)
                c += _acBuf[i & (AcSize - 1)] * _acBuf[(i + p) & (AcSize - 1)];
            c /= n;
            if (c > best) { best = c; bestP = p; }
        }
        _detectedHz      = best > 0.05f ? (float)_sampleRate / bestP : 0f;
        _debugDetectedHz = _detectedHz;
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void EnableAutotune()  => AutotuneEnabled = true;
    public void DisableAutotune() => AutotuneEnabled = false;
    public void ToggleAutotune()  => AutotuneEnabled = !AutotuneEnabled;
    public float DetectedHz => _detectedHz;
}
