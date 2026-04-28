using UnityEngine;

/// <summary>
/// Vocoder effect that runs directly on RTVoice's SpeakerAudio AudioSource.
///
/// Setup: Attach this script to the same GameObject as VoiceHandler's
/// SpeakerAudio AudioSource. Assign the CarrierSynth reference in the Inspector.
///
/// Signal flow:
///   RTVoice PCM → 16-band bandpass analysis → envelope per band
///   CarrierSynth ring buffer → 16-band bandpass → multiply by envelopes
///   → summed output replaces the original voice audio
/// </summary>
public class VocoderFilter : MonoBehaviour
{
    // ── Public settings ─────────────────────────────────────────────────
    [Header("References")]
    public CarrierSynth Carrier;

    [Header("Vocoder On/Off")]
    [Tooltip("Toggle the vocoder effect. When off, RTVoice plays normally.")]
    public bool VocoderEnabled = true;

    [Header("Blend")]
    [Range(0f, 1f)]
    [Tooltip("0 = fully vocoded, 1 = fully dry voice (useful for crossfades)")]
    public float DryWet = 1f; // 1 = fully vocoded by default

    [Header("Envelope Follower")]
    [Range(0.0005f, 0.02f)]
    [Tooltip("Attack time in seconds. Keep short (0.001–0.005) for crisp consonants.")]
    public float EnvAttack  = 0.002f;

    [Range(0.01f, 0.2f)]
    [Tooltip("Release time. 0.03–0.06 = intelligible. Higher = dreamier but muddier.")]
    public float EnvRelease = 0.04f;

    [Header("Band Gain")]
    [Range(0.5f, 8f)]
    [Tooltip("Scales the overall vocoder output level.")]
    public float OutputGain = 3.5f;

    [Header("Presence Boost")]
    [Range(1f, 4f)]
    [Tooltip("Extra gain on mid bands (800–3500 Hz) where speech intelligibility lives.")]
    public float MidBoost = 1.8f;

    // ── Filter bank ──────────────────────────────────────────────────────
    // 16 bands, densely packed in the speech range (300 Hz – 4 kHz).
    // Intelligibility requires resolution where formants live, not bass.
    private const int NumBands = 16;
    private static readonly float[] BandCentres = {
        150f, 250f, 350f, 500f, 700f, 900f, 1150f, 1450f,
        1800f, 2200f, 2700f, 3300f, 4000f, 5000f, 6300f, 8000f
    };
    // Per-band gain — boost the formant region (bands 3–11, roughly 500–3300 Hz)
    private static readonly float[] BandBoost = {
        0.6f, 0.8f, 1.0f, 1.2f, 1.4f, 1.6f, 1.8f, 1.8f,
        1.8f, 1.6f, 1.4f, 1.2f, 1.0f, 0.8f, 0.6f, 0.5f
    };
    private const float BandQ = 2.8f; // tighter = better separation between phonemes

    // IIR state arrays — allocated in Start() to match NumBands
    private float[] _voiceZ1;
    private float[] _voiceZ2;
    private float[] _carrZ1;
    private float[] _carrZ2;

    // Envelope followers per band
    private float[] _envelope;

    // Pre-computed filter coefficients per band [b0, b2, a1, a2]
    private float[,] _coeffs;

    private int _sampleRate;
    private bool _initialised = false;

    // Carrier read position
    private int _carrReadPos = 0;

    // ── Initialisation ───────────────────────────────────────────────────
    void Start()
    {
        _sampleRate = AudioSettings.outputSampleRate;

        _voiceZ1  = new float[NumBands];
        _voiceZ2  = new float[NumBands];
        _carrZ1   = new float[NumBands];
        _carrZ2   = new float[NumBands];
        _envelope = new float[NumBands];
        _coeffs   = new float[NumBands, 4];

        PrecomputeCoefficients();
        _carrReadPos = CarrierSynth.WritePos;
        _initialised = true;
    }

