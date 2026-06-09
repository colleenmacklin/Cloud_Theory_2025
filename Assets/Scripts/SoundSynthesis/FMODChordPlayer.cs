using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;
using Crosstales.RTVoice;
using Crosstales.RTVoice.Model;
using Synthic;

/// <summary>
/// Replaces CloudChordPlayer for the FMOD audio pipeline.
/// Plays four sustained FMOD event instances per chord, one per chord voice.
/// The FMOD event must expose a "MidiNote" parameter (range 36–84) that your
/// FMOD Studio Multi-Instrument uses to select the correct hardware synth sample.
///
/// Setup:
///   1. Add this component to a persistent GameObject.
///   2. Set NoteEvent to your FMOD event path (e.g. event:/Instruments/SynthNote).
///   3. Fill CloudChords with one entry per cloud shape name.
///   4. Drag this component into AutotuneFilter → Chord Source Object.
///   5. Disable CloudChordPlayer, CarrierSynth, VocoderFilter, VocoderController.
/// </summary>
public class FMODChordPlayer : MonoBehaviour, IChordSource
{
    // ── Types (same as CloudChordPlayer) ──────────────────────────────────
    public enum TetraType
    {
        Major7,      // 0-4-7-11
        Minor7,      // 0-3-7-10
        Dominant7,   // 0-4-7-10
        MinorMaj7,   // 0-3-7-11
        HalfDim7,    // 0-3-6-10
        FullDim7,    // 0-3-6-9
        DomSus4,     // 0-5-7-10
        MajAdd9,     // 0-4-7-14
    }

    public enum Inversion     { Root, First, Second, Third }
    public enum PlayStyle     { Block, Toggle, Arpeggio }
    public enum ArpeggioOrder
    {
        Up,        // 1 2 3 4
        Down,      // 4 3 2 1
        RootHigh,  // 1 4 2 3
        HighRoot,  // 4 1 3 2
        SkipUp,    // 1 3 2 4
        SkipDown,  // 4 2 3 1
    }

    [System.Serializable]
    public class CloudChordEntry
    {
        [Tooltip("Cloud name from Actions.ChooseCloud. Case-insensitive; substring match used as fallback.")]
        public string cloudName = "";
        // C3=48  D3=50  E3=52  F3=53  G3=55  A3=57  B3=59  C4=60
        [Range(36, 84)] public int rootMidi = 60;
        public TetraType    chordType      = TetraType.Major7;
        public Inversion    inversion      = Inversion.Root;
        public PlayStyle    playStyle      = PlayStyle.Block;
        public ArpeggioOrder arpeggioOrder = ArpeggioOrder.Up;
    }

    // ── FMOD ──────────────────────────────────────────────────────────────
    [Header("FMOD")]
    [Tooltip("FMOD Studio event for a single synth note. Must have a MidiNote parameter.")]
    public EventReference NoteEvent;

    [Tooltip("FMOD parameter name that selects the note (integer MIDI number, 36–84).")]
    public string MidiNoteParam = "MidiNote";

    [Range(0f, 1f)] public float NoteVolume = 0.5f;

    // ── Chord mapping ──────────────────────────────────────────────────────
    [Header("Cloud → Chord Mapping")]
    public List<CloudChordEntry> CloudChords = new();

    [Header("Default Chord")]
    // C3=48  D3=50  E3=52  F3=53  G3=55  A3=57  B3=59  C4=60
    [Range(36, 84)] public int DefaultRootMidi = 69;
    public TetraType DefaultChordType = TetraType.Major7;
    public Inversion DefaultInversion = Inversion.Root;
    public PlayStyle DefaultPlayStyle = PlayStyle.Block;

    [Header("Arpeggio")]
    [Range(0.05f, 2f)] public float ArpeggioNoteDuration = 0.3f;
    public bool          ArpeggioLoop  = true;
    public ArpeggioOrder Order         = ArpeggioOrder.Up;

    [Header("Metronome Sync")]
    [Tooltip("When enabled, arpeggio steps are clocked by RhythmicMasterClock instead of ArpeggioNoteDuration.")]
    public bool SyncToMasterClock = false;
    [Tooltip("Quarter-note beats between arpeggio steps (1 = quarter, 2 = half, 4 = whole).")]
    [Min(1)] public int BeatsPerStep = 1;

