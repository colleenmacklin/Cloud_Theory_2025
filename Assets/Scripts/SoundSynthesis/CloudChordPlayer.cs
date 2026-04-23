using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Crosstales.RTVoice;
using Crosstales.RTVoice.Model;
using Synthic;

/// <summary>
/// Sample-based chord player for the cloud scene.
/// Drops in as a stable replacement for CloudChordController + PolyphonicGenerator.
/// No native memory, no Burst — uses four AudioSources pitched to chord tones.
///
/// Setup:
///   1. Add this component to a persistent GameObject.
///   2. Assign any looping AudioClip to Clip.
///   3. Set SampleBaseMidi to the note the clip is tuned to (default 69 = A4 = 440 Hz).
///      A sine-wave clip or soft pad at A4 is the easiest starting point.
///   4. Fill CloudChords with one entry per cloud shape name.
///   5. Assign this component to AutotuneFilter.ChordSource.
///
/// The component auto-creates four child AudioSources in Awake. Route them to
/// a Unity Audio Mixer group via OutputMixer to add reverb, chorus, etc.
/// </summary>
public class CloudChordPlayer : MonoBehaviour
{
    // ── Types ─────────────────────────────────────────────────────────────
    public enum TetraType
    {
        Major7,      // 0-4-7-11  bright and lush
        Minor7,      // 0-3-7-10  melancholic
        Dominant7,   // 0-4-7-10  bluesy tension
        MinorMaj7,   // 0-3-7-11  bittersweet
        HalfDim7,    // 0-3-6-10  suspenseful
        FullDim7,    // 0-3-6-9   dark
        DomSus4,     // 0-5-7-10  suspended / open
        MajAdd9,     // 0-4-7-14  airy
    }

    public enum Inversion { Root, First, Second, Third }
    public enum PlayStyle  { Block, Toggle, Arpeggio }

    // ── Per-cloud chord mapping ───────────────────────────────────────────
    [System.Serializable]
    public class CloudChordEntry
    {
        [Tooltip("Cloud name from Actions.ChooseCloud. Case-insensitive; substring match used as fallback.")]
        public string cloudName = "";

        [Range(36, 84)]
        [Tooltip("Root note as MIDI (60=C4, 62=D4, 64=E4, 65=F4, 67=G4, 69=A4)")]
        public int rootMidi = 60;

        public TetraType chordType = TetraType.Major7;
        public Inversion inversion = Inversion.Root;
        public PlayStyle playStyle = PlayStyle.Block;
    }

    // ── Inspector — sound source ──────────────────────────────────────────
    [Header("Sound Source")]
    [Tooltip("Any audio clip. Looping tonal pads or single-cycle waveforms work best.")]
    public AudioClip Clip;

    [Range(36, 84)]
    [Tooltip("The MIDI note the clip is tuned to. 69 = A4 = 440 Hz. " +
             "Set this to match your sample so pitch ratios are accurate.")]
    public int SampleBaseMidi = 69;

    [Range(0f, 1f)]
    public float MasterVolume = 0.7f;

    [Tooltip("Optional Audio Mixer Group for routing reverb, chorus, EQ, etc.")]
    public AudioMixerGroup OutputMixer;

    // ── Inspector — envelope ──────────────────────────────────────────────
    [Header("Envelope")]
    [Range(0f, 2f)] public float Attack  = 0.05f;
    [Range(0f, 2f)] public float Release = 0.4f;

    // ── Inspector — LFO : Vibrato ─────────────────────────────────────────
    [Header("LFO — Vibrato (pitch)")]
    public bool  VibratoEnabled = true;
    [Range(0f,  10f)] public float VibratoRate  = 5f;    // cycles per second
    [Range(0f, 0.1f)] public float VibratoDepth = 0.012f; // pitch ratio swing

    // ── Inspector — LFO : Tremolo ─────────────────────────────────────────
    [Header("LFO — Tremolo (volume)")]
    public bool  TremoloEnabled = false;
    [Range(0f,  10f)] public float TremoloRate  = 4f;
    [Range(0f,  1f)]  public float TremoloDepth = 0.25f;

