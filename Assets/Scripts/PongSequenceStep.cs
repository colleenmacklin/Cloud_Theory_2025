using UnityEngine;

namespace Synthic
{
    /// <summary>
    /// One 16th-note step in a Pong sequence.
    /// </summary>
    [System.Serializable]
    public class PongSequenceStep
    {
        [Tooltip("If true, no ball is spawned and no note plays on this step")]
        public bool isRest = false;

        [Tooltip("If true, this step is a continuation of the previous note — no new ball spawned")]
        public bool isTie = false;

        [Tooltip("The note to play (ignored if isRest or isTie)")]
        public Note.Name note = Note.Name.C;

        [Tooltip("Octave of the note")]
        [Range(1, 7)]
        public int octave = 3;

        [Tooltip("Launch angle offset in degrees — randomised at runtime within launchSpread")]
        [Range(-30f, 30f)]
        public float angleOffset = 0f;

        // ── Convenience ──────────────────────────────────────────────────────

        public float Frequency => Note.Get(note, octave);

        public bool SpawnsBall => !isRest && !isTie;

        /// <summary>Create a rest step.</summary>
        public static PongSequenceStep Rest()
            => new PongSequenceStep { isRest = true };

        /// <summary>Create a tie step.</summary>
        public static PongSequenceStep Tie()
            => new PongSequenceStep { isTie = true };

        /// <summary>Create a note step.</summary>
        public static PongSequenceStep NoteStep(Note.Name n, int oct = 3, float angle = 0f)
            => new PongSequenceStep { note = n, octave = oct, angleOffset = angle };
    }
}