    [Header("Debug")]
    [Tooltip("Click in play mode to fire one test note (middle C = 60) and confirm the FMOD event makes sound.")]
    public bool TestNoteNow = false;
    [Tooltip("Click in play mode to immediately play the chord defined by the Test fields below.")]
    public bool TestChordNow = false;
    // C3=48  D3=50  E3=52  F3=53  G3=55  A3=57  B3=59  C4=60
    [Range(36, 84)] public int      TestRootMidi  = 60;
    public TetraType                TestChordType = TetraType.Major7;
    public Inversion                TestInversion = Inversion.Root;
    public PlayStyle                TestPlayStyle = PlayStyle.Block;
    [SerializeField] private string _activeChordLabel;
    [SerializeField] private int    _wordNoteIndex;

    // ── IChordSource ───────────────────────────────────────────────────────
    public float[] CurrentChordFrequencies { get; private set; } = System.Array.Empty<float>();
    public int     WordNoteIndex => _wordNoteIndex;

    // ── Internal ───────────────────────────────────────────────────────────
    private const int VoiceCount = 4;
    private readonly EventInstance[] _voices = new EventInstance[VoiceCount];
    private Coroutine _arpeggioRoutine;

    // beat-synced arpeggio state
    private int[]  _syncedNotes;
    private int    _arpeggioStep;
    private int    _beatCount;

    private static readonly Dictionary<ArpeggioOrder, int[]> OrderSequences = new()
    {
        { ArpeggioOrder.Up,       new[] { 0, 1, 2, 3 } },
        { ArpeggioOrder.Down,     new[] { 3, 2, 1, 0 } },
        { ArpeggioOrder.RootHigh, new[] { 0, 3, 1, 2 } },
        { ArpeggioOrder.HighRoot, new[] { 3, 0, 2, 1 } },
        { ArpeggioOrder.SkipUp,   new[] { 0, 2, 1, 3 } },
        { ArpeggioOrder.SkipDown, new[] { 3, 1, 2, 0 } },
    };

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

    // ── Lifecycle ──────────────────────────────────────────────────────────
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

    private void Start()
    {
        PlayChord(DefaultRootMidi, DefaultChordType, DefaultInversion, DefaultPlayStyle);
        if (RhythmicMasterClock.Instance != null)
            RhythmicMasterClock.Instance.OnBeat += OnClockBeat;
    }

    private void Update()
    {
        if (TestNoteNow)
        {
            TestNoteNow = false;
            if (NoteEvent.IsNull) { Debug.LogWarning("[FMODChordPlayer] NoteEvent not assigned."); return; }
            Debug.Log("[FMODChordPlayer] Firing test note (MIDI 60 = middle C).");
            try
            {
                EventInstance t = RuntimeManager.CreateInstance(NoteEvent);
                t.setParameterByName(MidiNoteParam, 60);
                t.start();
                t.release();
            }
            catch (System.Exception e) { Debug.LogError($"[FMODChordPlayer] Test note failed: {e.Message}"); }
        }

        if (TestChordNow)
        {
            TestChordNow = false;
            Debug.Log($"[FMODChordPlayer] Test chord: root={TestRootMidi} {TestChordType} inv={TestInversion} [{TestPlayStyle}]");
            PlayChord(TestRootMidi, TestChordType, TestInversion, TestPlayStyle);
        }
    }

    private void OnDestroy()
    {
        if (RhythmicMasterClock.Instance != null)
            RhythmicMasterClock.Instance.OnBeat -= OnClockBeat;
        StopArpeggio();
        for (int i = 0; i < VoiceCount; i++)
            ReleaseVoice(i, immediate: true);
    }

    // ── FMOD voice management ──────────────────────────────────────────────
    private void NoteOn(int voiceIndex, int midiNote)
    {
        ReleaseVoice(voiceIndex, immediate: false);

        if (NoteEvent.IsNull)
        {
            Debug.LogWarning("[FMODChordPlayer] NoteEvent is not assigned.");
            return;
        }

        try
        {
            EventInstance inst = RuntimeManager.CreateInstance(NoteEvent);
            inst.setParameterByName(MidiNoteParam, midiNote);
            inst.setVolume(NoteVolume);
            inst.start();
            _voices[voiceIndex] = inst;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[FMODChordPlayer] Failed to create FMOD instance: {e.Message}");
        }
    }