    // ── Inspector — chord list ────────────────────────────────────────────
    [Header("Cloud → Chord Mapping")]
    public List<CloudChordEntry> CloudChords = new();

    [Header("Default Chord")]
    [Range(36, 84)] public int DefaultRootMidi  = 60;
    public TetraType DefaultChordType = TetraType.Major7;
    public Inversion DefaultInversion = Inversion.Root;
    public PlayStyle DefaultPlayStyle = PlayStyle.Block;

    [Header("Arpeggio")]
    [Range(0.05f, 2f)] public float ArpeggioNoteDuration = 0.3f;
    public bool ArpeggioLoop = true;

    [Header("Debug (read-only)")]
    [SerializeField] private string _activeChordLabel;
    [SerializeField] private int    _wordNoteIndex;

    // ── Public state (read by AutotuneFilter) ─────────────────────────────
    /// <summary>Hz of each note in the currently playing chord.</summary>
    public float[] CurrentChordFrequencies { get; private set; } = System.Array.Empty<float>();

    /// <summary>Which chord tone AutotuneFilter should target for the current spoken word.</summary>
    public int WordNoteIndex => _wordNoteIndex;

    // ── Voice ─────────────────────────────────────────────────────────────
    private enum VoiceState { Off, Attack, Sustain, Release }

    private class Voice
    {
        public AudioSource source;
        public float       targetPitch          = 1f;
        public float       currentVolume        = 0f;
        public VoiceState  state                = VoiceState.Off;
        public float       envTimer             = 0f;
        public float       releaseStartVolume   = 0f;
    }

    private const int VoiceCount = 4;
    private Voice[] _voices;

    // ── LFO state ─────────────────────────────────────────────────────────
    private float _vibratoPhase = 0f;
    private float _tremoloPhase = 0f;

    // ── Arpeggio ──────────────────────────────────────────────────────────
    private Coroutine _arpeggioRoutine;

    // ── Cached sample base frequency ─────────────────────────────────────
    private float _sampleBaseHz;

    // ── Interval tables ───────────────────────────────────────────────────
    private static readonly Dictionary<TetraType, int[]> BaseIntervals = new()
    {
        { TetraType.Major7,    new[] { 0, 4, 7, 11 } },
        { TetraType.Minor7,    new[] { 0, 3, 7, 10 } },
        { TetraType.Dominant7, new[] { 0, 4, 7, 10 } },
        { TetraType.MinorMaj7, new[] { 0, 3, 7, 11 } },
        { TetraType.HalfDim7,  new[] { 0, 3, 6, 10 } },
        { TetraType.FullDim7,  new[] { 0, 3, 6,  9 } },
        { TetraType.DomSus4,   new[] { 0, 5, 7, 10 } },
        { TetraType.MajAdd9,   new[] { 0, 4, 7, 14 } },
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _sampleBaseHz = MidiToHz(SampleBaseMidi);
        BuildVoices();
    }

    private void OnEnable()
    {
        Actions.ChooseCloud += OnChooseCloud;
        if (Speaker.Instance != null)
            Speaker.Instance.OnSpeakCurrentWordString += OnWordSpoken;
    }

    private void OnDisable()
    {
        Actions.ChooseCloud -= OnChooseCloud;
        if (Speaker.Instance != null)
            Speaker.Instance.OnSpeakCurrentWordString -= OnWordSpoken;
    }

    private void OnDestroy()
    {
        StopArpeggio();
    }

    private void Start()
    {
        PlayChord(DefaultRootMidi, DefaultChordType, DefaultInversion, DefaultPlayStyle);
    }

