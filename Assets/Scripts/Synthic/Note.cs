using System.Collections.Generic;

namespace Synthic
{
    public static class Note
    {

//api:
        // get a single note frequency
//float freq = Note.Get(Note.Name.A, 4);  // 440Hz

// get a chord
//float[] cMajor = Note.GetChord(Note.Name.C, Note.Minor, 4);

// convert from MIDI
//float freq = Note.MidiToFrequency(60); // middle C

// get voice id for matching NoteOn/NoteOff
//int id = Note.GetId(freq);

        // A4 = MIDI note 69 = 440Hz, all other frequencies derived from this
        private const float A4Frequency = 440f;
        private const int A4MidiNote = 69;

        public enum Name
        {
            C, CSharp, D, DSharp, E, F, FSharp, G, GSharp, A, ASharp, B
        }

        // common chords as semitone intervals from root
        public static readonly int[] Major      = { 0, 4, 7 };
        public static readonly int[] Minor      = { 0, 3, 7 };
        public static readonly int[] Diminished = { 0, 3, 6 };
        public static readonly int[] Augmented  = { 0, 4, 8 };
        public static readonly int[] Major7     = { 0, 4, 7, 11 };
        public static readonly int[] Minor7     = { 0, 3, 7, 10 };
        public static readonly int[] Dominant7  = { 0, 4, 7, 10 };

        // convert MIDI note number to frequency
        public static float MidiToFrequency(int midiNote)
        {
            return A4Frequency * UnityEngine.Mathf.Pow(2f, (midiNote - A4MidiNote) / 12f);
        }

        // convert frequency to nearest MIDI note number
        public static int FrequencyToMidi(float frequency)
        {
            return UnityEngine.Mathf.RoundToInt(
                A4MidiNote + 12f * UnityEngine.Mathf.Log(frequency / A4Frequency, 2f));
        }

        // get frequency from note name and octave
        // e.g. Get(Name.A, 4) = 440Hz, Get(Name.C, 4) = middle C
        public static float Get(Name note, int octave = 4)
        {
            int midiNote = ((octave + 1) * 12) + (int)note;
            return MidiToFrequency(midiNote);
        }

        // get a unique id for a note - used for voice matching
        public static int GetId(float frequency)
        {
            return UnityEngine.Mathf.RoundToInt(frequency * 100f);
        }

        // get all frequencies in a chord from a root note and octave
        public static float[] GetChord(Name root, int[] intervals, int octave = 4)
        {
            float[] frequencies = new float[intervals.Length];
            int rootMidi = ((octave + 1) * 12) + (int)root;
            for (int i = 0; i < intervals.Length; i++)
                frequencies[i] = MidiToFrequency(rootMidi + intervals[i]);
            return frequencies;
        }

        // get note name and octave from MIDI note number
        public static (Name name, int octave) MidiToNote(int midiNote)
        {
            int octave = (midiNote / 12) - 1;
            Name name = (Name)(midiNote % 12);
            return (name, octave);
        }

        // chromatic scale keyboard mapping - two octaves starting from C3
        public static readonly Dictionary<UnityEngine.KeyCode, float> KeyboardMap =
            new Dictionary<UnityEngine.KeyCode, float>
        {
            // lower row - C3 to B3
            { UnityEngine.KeyCode.Z,            Get(Name.C,      3) },
            { UnityEngine.KeyCode.S,            Get(Name.CSharp, 3) },
            { UnityEngine.KeyCode.X,            Get(Name.D,      3) },
            { UnityEngine.KeyCode.D,            Get(Name.DSharp, 3) },
            { UnityEngine.KeyCode.C,            Get(Name.E,      3) },
            { UnityEngine.KeyCode.V,            Get(Name.F,      3) },
            { UnityEngine.KeyCode.G,            Get(Name.FSharp, 3) },
            { UnityEngine.KeyCode.B,            Get(Name.G,      3) },
            { UnityEngine.KeyCode.H,            Get(Name.GSharp, 3) },
            { UnityEngine.KeyCode.N,            Get(Name.A,      3) },
            { UnityEngine.KeyCode.J,            Get(Name.ASharp, 3) },
            { UnityEngine.KeyCode.M,            Get(Name.B,      3) },

            // upper row - C4 to B4
            { UnityEngine.KeyCode.Q,            Get(Name.C,      4) },
            { UnityEngine.KeyCode.Alpha2,       Get(Name.CSharp, 4) },
            { UnityEngine.KeyCode.W,            Get(Name.D,      4) },
            { UnityEngine.KeyCode.Alpha3,       Get(Name.DSharp, 4) },
            { UnityEngine.KeyCode.E,            Get(Name.E,      4) },
            { UnityEngine.KeyCode.R,            Get(Name.F,      4) },
            { UnityEngine.KeyCode.Alpha5,       Get(Name.FSharp, 4) },
            { UnityEngine.KeyCode.T,            Get(Name.G,      4) },
            { UnityEngine.KeyCode.Alpha6,       Get(Name.GSharp, 4) },
            { UnityEngine.KeyCode.Y,            Get(Name.A,      4) },
            { UnityEngine.KeyCode.Alpha7,       Get(Name.ASharp, 4) },
            { UnityEngine.KeyCode.U,            Get(Name.B,      4) },
        };
    }
}