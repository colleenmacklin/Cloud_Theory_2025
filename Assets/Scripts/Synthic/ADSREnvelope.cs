using UnityEngine;

namespace Synthic
{
    [System.Serializable]
    public struct ADSREnvelope
    {
        [Tooltip("Time in seconds for the note to ramp from silence to full volume after a NoteOn.\n• 0.001 = instant (percussive)\n• 0.01–0.05 = snappy\n• 0.1–0.5 = gentle fade in\n• 1–5 = slow swell")]
        [Range(0.001f, 5f)] public float attack;

        [Tooltip("Time in seconds to fall from peak volume down to the Sustain level.\n• 0.05–0.2 = tight, plucky\n• 0.3–0.8 = medium decay\n• 1–5 = long bloom")]
        [Range(0.001f, 5f)] public float decay;

        [Tooltip("Volume level held while the key is down, after the Decay phase. 0–1 fraction of peak.\n• 0 = note dies after decay (pluck / stab)\n• 0.3–0.7 = partial sustain\n• 1 = holds at full volume (organ)")]
        [Range(0f, 1f)] public float sustain;

        [Tooltip("Time in seconds for the note to fade from the Sustain level to silence after NoteOff.\n• 0.01–0.1 = staccato, immediate cutoff\n• 0.2–0.5 = short tail\n• 1–5 = long reverberant tail")]
        [Range(0.001f, 5f)] public float release;
    }
}
