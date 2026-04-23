using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Synthic;
using Crosstales.RTVoice;
using Crosstales.RTVoice.Model;

/// <summary>
/// Assigns tetrad chords to cloud shapes and drives PolyphonicGenerator.
///
/// Setup:
///   1. Add this component to a persistent GameObject (e.g. your music manager).
///   2. Assign the PolyphonicGenerator reference in the Inspector.
///   3. Add entries to CloudChords — one per cloud shape name. The name is matched
///      case-insensitively against the string fired by Actions.ChooseCloud.
///   4. Assign this component to AutotuneFilter.ChordSource so the autotune
///      follows the same chord.
///
/// Play styles (switchable at runtime via SetPlayStyle or per-cloud entry):
///   Block    — all four notes sound simultaneously (classic block chord).
///   Toggle   — forces note retrigger through ADSR even if notes are held.
///   Arpeggio — plays notes one at a time in ascending order, looping.
///
/// Word note cycling:
///   AutotuneFilter reads WordNoteIndex to know which chord tone to target.
///   The index advances automatically on each spoken word via RTVoice's
///   OnSpeakCurrentWordString callback.
/// </summary>
public class CloudChordController : MonoBehaviour
{
    // ── Chord types ───────────────────────────────────────────────────────
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

    public enum PlayStyle { Block, Toggle, Arpeggio }

    // ── Per-cloud mapping ─────────────────────────────────────────────────
    [System.Serializable]
    public class CloudChordEntry
    {
        [Tooltip("Cloud name to match (from Actions.ChooseCloud). Case-insensitive; substring match used as fallback.")]
        public string cloudName = "";

        [Range(36, 84)]
        [Tooltip("Root note as MIDI number (60=C4, 62=D4, 64=E4, 65=F4, 67=G4, 69=A4=440Hz)")]
        public int rootMidi = 60;

