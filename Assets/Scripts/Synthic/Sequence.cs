using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    [System.Serializable]
    public class Sequence
    {
        public string name = "New Pattern";
        [Range(0f, 0.5f)] public float swing = 0f;
        public List<SequenceStep> steps = new();

        public Sequence()
        {
            // initialize with 16 empty steps
            for (int i = 0; i < 16; i++)
                steps.Add(new SequenceStep());
        }
    }
}