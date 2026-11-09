using UnityEngine;

namespace Synthic.Patterns
{
    // The scene's key. Put one on any GameObject in the Forest scene (e.g. next to the
    // RhythmicMasterClock). Every AnimalNoteSequencer reads it at the moment a note
    // plays, so changing the key at runtime — from the Inspector or from code via
    // SetKey() — retunes every animal on its next hop.
    public class MusicalKey : MonoBehaviour
    {
        public static MusicalKey Instance { get; private set; }

        [Tooltip("The key's root note. Scale step 0 plays this note.")]
        public NoteName root = NoteName.D;
        [Tooltip("Which scale the steps walk through.")]
        public ScaleMode mode = ScaleMode.Major;
        [Tooltip("Octave of step 0 (scientific pitch: 4 = the octave starting at middle C).")]
        [Range(1, 7)] public int octave = 4;

        public event System.Action OnKeyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[MusicalKey] More than one MusicalKey in the scene; using {Instance.name}, ignoring {name}.");
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate() => OnKeyChanged?.Invoke();

        public void SetKey(NoteName newRoot, ScaleMode newMode)
        {
            root = newRoot;
            mode = newMode;
            OnKeyChanged?.Invoke();
        }

        public int StepToMidi(int step, int octaveShift = 0) =>
            Scales.StepToMidi(step, root, mode, octave, octaveShift);

        // Used when no MusicalKey exists in the scene: D major, like the talk's demo.
        public static int StepToMidiOrDefault(int step, int octaveShift = 0) =>
            Instance != null
                ? Instance.StepToMidi(step, octaveShift)
                : Scales.StepToMidi(step, NoteName.D, ScaleMode.Major, 4, octaveShift);

        public static int ScaleLength => Instance != null ? Scales.Length(Instance.mode) : 7;
    }
}
