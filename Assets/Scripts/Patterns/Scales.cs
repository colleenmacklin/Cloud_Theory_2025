namespace Synthic.Patterns
{
    public enum NoteName { C, CSharp, D, DSharp, E, F, FSharp, G, GSharp, A, ASharp, B }

    public enum ScaleMode
    {
        Major,
        Minor,
        Dorian,
        Phrygian,
        Lydian,
        Mixolydian,
        Locrian,
        HarmonicMinor,
        MajorPentatonic,
        MinorPentatonic,
        Blues,
        WholeTone,
        Chromatic,
    }

    // Turns scale steps into MIDI note numbers, the way Strudel's n("0 2 4").scale("D:major")
    // does: step 0 is the root, step 1 the next note up the scale, and so on. Steps wrap
    // into the next octave, so in a 7-note scale step 7 is the root an octave up and
    // step -1 is the note just below the root.
    public static class Scales
    {
        public static int[] Intervals(ScaleMode mode)
        {
            switch (mode)
            {
                case ScaleMode.Major:           return new[] { 0, 2, 4, 5, 7, 9, 11 };
                case ScaleMode.Minor:           return new[] { 0, 2, 3, 5, 7, 8, 10 };
                case ScaleMode.Dorian:          return new[] { 0, 2, 3, 5, 7, 9, 10 };
                case ScaleMode.Phrygian:        return new[] { 0, 1, 3, 5, 7, 8, 10 };
                case ScaleMode.Lydian:          return new[] { 0, 2, 4, 6, 7, 9, 11 };
                case ScaleMode.Mixolydian:      return new[] { 0, 2, 4, 5, 7, 9, 10 };
                case ScaleMode.Locrian:         return new[] { 0, 1, 3, 5, 6, 8, 10 };
                case ScaleMode.HarmonicMinor:   return new[] { 0, 2, 3, 5, 7, 8, 11 };
                case ScaleMode.MajorPentatonic: return new[] { 0, 2, 4, 7, 9 };
                case ScaleMode.MinorPentatonic: return new[] { 0, 3, 5, 7, 10 };
                case ScaleMode.Blues:           return new[] { 0, 3, 5, 6, 7, 10 };
                case ScaleMode.WholeTone:       return new[] { 0, 2, 4, 6, 8, 10 };
                case ScaleMode.Chromatic:       return new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
                default:                        return new[] { 0, 2, 4, 5, 7, 9, 11 };
            }
        }

        public static int Length(ScaleMode mode) => Intervals(mode).Length;

        // octave uses scientific pitch: octave 4 with root C gives middle C (MIDI 60).
        // octaveShift moves whole octaves on top of whatever the step already implies.
        public static int StepToMidi(int step, NoteName root, ScaleMode mode, int octave, int octaveShift = 0)
        {
            int[] iv = Intervals(mode);
            int n = iv.Length;
            int wraps = (int)Pattern.FloorDiv(step, n);
            int degree = step - wraps * n;
            return 12 * (octave + 1 + wraps + octaveShift) + (int)root + iv[degree];
        }
    }
}