    private void ReleaseVoice(int voiceIndex, bool immediate)
    {
        if (!_voices[voiceIndex].isValid()) return;
        var mode = immediate ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT;
        _voices[voiceIndex].stop(mode);
        _voices[voiceIndex].release();
        _voices[voiceIndex] = default;
    }

    private void AllNotesOff()
    {
        for (int i = 0; i < VoiceCount; i++) ReleaseVoice(i, immediate: false);
    }

    // ── Chord player ───────────────────────────────────────────────────────
    private void PlayChord(int rootMidi, TetraType type, Inversion inv, PlayStyle style)
    {
        StopArpeggio();
        AllNotesOff();

        int[] intervals = BuildIntervals(type, inv);
        int[] midiNotes = new int[intervals.Length];
        for (int i = 0; i < intervals.Length; i++)
            midiNotes[i] = rootMidi + intervals[i];

        CurrentChordFrequencies = MidiNotesToFreqs(midiNotes);
        _wordNoteIndex           = 0;
        _activeChordLabel        = $"{rootMidi % 12} {type} inv{(int)inv} [{style}]";

        switch (style)
        {
            case PlayStyle.Block:
            case PlayStyle.Toggle:
                for (int i = 0; i < midiNotes.Length && i < VoiceCount; i++)
                    NoteOn(i, midiNotes[i]);
                break;

            case PlayStyle.Arpeggio:
                if (SyncToMasterClock && RhythmicMasterClock.Instance != null)
                {
                    _syncedNotes  = midiNotes;
                    _arpeggioStep = 0;
                    _beatCount    = 0;
                    NoteOn(0, midiNotes[OrderSequences[Order][0]]);
                }
                else
                {
                    _arpeggioRoutine = StartCoroutine(ArpeggioRoutine(midiNotes));
                }
                break;
        }
    }

    private IEnumerator ArpeggioRoutine(int[] midiNotes)
    {
        int[] seq  = OrderSequences[Order];
        int   prev = -1;
        do
        {
            for (int step = 0; step < midiNotes.Length; step++)
            {
                if (prev >= 0) ReleaseVoice(prev, immediate: false);
                NoteOn(step, midiNotes[seq[step]]);
                prev = step;
                yield return new WaitForSeconds(ArpeggioNoteDuration);
            }
        }
        while (ArpeggioLoop);
    }

    private void StopArpeggio()
    {
        if (_arpeggioRoutine != null)
        {
            StopCoroutine(_arpeggioRoutine);
            _arpeggioRoutine = null;
        }
        _syncedNotes = null;
    }

    private void OnClockBeat()
    {
        if (!SyncToMasterClock || _syncedNotes == null || _syncedNotes.Length == 0) return;
        if (++_beatCount < BeatsPerStep) return;
        _beatCount = 0;

        int prev = _arpeggioStep;
        _arpeggioStep = (_arpeggioStep + 1) % _syncedNotes.Length;
        ReleaseVoice(prev, immediate: false);
        NoteOn(_arpeggioStep, _syncedNotes[OrderSequences[Order][_arpeggioStep]]);
    }

    // ── Event handlers ─────────────────────────────────────────────────────
    private void OnChooseCloud(string cloudName)
    {
        CloudChordEntry entry = FindEntry(cloudName);
        if (entry != null)
            PlayChord(entry.rootMidi, entry.chordType, entry.inversion, entry.playStyle);
        else
            PlayChord(DefaultRootMidi, DefaultChordType, DefaultInversion, DefaultPlayStyle);
    }

    private void OnWordSpoken(Wrapper wrapper, string word) => AdvanceWordNote();

    // ── Public API (IChordSource) ──────────────────────────────────────────
    public void AdvanceWordNote()
    {
        if (CurrentChordFrequencies.Length == 0) return;
        _wordNoteIndex = (_wordNoteIndex + 1) % CurrentChordFrequencies.Length;
    }

    public void ResetWordNote() => _wordNoteIndex = 0;

    // ── Helpers ────────────────────────────────────────────────────────────
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

    private static float[] MidiNotesToFreqs(int[] midiNotes)
    {
        float[] freqs = new float[midiNotes.Length];
        for (int i = 0; i < midiNotes.Length; i++)
            freqs[i] = 440f * Mathf.Pow(2f, (midiNotes[i] - 69) / 12f);
        return freqs;
    }
}