    void PrecomputeCoefficients()
    {
        for (int b = 0; b < NumBands; b++)
        {
            float f0 = BandCentres[b];
            float w0 = 2f * Mathf.PI * f0 / _sampleRate;
            float cosW = Mathf.Cos(w0);
            float sinW = Mathf.Sin(w0);
            float alpha = sinW / (2f * BandQ);

            // Standard Audio EQ Cookbook bandpass (constant 0 dB peak gain)
            float b0 =  alpha;
            float b1 =  0f;
            float b2 = -alpha;
            float a0 =  1f + alpha;
            float a1 = -2f * cosW;
            float a2 =  1f - alpha;

            _coeffs[b, 0] = b0 / a0;
            _coeffs[b, 1] = b2 / a0;  // b2/a0 (b1 is always 0)
            _coeffs[b, 2] = a1 / a0;
            _coeffs[b, 3] = a2 / a0;
        }
    }

    // ── DSP callback ─────────────────────────────────────────────────────
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_initialised || Carrier == null) return;

        // If vocoder is disabled, pass the audio through untouched.
        if (!VocoderEnabled) return;

        int frames = data.Length / channels;

        // Pre-compute envelope follower coefficients from time constants.
        float attCoef = Mathf.Exp(-1f / (_sampleRate * EnvAttack));
        float relCoef = Mathf.Exp(-1f / (_sampleRate * EnvRelease));

        for (int f = 0; f < frames; f++)
        {
            // ── 1. Read mono voice sample (average all input channels) ──
            float voiceIn = 0f;
            for (int c = 0; c < channels; c++)
                voiceIn += data[f * channels + c];
            voiceIn /= channels;

            // ── 2. Read carrier sample from ring buffer ──────────────────
            float carrIn = CarrierSynth.RingBuffer[_carrReadPos % CarrierSynth.BufferSize];
            _carrReadPos = (_carrReadPos + 1) % CarrierSynth.BufferSize;

            // ── 3. Per-band analysis + synthesis ─────────────────────────
            float vocodedOut = 0f;

            for (int b = 0; b < NumBands; b++)
            {
                float b0 = _coeffs[b, 0];
                float b2 = _coeffs[b, 1];
                float a1 = _coeffs[b, 2];
                float a2 = _coeffs[b, 3];

                // --- Analyse voice band ---
                float voiceBand = b0 * voiceIn + _voiceZ1[b];
                _voiceZ1[b] = /* b1*voiceIn */ -a1 * voiceBand + _voiceZ2[b];  // b1=0
                _voiceZ2[b] = b2 * voiceIn - a2 * voiceBand;

                // --- Envelope follower on voice band ---
                float abs = Mathf.Abs(voiceBand);
                float coef = abs > _envelope[b] ? attCoef : relCoef;
                _envelope[b] = _envelope[b] * coef + (1f - coef) * abs;

                // --- Filter carrier band ---
                float carrBand = b0 * carrIn + _carrZ1[b];
                _carrZ1[b] = -a1 * carrBand + _carrZ2[b];
                _carrZ2[b] = b2 * carrIn - a2 * carrBand;

                // --- Multiply carrier band by voice envelope + per-band boost ---
                vocodedOut += carrBand * _envelope[b] * BandBoost[b];
            }

            vocodedOut *= OutputGain;

            // ── 4. Dry/wet blend and write output ─────────────────────────
            // DryWet = 1 → fully vocoded; DryWet = 0 → fully dry voice
            float finalSample = Mathf.Lerp(voiceIn, vocodedOut, DryWet);

            for (int c = 0; c < channels; c++)
                data[f * channels + c] = finalSample;
        }
    }

    // ── Runtime control helpers ───────────────────────────────────────────

    /// <summary>Enable the vocoder and sync carrier read position.</summary>
    public void EnableVocoder()
    {
        // Re-sync read head to avoid stale buffer content.
        _carrReadPos = CarrierSynth.WritePos;
        VocoderEnabled = true;
    }

    /// <summary>Disable the vocoder — RTVoice plays as normal.</summary>
    public void DisableVocoder()
    {
        VocoderEnabled = false;
        // Reset envelope state so there's no bleed when re-enabling.
        if (_envelope != null)
            System.Array.Clear(_envelope, 0, NumBands);
    }

    /// <summary>Toggle vocoder on/off.</summary>
    public void ToggleVocoder()
    {
        if (VocoderEnabled) DisableVocoder();
        else EnableVocoder();
    }
}
