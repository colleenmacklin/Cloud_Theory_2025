using UnityEngine;

/// <summary>
/// Generates synthesizer oscillator audio into a shared ring buffer.
/// The carrier AudioSource volume must be set to 0 — sound is only heard
/// through the VocoderFilter on the RTVoice SpeakerAudio source.
///
/// Setup: Attach to a new empty GameObject. Add an AudioSource to the same
/// GameObject with Volume = 0 and Play On Awake = true.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CarrierSynth : MonoBehaviour
{
    // ── Carrier chord ───────────────────────────────────────────────────
    [Header("Carrier Chord")]
    [Tooltip("Base note as MIDI number (60 = middle C, 69 = A4)")]
    public int rootMidi = 60;

    [Tooltip("Chord intervals in semitones above root (e.g. major 7th = 0,4,7,11)")]
    public int[] chordIntervals = { 0, 7, 12, 19 }; // open fifth stack — dreamy

    [Range(0f, 1f)]
    [Tooltip("Oscillator mix: 0 = pure sine, 1 = pure sawtooth")]
    public float sawMix = 0.55f;

    [Range(0f, 0.5f)]
    [Tooltip("Each oscillator is detuned slightly for shimmer")]
    public float detuneAmount = 0.008f; // in semitone fraction

    // ── Carrier buffer (shared with VocoderFilter) ──────────────────────
    // Lock-free single-producer / single-consumer ring buffer.
    public const int BufferSize = 8192;
    public static readonly float[] RingBuffer = new float[BufferSize];
    public static volatile int WritePos = 0;

    // ── Internal state ───────────────────────────────────────────────────
    private float[] _phases;   // one phase accumulator per oscillator voice
    private float[] _freqs;    // current frequency per voice (Hz)
    private int _sampleRate;
    private bool _initialised = false;

    // ── Active flag — set from VocoderController ─────────────────────────
    [HideInInspector] public bool IsActive = false;

    // ────────────────────────────────────────────────────────────────────

    void Start()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        RebuildVoices();

        // Ensure the carrier AudioSource is silent — we only want the
        // vocoder output to be heard, never the raw oscillator.
        var src = GetComponent<AudioSource>();
        src.volume = 0f;
        src.loop = true;
        if (!src.isPlaying) src.Play();

        _initialised = true;
    }

    /// <summary>
    /// Recalculate voice frequencies from the current rootMidi + chordIntervals.
    /// Call this whenever you change the chord at runtime.
    /// </summary>
    public void RebuildVoices()
    {
        int count = chordIntervals.Length;
        _phases = new float[count];
        _freqs  = new float[count];

        for (int i = 0; i < count; i++)
        {
            int midi = rootMidi + chordIntervals[i];
            _freqs[i] = MidiToHz(midi);
        }
    }

    /// <summary>
    /// Set the carrier root note and optionally a named chord type.
    /// Called from VocoderController when a game event changes the chord.
    /// </summary>
    public void SetChord(int midiRoot, int[] intervals = null)
    {
        rootMidi = midiRoot;
        if (intervals != null)
            chordIntervals = intervals;
        RebuildVoices();
    }

    // ── DSP callback ─────────────────────────────────────────────────────
    // Unity calls this on the audio thread. We generate samples whether or
    // not IsActive is true — VocoderFilter reads the buffer regardless,
    // and if the vocoder is disabled it simply passes through the voice.
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_initialised) return;

        int frames = data.Length / channels;

        for (int f = 0; f < frames; f++)
        {
            float sample = 0f;
            int voices = _freqs.Length;

            for (int v = 0; v < voices; v++)
            {
                // Slight per-voice detune alternates ±
                float detuneMult = 1f + detuneAmount * (v % 2 == 0 ? 1f : -1f);
                float freq = _freqs[v] * detuneMult;
                float phaseInc = freq / _sampleRate;

                _phases[v] = (_phases[v] + phaseInc) % 1f;
                float p = _phases[v];

                // Sine
                float sine = Mathf.Sin(p * 2f * Mathf.PI);

                // Sawtooth (aliased but intentionally warm at low frequencies)
                float saw = 2f * p - 1f;

                sample += Mathf.Lerp(sine, saw, sawMix);
            }

            // Normalise by voice count and write to ring buffer
            float normalised = sample / Mathf.Max(1, voices);

            int writeIdx = WritePos % BufferSize;
            RingBuffer[writeIdx] = normalised;
            WritePos = (WritePos + 1) % BufferSize;

            // Zero out the actual data array so the carrier AudioSource
            // produces no audible sound on its own.
            for (int c = 0; c < channels; c++)
                data[f * channels + c] = 0f;
        }
    }

    // ── Utility ──────────────────────────────────────────────────────────
    public static float MidiToHz(int midi)
    {
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
