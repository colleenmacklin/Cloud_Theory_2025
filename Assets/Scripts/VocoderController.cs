using UnityEngine;

/// <summary>
/// Scene-level controller that wires VoiceHandler → AutotuneFilter → VocoderFilter → CarrierSynth.
///
/// Setup:
///   1. Assign all four references in the Inspector.
///   2. This script can live on any persistent GameObject (e.g. your GameManager).
///   3. Use the Inspector toggles or the public methods to switch effects on/off at runtime.
///
/// Chord changes:
///   Call SetChordByName() with "Dream", "Open", "Minor", "Bright", or "Bare" to shift
///   the carrier harmony — e.g. in response to a cloud shape being spotted.
///   Both the vocoder carrier and the autotune snap target update automatically.
/// </summary>
public class VocoderController : MonoBehaviour
{
    [Header("Scene References")]
    public VoiceHandler   VoiceHandler;
    public VocoderFilter  VocoderFilter;
    public AutotuneFilter AutotuneFilter;
    public CarrierSynth   CarrierSynth;

    [Header("Vocoder Toggle")]
    [Tooltip("Master on/off for the vocoder. Can be toggled at runtime.")]
    public bool VocoderActive = true;

    [Header("Autotune Toggle")]
    [Tooltip("Snap voice pitch to chord notes before vocoding.")]
    public bool AutotuneActive = true;

    [Header("Carrier Settings")]
    [Tooltip("Root note as MIDI number (60 = middle C, 69 = A4 = 440Hz)")]
    [Range(36, 84)]
    public int RootNote = 60;

    [Tooltip("Chord preset to use on Start")]
    public ChordPreset StartingChord = ChordPreset.Dream;

    // ── Chord presets ────────────────────────────────────────────────────
    public enum ChordPreset
    {
        Dream,   // major 7th + 9 — lush and soft
        Open,    // stacked fifths — spacious
        Minor,   // minor 7th — melancholic
        Bright,  // major + 9 — airy
        Bare,    // root + octave only — transparent
    }

    private static readonly int[][] ChordIntervals = {
        new[] { 0, 4, 7, 11, 14 },  // Dream
        new[] { 0, 7, 12, 19 },      // Open
        new[] { 0, 3, 7, 10 },       // Minor
        new[] { 0, 4, 7, 14 },       // Bright
        new[] { 0, 12 },              // Bare
    };

    // ── Lifecycle ────────────────────────────────────────────────────────
    void Start()
    {
        if (CarrierSynth == null || VocoderFilter == null || VoiceHandler == null)
        {
            Debug.LogError("VocoderController: missing references — check Inspector.");
            return;
        }

        SetChord(StartingChord);
        ApplyActiveState();
    }

    void OnValidate()
    {
        if (Application.isPlaying && CarrierSynth != null)
            ApplyActiveState();
    }

    // ── Public API — Vocoder ─────────────────────────────────────────────

    public void EnableVocoder()
    {
        VocoderActive = true;
        ApplyActiveState();
    }

    public void DisableVocoder()
    {
        VocoderActive = false;
        ApplyActiveState();
    }

    public void ToggleVocoder()
    {
        VocoderActive = !VocoderActive;
        ApplyActiveState();
        Debug.Log($"Vocoder: {(VocoderActive ? "ON" : "OFF")}");
    }

    // ── Public API — Autotune ────────────────────────────────────────────

    public void EnableAutotune()
    {
        AutotuneActive = true;
        ApplyActiveState();
    }

    public void DisableAutotune()
    {
        AutotuneActive = false;
        ApplyActiveState();
    }

    public void ToggleAutotune()
    {
        AutotuneActive = !AutotuneActive;
        ApplyActiveState();
        Debug.Log($"Autotune: {(AutotuneActive ? "ON" : "OFF")}");
    }

    // ── Public API — Chord ───────────────────────────────────────────────

    /// <summary>
    /// Set carrier chord by preset enum.
    /// Updates both the vocoder carrier and the autotune snap target.
    /// </summary>
    public void SetChord(ChordPreset preset)
    {
        StartingChord = preset;
        int[] intervals = ChordIntervals[(int)preset];
        CarrierSynth.SetChord(RootNote, intervals);
        // AutotuneFilter reads rootMidi and chordIntervals directly from CarrierSynth,
        // so it updates automatically — no extra call needed.
        Debug.Log($"Chord -> {preset} (root MIDI {RootNote})");
    }

    /// <summary>
    /// Set chord by name string — useful from UnityEvents / VisualScripting.
    /// Valid names: "Dream", "Open", "Minor", "Bright", "Bare"
    /// </summary>
    public void SetChordByName(string presetName)
    {
        if (System.Enum.TryParse(presetName, true, out ChordPreset preset))
            SetChord(preset);
        else
            Debug.LogWarning($"VocoderController: unknown chord preset '{presetName}'");
    }

    /// <summary>
    /// Set the root MIDI note. 60=C4, 62=D4, 64=E4, 65=F4, 67=G4, 69=A4
    /// </summary>
    public void SetRootNote(int midiNote)
    {
        RootNote = Mathf.Clamp(midiNote, 36, 84);
        SetChord(StartingChord);
    }

    // ── Private helpers ──────────────────────────────────────────────────
    private void ApplyActiveState()
    {
        if (VocoderFilter != null)
        {
            if (VocoderActive) VocoderFilter.EnableVocoder();
            else               VocoderFilter.DisableVocoder();
        }

        if (AutotuneFilter != null)
        {
            if (AutotuneActive) AutotuneFilter.EnableAutotune();
            else                AutotuneFilter.DisableAutotune();
        }
    }
}
