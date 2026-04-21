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
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AutotuneFilter : MonoBehaviour
{
    public enum PitchMode  { Test, Forced, Sweep, Detected }
    public enum SweepShape { Forward, PingPong, Random }

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("References")]
    public CarrierSynth Carrier;

    [Header("On/Off")]
    public bool AutotuneEnabled = true;

    [Header("Pitch Mode")]
    [Tooltip("Test: manual semitone offset to verify shifter is working.\n" +
             "Forced: fixed chord interval.\n" +
             "Sweep: LFO through chord notes.\n" +
             "Detected: autocorrelation snap.")]
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

    [Header("Correction (Forced / Sweep / Detected)")]
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
    // Input ring: written at 1 sample/output sample (fixed rate)
    // Output ring: grains overlap-added with variable hop = GrainSize / ratio
    private const int GrainSize = 512;   // ~11.6ms @ 44100
    private const int BufSize   = 65536; // must be >> GrainSize * max_ratio

    private float[] _inBuf;     // input ring buffer
    private float[] _outBuf;    // output overlap-add buffer
    private float[] _window;    // Hann window

    private int _inWrite;       // input write head (integer, advances by 1 per sample)
    private int _inRead;        // input read head for grain extraction
    private int _outWrite;      // output write head (advances by scaled hop)
    private int _outRead;       // output read head (advances by 1 per sample)

    // Fractional output hop accumulator — we need non-integer hop sizes
    private float _hopAccum;    // accumulates fractional hop, triggers grain when >= GrainSize

    // ── Pitch state ───────────────────────────────────────────────────────
    private float _smoothedRatio = 1f;
    private float _detectedHz    = 0f;

    // ── Sweep state ───────────────────────────────────────────────────────
    private int   _sweepStep    = 0;
    private float _sweepTimer   = 0f;
    private int   _pingPongDir  = 1;
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

        // Prime output ring with one grain of latency headroom
        _outWrite = GrainSize;
        _outRead  = 0;
        _inRead   = 0;
        _inWrite  = GrainSize; // start inWrite ahead so first grain has data to read

        _ready = true;
        Debug.Log($"AutotuneFilter ready. Mode={Mode}, TestSemitones={TestSemitones}");
    }

    // ── Sweep LFO (main thread) ───────────────────────────────────────────
    void Update()
    {
        if (!AutotuneEnabled || Carrier == null || Mode != PitchMode.Sweep) return;
        int n = Carrier.chordIntervals.Length;
        if (n == 0) return;

        _sweepTimer += Time.deltaTime;
        if (_sweepTimer >= SweepNoteDuration)
        {
            _sweepTimer = 0f;
            AdvanceSweep(n);
            _sweepIndex     = Mathf.Clamp(_sweepStep, 0, n - 1);
            _debugSweepStep = _sweepIndex;
            Debug.Log($"AutotuneFilter sweep → step {_sweepIndex}, " +
                      $"interval +{Carrier.chordIntervals[_sweepIndex]} semitones");
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

        // Compute and smooth ratio once per buffer
        float targetRatio = ComputeTargetRatio();
        _debugTargetRatio = targetRatio;

        float smoothSamples = Mathf.Max(1f, PitchSmoothing * _sampleRate);
        float k = 1f - Mathf.Exp(-(float)frames / smoothSamples);
        _smoothedRatio = Mathf.Lerp(_smoothedRatio, targetRatio, k);
        _smoothedRatio = Mathf.Clamp(_smoothedRatio, 0.25f, 4.0f);
        _debugSmoothedRatio = _smoothedRatio;

        for (int f = 0; f < frames; f++)
        {
            // ── 1. Read mono input and store ──────────────────────────────
            float input = 0f;
            for (int c = 0; c < channels; c++) input += data[f * channels + c];
            input /= channels;

            _inBuf[_inWrite & (BufSize - 1)] = input;
            _inWrite++;

            _acBuf[_acWrite & (AcSize - 1)] = input;
            _acWrite++;

            // ── 2. Accumulate fractional hop ──────────────────────────────
            // Each output sample "costs" 1/_smoothedRatio input hops.
            // When _hopAccum reaches GrainSize we've consumed enough input
            // for a new grain to be placed.
            _hopAccum += _smoothedRatio;

            if (_hopAccum >= GrainSize)
            {
                _hopAccum -= GrainSize;
                WriteGrain();
            }

            // ── 3. Read from output ring ──────────────────────────────────
            int   ri   = _outRead & (BufSize - 1);
            float out_ = _outBuf[ri];
            _outBuf[ri] = 0f;   // clear after read — essential for OLA
            _outRead++;

            for (int c = 0; c < channels; c++)
                data[f * channels + c] = out_;
        }

        if ((_acWrite & (AcSize - 1)) < frames) DetectPitch();
    }

    // ── Grain writing ─────────────────────────────────────────────────────
    // Reads GrainSize samples from the input ring at the current _inRead pos,
    // applies the Hann window, and overlap-adds into the output ring.
    // _inRead advances by GrainSize / _smoothedRatio so that:
    //   ratio=2 → inRead advances by GrainSize/2 per grain
    //             → same input region used twice → pitch doubled
    //   ratio=0.5 → inRead advances by GrainSize*2 per grain
    //             → skips ahead in input → pitch halved
    private void WriteGrain()
    {
        // How far to advance the input read head after this grain.
        // This is the key: smaller step = more overlap = higher pitch.
        int inputHop = Mathf.Max(1, Mathf.RoundToInt(GrainSize / _smoothedRatio));

        for (int i = 0; i < GrainSize; i++)
        {
            int   idx = (_inRead + i) & (BufSize - 1);
            float s   = _inBuf[idx] * _window[i];

            int outIdx = (_outWrite + i) & (BufSize - 1);
            _outBuf[outIdx] += s;
        }

        // Advance input read head by inputHop
        _inRead = (_inRead + inputHop) & (BufSize - 1);

        // Advance output write head by a fixed half-grain (50% overlap)
        _outWrite = (_outWrite + GrainSize / 2) & (BufSize - 1);
    }

    // ── Target ratio ──────────────────────────────────────────────────────
    private float ComputeTargetRatio()
    {
        switch (Mode)
        {
            case PitchMode.Test:
                // 2^(semitones/12) — e.g. +7 semitones = 1.498
                return Mathf.Pow(2f, TestSemitones / 12f);

            case PitchMode.Forced:
            {
                if (Carrier == null) return 1f;
                int   idx = Mathf.Clamp(TargetIntervalIndex, 0, Carrier.chordIntervals.Length - 1);
                float hz  = FoldToVocalRange(CarrierSynth.MidiToHz(Carrier.rootMidi + Carrier.chordIntervals[idx]));
                float ref_ = _detectedHz > 50f ? _detectedHz : 220f;
                return Mathf.Exp(Mathf.Log(hz / ref_) * CorrectionStrength);
            }

            case PitchMode.Sweep:
            {
                if (Carrier == null) return 1f;
                int   idx = Mathf.Clamp(_sweepIndex, 0, Carrier.chordIntervals.Length - 1);
                float hz  = FoldToVocalRange(CarrierSynth.MidiToHz(Carrier.rootMidi + Carrier.chordIntervals[idx]));
                float ref_ = _detectedHz > 50f ? _detectedHz : 220f;
                return Mathf.Exp(Mathf.Log(hz / ref_) * CorrectionStrength);
            }

            case PitchMode.Detected:
            {
                if (Carrier == null || _detectedHz < 50f) return 1f;
                float[] freqs = GetChordFreqsNear(_detectedHz);
                float   tgt   = freqs[0];
                float   minC  = float.MaxValue;
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

    // ── Helpers ───────────────────────────────────────────────────────────
    private float FoldToVocalRange(float hz)
    {
        while (hz < 150f) hz *= 2f;
        while (hz > 450f) hz *= 0.5f;
        return hz;
    }

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
                c += _acBuf[i & (AcSize-1)] * _acBuf[(i + p) & (AcSize-1)];
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
