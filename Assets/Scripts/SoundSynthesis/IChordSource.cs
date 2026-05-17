/// <summary>
/// Common interface for any component that provides chord-tone data to AutotuneFilter.
/// Implemented by CloudChordPlayer and FMODChordPlayer.
/// </summary>
public interface IChordSource
{
    /// <summary>Hz of each note in the currently playing chord.</summary>
    float[] CurrentChordFrequencies { get; }

    /// <summary>Which chord tone AutotuneFilter should target for the current spoken word.</summary>
    int WordNoteIndex { get; }
}
