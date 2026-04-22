using UnityEngine;

namespace Synthic
{
    [System.Serializable]
    public class SequenceStep
    {
        public enum StepType { Normal, Rest, Tie, Accent }

        public bool active = true;
        public StepType stepType = StepType.Normal;
        public Note.Name note = Note.Name.C;
        public int octave = 4;
        [Range(0f, 1f)] public float gateLength = 0.8f;
        [Range(0f, 1f)] public float velocity = 0.7f;
    }
}