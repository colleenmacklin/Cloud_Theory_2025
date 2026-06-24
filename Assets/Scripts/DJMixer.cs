using UnityEngine;
using FMODUnity;

/// <summary>
/// Crossfader between two FMOD buses — one per platter.
/// Drives volume from a UI slider via SetCrossfader().
/// Uses a constant-power curve so the centre position doesn't dip in volume.
///
/// Setup:
///   1. In FMOD Studio, route each platter's events to separate buses
///      (e.g. bus:/Platter1 and bus:/Platter2).
///   2. Set BusPathA and BusPathB here to match.
///   3. Wire a UI Slider's OnValueChanged to SetCrossfader().
/// </summary>
public class DJMixer : MonoBehaviour
{
    [Header("FMOD Buses")]
    public string BusPathA = "bus:/Platter1";
    public string BusPathB = "bus:/Platter2";

    [Range(0f, 1f)]
    [Tooltip("0 = full Platter A, 1 = full Platter B, 0.5 = equal mix.")]
    public float Crossfader = 0.5f;

    private FMOD.Studio.Bus _busA;
    private FMOD.Studio.Bus _busB;
    private bool _ready;

    private void Start()
    {
        bool a = RuntimeManager.StudioSystem.getBus(BusPathA, out _busA) == FMOD.RESULT.OK;
        bool b = RuntimeManager.StudioSystem.getBus(BusPathB, out _busB) == FMOD.RESULT.OK;
        _ready = a && b;

        if (!a) Debug.LogWarning($"[DJMixer] Bus '{BusPathA}' not found.");
        if (!b) Debug.LogWarning($"[DJMixer] Bus '{BusPathB}' not found.");
    }

    private void Update()
    {
        if (!_ready) return;

        // constant-power crossfade: cos curve keeps perceived loudness even at centre
        float t = Crossfader;
        _busA.setVolume(Mathf.Cos(t * Mathf.PI * 0.5f));
        _busB.setVolume(Mathf.Cos((1f - t) * Mathf.PI * 0.5f));
    }

    /// <summary>Called by a UI Slider's OnValueChanged event.</summary>
    public void SetCrossfader(float value) => Crossfader = Mathf.Clamp01(value);
}