    private void Update()
    {
        // Allow live tweaking of sample base note in the Inspector.
        _sampleBaseHz = MidiToHz(SampleBaseMidi);

        // Advance LFO phases.
        float tau = Mathf.PI * 2f;
        _vibratoPhase = (_vibratoPhase + VibratoRate * Time.deltaTime * tau) % tau;
        _tremoloPhase = (_tremoloPhase + TremoloRate * Time.deltaTime * tau) % tau;

        float vibratoMod = VibratoEnabled
            ? 1f + Mathf.Sin(_vibratoPhase) * VibratoDepth
            : 1f;
        // Tremolo swings between (1 - depth) and 1.0
        float tremoloMod = TremoloEnabled
            ? 1f - TremoloDepth * (0.5f + 0.5f * Mathf.Sin(_tremoloPhase))
            : 1f;

        for (int i = 0; i < VoiceCount; i++)
        {
            var v = _voices[i];

            // Advance envelope timer.
            if (v.state == VoiceState.Attack || v.state == VoiceState.Release)
                v.envTimer += Time.deltaTime;

            switch (v.state)
            {
                case VoiceState.Attack:
                    v.currentVolume = Mathf.Clamp01(v.envTimer / Mathf.Max(Attack, 0.001f));
                    if (v.envTimer >= Attack)
                    {
                        v.currentVolume = 1f;
                        v.state = VoiceState.Sustain;
                    }
                    break;

                case VoiceState.Release:
                    v.currentVolume = Mathf.Lerp(v.releaseStartVolume, 0f,
                        v.envTimer / Mathf.Max(Release, 0.001f));
                    if (v.envTimer >= Release)
                    {
                        v.currentVolume = 0f;
                        v.state = VoiceState.Off;
                        v.source.Stop();
                    }
                    break;
            }

            // Write final pitch and volume to AudioSource.
            v.source.pitch  = Mathf.Clamp(v.targetPitch * vibratoMod, 0.01f, 4f);
            v.source.volume = v.currentVolume * tremoloMod * MasterVolume;
        }
    }

    // ── Voice helpers ─────────────────────────────────────────────────────

