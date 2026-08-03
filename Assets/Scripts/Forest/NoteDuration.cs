namespace Synthic
{
    // Rhythmic value of a single note, expressed relative to a quarter note (the unit
    // RhythmicMasterClock.BeatDuration already measures). Used anywhere a note or hop
    // needs to know how long it should hold before the next one is allowed.
    public enum NoteDuration
    {
        ThirtySecond,
        Sixteenth,
        Eighth,
        Quarter,
        Half,
        Whole
    }
}
