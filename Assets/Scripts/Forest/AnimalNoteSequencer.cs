using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using Synthic;

namespace Forest
{
    // One note in an animal's hop sequence: which pitch to play, and how long that note
    // rhythmically holds before the animal is allowed to hop (and play the next note)
    // again. Mixing durations is what gives each animal's hop pattern its own groove —
    // a quick 16th lets it hop again almost immediately, a half or whole note makes it
    // pause.
    [System.Serializable]
    public struct SequencedNote
    {
        [Tooltip("MIDI note number (e.g. 60 = middle C). Matches the FMOD event's MidiNote parameter range (36-84).")]
        [Range(36, 84)] public int midiNote;
        public NoteDuration duration;
    }

    // Plays a per-animal sequence of notes through FMOD, one note per hop, walking the
    // sequence forward when the animal flees and backward when it hops closer — the
    // "same mechanism, opposite direction" the design calls for. Timing (how long the
    // animal must wait before its next note is allowed) is derived from
    // RhythmicMasterClock's current BPM via SecondsForDuration, so the whole cast of
    // animals stays in the same tempo even if BPM changes at runtime.
    public class AnimalNoteSequencer : MonoBehaviour
    {
        [Header("Sequence")]
        [Tooltip("This animal's note pattern, in the order played while fleeing (forward). Hopping closer plays the same list backward.")]
        public List<SequencedNote> notes = new();

        [Header("FMOD")]
        [Tooltip("FMOD event for a single synth note. Must expose a MidiNote parameter (range 36-84), same convention as FMODChordPlayer.")]
        public EventReference noteEvent;
        [Tooltip("FMOD parameter name that selects the pitch.")]
        public string midiNoteParam = "MidiNote";
        [Range(0f, 1f)] public float noteVolume = 0.8f;

        // Index of the note that was last played. Starts at -1 so the very first
        // PlayNextForward() call plays notes[0] rather than notes[1].
        private int _index = -1;
        private float _readyTime = 0f;

        // True once the cooldown from the last-played note's duration has elapsed —
        // AnimalHopController gates hopping on this so hops never outrun the sequence's
        // own rhythm.
        public bool IsReady => Time.time >= _readyTime;
        public float TimeUntilReady => Mathf.Max(0f, _readyTime - Time.time);

        public void ResetToStart() => _index = -1;

        // Plays the next note going forward (wrapping to the start past the end) —
        // called on each flee-hop.
        public void PlayNextForward()
        {
            if (notes.Count == 0) return;
            _index = (_index + 1) % notes.Count;
            PlayCurrent();
        }

        // Plays the previous note going backward (wrapping to the end past the start) —
        // called on each hop-closer.
        public void PlayNextReverse()
        {
            if (notes.Count == 0) return;
            _index = _index <= 0 ? notes.Count - 1 : _index - 1;
            PlayCurrent();
        }

        private void PlayCurrent()
        {
            SequencedNote note = notes[_index];
            PlayNote(note.midiNote);

            float cooldown = RhythmicMasterClock.Instance != null
                ? RhythmicMasterClock.Instance.SecondsForDuration(note.duration)
                : 0.25f; // sensible fallback if no clock exists in the scene yet
            _readyTime = Time.time + cooldown;
        }

        private void PlayNote(int midiNote)
        {
            if (noteEvent.IsNull)
            {
                Debug.LogWarning($"[AnimalNoteSequencer] {name}: noteEvent is not assigned.");
                return;
            }

            try
            {
                var inst = RuntimeManager.CreateInstance(noteEvent);
                // Required once the event has a spatializer: sets 3D attributes immediately
                // and keeps them updated as the animal hops, so distance-based volume/pan
                // tracks it correctly instead of FMOD warning and playing unpositioned.
                RuntimeManager.AttachInstanceToGameObject(inst, transform);
                inst.setParameterByName(midiNoteParam, midiNote);
                inst.setVolume(noteVolume);
                inst.start();
                inst.release(); // one-shot: FMOD keeps playing until the sound finishes, then cleans up
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AnimalNoteSequencer] {name}: failed to play note {midiNote}: {e.Message}");
            }
        }
    }
}