    // Creates four child AudioSources. Called once in Awake.
    private void BuildVoices()
    {
        _voices = new Voice[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var go  = new GameObject($"ChordVoice_{i}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop         = true;
            src.playOnAwake  = false;
            src.volume       = 0f;
            src.spatialBlend = 0f;  // fully 2D
            if (OutputMixer != null) src.outputAudioMixerGroup = OutputMixer;

            _voices[i] = new Voice { source = src };
        }
    }

    // Starts or restarts a voice at the given frequency.
    private void NoteOn(int index, float frequency)
    {
        if (Clip == null) return;
        var v = _voices[index];

        v.targetPitch = frequency / Mathf.Max(_sampleBaseHz, 1f);
        v.source.pitch = Mathf.Clamp(v.targetPitch, 0.01f, 4f);

        // Restart playback if the clip changed or the source has stopped.
        if (v.source.clip != Clip || !v.source.isPlaying)
        {
            v.source.clip = Clip;
            v.source.Stop();
            v.source.Play();
        }

        v.currentVolume = 0f;
        v.state    = VoiceState.Attack;
        v.envTimer = 0f;
    }

    // Starts a fade-out on a voice.
    private void NoteOff(int index)
    {
        var v = _voices[index];
        if (v.state == VoiceState.Off) return;
        v.releaseStartVolume = v.currentVolume;
        v.state    = VoiceState.Release;
        v.envTimer = 0f;
    }

    private void AllNotesOff()
    {
        for (int i = 0; i < VoiceCount; i++) NoteOff(i);
    }

    // ── Chord player ──────────────────────────────────────────────────────
    private void PlayChord(int rootMidi, TetraType type, Inversion inv, PlayStyle style)
    {
        if (Clip == null)
        {
            Debug.LogWarning("CloudChordPlayer: no AudioClip assigned — drag a clip into the Clip field.");
            return;
        }

        StopArpeggio();
        AllNotesOff();

        int[]   intervals = BuildIntervals(type, inv);
        float[] freqs     = IntervalsToFreqs(rootMidi, intervals);

        CurrentChordFrequencies = freqs;
        _wordNoteIndex          = 0;
        _activeChordLabel       = $"{(Note.Name)(rootMidi % 12)} {type} inv{(int)inv} [{style}]";

        switch (style)
        {
            case PlayStyle.Block:
                for (int i = 0; i < freqs.Length; i++) NoteOn(i, freqs[i]);
                break;

            case PlayStyle.Toggle:
                // Force all voices back to silence so attack restarts cleanly.
                for (int i = 0; i < VoiceCount; i++) _voices[i].currentVolume = 0f;
                for (int i = 0; i < freqs.Length; i++) NoteOn(i, freqs[i]);
                break;

            case PlayStyle.Arpeggio:
                _arpeggioRoutine = StartCoroutine(ArpeggioRoutine(freqs));
                break;
        }
    }

    private IEnumerator ArpeggioRoutine(float[] freqs)
    {
        int prev = -1;
        do
        {
            for (int i = 0; i < freqs.Length; i++)
            {
                if (prev >= 0) NoteOff(prev);
                NoteOn(i, freqs[i]);
                prev = i;
                yield return new WaitForSeconds(ArpeggioNoteDuration);
            }
        }
        while (ArpeggioLoop);
    }

    private void StopArpeggio()
    {
        if (_arpeggioRoutine == null) return;
        StopCoroutine(_arpeggioRoutine);
        _arpeggioRoutine = null;
    }

    // ── Event handlers ────────────────────────────────────────────────────
    private void OnChooseCloud(string cloudName)
    {
        CloudChordEntry entry = FindEntry(cloudName);
        if (entry != null)
            PlayChord(entry.rootMidi, entry.chordType, entry.inversion, entry.playStyle);
        else
            PlayChord(DefaultRootMidi, DefaultChordType, DefaultInversion, DefaultPlayStyle);
    }

    private void OnWordSpoken(Wrapper wrapper, string word)
    {
        AdvanceWordNote();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Advance the autotune target to the next chord tone.</summary>
    public void AdvanceWordNote()
    {
        if (CurrentChordFrequencies.Length == 0) return;
        _wordNoteIndex = (_wordNoteIndex + 1) % CurrentChordFrequencies.Length;
    }

    /// <summary>Reset to root note — call at the start of each sentence.</summary>
    public void ResetWordNote() => _wordNoteIndex = 0;

    /// <summary>Swap play style at runtime and immediately retrigger the current chord.</summary>
    public void SetPlayStyle(PlayStyle style)
    {
        DefaultPlayStyle = style;
        if (CurrentChordFrequencies.Length == 0) return;
        StopArpeggio();
        AllNotesOff();
        switch (style)
        {
            case PlayStyle.Block:
            case PlayStyle.Toggle:
                if (style == PlayStyle.Toggle)
                    for (int i = 0; i < VoiceCount; i++) _voices[i].currentVolume = 0f;
                for (int i = 0; i < CurrentChordFrequencies.Length; i++)
                    NoteOn(i, CurrentChordFrequencies[i]);
                break;
            case PlayStyle.Arpeggio:
                _arpeggioRoutine = StartCoroutine(ArpeggioRoutine(CurrentChordFrequencies));
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private CloudChordEntry FindEntry(string cloudName)
    {
        foreach (var e in CloudChords)
            if (string.Equals(e.cloudName, cloudName, System.StringComparison.OrdinalIgnoreCase))
                return e;
        foreach (var e in CloudChords)
            if (!string.IsNullOrEmpty(e.cloudName) &&
                (cloudName.IndexOf(e.cloudName, System.StringComparison.OrdinalIgnoreCase) >= 0
                 || e.cloudName.IndexOf(cloudName, System.StringComparison.OrdinalIgnoreCase) >= 0))
                return e;
        return null;
    }

    // Rotates the interval array for inversions:
    // Root: [0,4,7,11]  1st: [4,7,11,12]  2nd: [7,11,12,16]  3rd: [11,12,16,19]
    private static int[] BuildIntervals(TetraType type, Inversion inv)
    {
        int[] b = (int[])BaseIntervals[type].Clone();
        for (int i = 0; i < (int)inv; i++)
        {
            int raised = b[0] + 12;
            for (int j = 0; j < b.Length - 1; j++) b[j] = b[j + 1];
            b[b.Length - 1] = raised;
        }
        return b;
    }

    private static float[] IntervalsToFreqs(int rootMidi, int[] intervals)
    {
        float[] freqs = new float[intervals.Length];
        for (int i = 0; i < intervals.Length; i++)
            freqs[i] = MidiToHz(rootMidi + intervals[i]);
        return freqs;
    }

    private static float MidiToHz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
}