        public TetraType chordType = TetraType.Major7;
        public Inversion inversion = Inversion.Root;
        public PlayStyle playStyle = PlayStyle.Block;
    }

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("References")]
    public PolyphonicGenerator Synth;

    [Header("Cloud → Chord Mapping")]
    [Tooltip("One entry per cloud shape. Falls back to the default chord if no name matches.")]
    public List<CloudChordEntry> CloudChords = new();

    [Header("Default Chord")]
    [Range(36, 84)] public int         DefaultRootMidi  = 60;
    public TetraType DefaultChordType  = TetraType.Major7;
    public Inversion DefaultInversion  = Inversion.Root;
    public PlayStyle DefaultPlayStyle  = PlayStyle.Block;

    [Header("Arpeggio")]
    [Range(0.05f, 2f)]
    [Tooltip("How long each note sounds before the next one starts.")]
    public float ArpeggioNoteDuration = 0.25f;
    public bool  ArpeggioLoop         = true;

    [Header("Debug (read-only)")]
    [SerializeField] private string _activeCloudName;
    [SerializeField] private string _activeChordLabel;
    [SerializeField] private int    _wordNoteIndex;

    // ── Public state (read by AutotuneFilter) ─────────────────────────────
    /// <summary>The four note frequencies of the currently active chord.</summary>
    public float[] CurrentChordFrequencies { get; private set; } = System.Array.Empty<float>();

    /// <summary>Which chord-tone index (0–3) AutotuneFilter should target right now.</summary>
    public int WordNoteIndex => _wordNoteIndex;

    // ── Private ───────────────────────────────────────────────────────────
    private Coroutine _arpeggioRoutine;

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

    // ── Unity lifecycle ───────────────────────────────────────────────────
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
        // Ensure a chord is always playing from the moment the scene starts.
        PlayChord(DefaultRootMidi, DefaultChordType, DefaultInversion, DefaultPlayStyle);
    }

    // ── RTVoice per-word callback ─────────────────────────────────────────
    // Fires once per spoken word, advancing the autotune target through the chord tones.
    private void OnWordSpoken(Wrapper wrapper, string word)
    {
        AdvanceWordNote();
    }

    // ── Actions.ChooseCloud handler ───────────────────────────────────────
    private void OnChooseCloud(string cloudName)
    {
        _activeCloudName = cloudName;
        CloudChordEntry entry = FindEntry(cloudName);

        if (entry != null)
            PlayChord(entry.rootMidi, entry.chordType, entry.inversion, entry.playStyle);
        else
            PlayChord(DefaultRootMidi, DefaultChordType, DefaultInversion, DefaultPlayStyle);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Step the autotune cursor forward by one chord tone (wraps at top).</summary>
    public void AdvanceWordNote()
    {
        if (CurrentChordFrequencies.Length == 0) return;
        _wordNoteIndex = (_wordNoteIndex + 1) % CurrentChordFrequencies.Length;
    }

    /// <summary>Reset the autotune cursor to the root note (call at the start of each sentence).</summary>
    public void ResetWordNote() => _wordNoteIndex = 0;

    /// <summary>
    /// Swap the play style at runtime and immediately retrigger the current chord notes.
    /// Useful for binding to a UI slider or Timeline signal.
    /// </summary>
    public void SetPlayStyle(PlayStyle style)
    {
        DefaultPlayStyle = style;
        StopArpeggio();
        TriggerStyle(style, CurrentChordFrequencies);
    }

    // ── Core chord player ─────────────────────────────────────────────────
    private void PlayChord(int rootMidi, TetraType type, Inversion inv, PlayStyle style)
    {
        if (Synth == null)
        {
            Debug.LogWarning("CloudChordController: Synth reference is not assigned.");
            return;
        }

        StopArpeggio();

        // NoteOff every note that is currently playing before switching chords.
        // CurrentChordFrequencies always reflects what is live, so this is immediate
        // and never one step behind.
        foreach (float f in CurrentChordFrequencies) Synth.NoteOff(f);

        int[]   intervals = BuildIntervals(type, inv);
        float[] freqs     = IntervalsToFreqs(rootMidi, intervals);

        CurrentChordFrequencies = freqs;
        _wordNoteIndex          = 0;
        _activeChordLabel       = $"{(Note.Name)(rootMidi % 12)} {type} inv{(int)inv} [{style}]";

        TriggerStyle(style, freqs);
    }

    private void TriggerStyle(PlayStyle style, float[] freqs)
    {
        switch (style)
        {
            case PlayStyle.Block:
                // Trigger all notes. NoteOn skips voices already playing the same note.
                foreach (float f in freqs) Synth.NoteOn(f);
                break;

            case PlayStyle.Toggle:
                // Force ADSR retrigger even if notes are already held.
                foreach (float f in freqs) Synth.NoteOff(f);
                foreach (float f in freqs) Synth.ForceNoteOn(f);
                break;

            case PlayStyle.Arpeggio:
                _arpeggioRoutine = StartCoroutine(ArpeggioRoutine(freqs));
                break;
        }
    }

    // Plays notes in ascending order, releasing each before the next sounds.
    private IEnumerator ArpeggioRoutine(float[] freqs)
    {
        do
        {
            for (int i = 0; i < freqs.Length; i++)
            {
                if (Synth == null) yield break;
                if (i > 0) Synth.NoteOff(freqs[i - 1]);
                Synth.NoteOn(freqs[i]);
                yield return new WaitForSeconds(ArpeggioNoteDuration);
            }
            if (Synth != null) Synth.NoteOff(freqs[freqs.Length - 1]);
        }
        while (ArpeggioLoop);
    }

    private void StopArpeggio()
    {
        if (_arpeggioRoutine == null) return;
        StopCoroutine(_arpeggioRoutine);
        _arpeggioRoutine = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private CloudChordEntry FindEntry(string cloudName)
    {
        string _cloudname = parseName(cloudName);

        // Exact match first.
        foreach (var e in CloudChords)
            if (string.Equals(e.cloudName, _cloudname, System.StringComparison.OrdinalIgnoreCase))
                return e;

        // Substring match as fallback (handles names like "cloud_dragon" matching "dragon").
        foreach (var e in CloudChords)
        {
            if (!string.IsNullOrEmpty(e.cloudName) &&
                (_cloudname.IndexOf(e.cloudName, System.StringComparison.OrdinalIgnoreCase) >= 0
                 || e.cloudName.IndexOf(_cloudname, System.StringComparison.OrdinalIgnoreCase) >= 0))
                return e;
        }
        return null;
    }
    public string parseName(string s)
    {
        string name = s.Replace("_", " ");
        return name;
    }
    // Builds the interval array for a given chord type, then rotates for inversions.
    // Inversion n: take the lowest note, transpose it up an octave (+12), place at top.
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
            freqs[i] = Note.MidiToFrequency(rootMidi + intervals[i]);
        return freqs;
    }
}
