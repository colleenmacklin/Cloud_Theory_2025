using System;
using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD;
using Debug = UnityEngine.Debug;

/// <summary>
/// Reads RMS loudness for use with ScaleFromAudioClip.
///
/// Two modes — set one or the other in the Inspector, not both:
///
///   Per-object mode (recommended for prefabs):
///     Assign the StudioEventEmitter on this GameObject to Emitter.
///     Each instance meters its own event, so multiple prefab copies behave independently.
///
///   Global bus mode:
///     Leave Emitter empty and set BusPath to an FMOD Studio bus (e.g. "bus:/Ambiance").
///     All objects sharing this component will respond to the same bus level.
/// </summary>
public class FMODBusLoudness : MonoBehaviour
{
    [Header("Per-object mode")]
    [Tooltip("Assign the StudioEventEmitter on this GameObject. Each instance gets independent metering.")]
    public StudioEventEmitter Emitter;

    [Header("Global bus mode (ignored if Emitter is set)")]
    [Tooltip("FMOD Studio bus to meter (e.g. \"bus:/Ambiance\"). Leave empty for master bus.")]
    public string BusPath = "bus:/";

    [Tooltip("Current RMS level — visible in Inspector during play for debugging.")]
    [SerializeField] private float _debugRms;

    // Bus mode state
    private FMOD.DSP _busDsp;
    private bool _busReady;

    private void Start()
    {
        if (Emitter == null)
        {
            if (string.IsNullOrEmpty(BusPath)) BusPath = "bus:/";
            StartCoroutine(SetupBusMeter());
        }
    }

    // Bus metering requires a flush + one frame after lockChannelGroup before getChannelGroup works.
    private IEnumerator SetupBusMeter()
    {
        var result = RuntimeManager.StudioSystem.getBus(BusPath, out FMOD.Studio.Bus bus);
        if (result != RESULT.OK)
        {
            Debug.LogWarning($"[FMODBusLoudness] Bus '{BusPath}' not found: {result}");
            yield break;
        }

        bus.lockChannelGroup();
        RuntimeManager.StudioSystem.flushCommands();
        yield return null;

        if (bus.getChannelGroup(out FMOD.ChannelGroup cg) != RESULT.OK)
        {
            Debug.LogWarning($"[FMODBusLoudness] getChannelGroup failed for '{BusPath}'. Check the bus name in FMOD Studio.");
            yield break;
        }

        if (cg.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, out _busDsp) != RESULT.OK)
        {
            Debug.LogWarning($"[FMODBusLoudness] getDSP(HEAD) failed for '{BusPath}'.");
            yield break;
        }

        _busDsp.setMeteringEnabled(false, true);
        _busReady = true;
        Debug.Log($"[FMODBusLoudness] Bus metering ready on '{BusPath}'.");
    }

    /// <summary>Returns the RMS loudness (0–1) for this object's audio this frame.</summary>
    public float GetLoudness()
    {
        return Emitter != null ? GetEmitterLoudness() : GetBusLoudness();
    }

    private float GetEmitterLoudness()
    {
        if (!Emitter.EventInstance.isValid()) return 0f;

        if (Emitter.EventInstance.getChannelGroup(out FMOD.ChannelGroup cg) != RESULT.OK)
            return 0f;

        if (cg.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, out FMOD.DSP dsp) != RESULT.OK)
            return 0f;

        dsp.setMeteringEnabled(false, true);

        if (dsp.getMeteringInfo(IntPtr.Zero, out DSP_METERING_INFO info) != RESULT.OK)
            return 0f;

        if (info.numchannels == 0) return 0f;

        float sumSq = 0f;
        for (int i = 0; i < info.numchannels; i++)
            sumSq += info.rmslevel[i] * info.rmslevel[i];

        _debugRms = Mathf.Sqrt(sumSq / info.numchannels);
        return _debugRms;
    }

    private float GetBusLoudness()
    {
        if (!_busReady) return 0f;

        if (_busDsp.getMeteringInfo(IntPtr.Zero, out DSP_METERING_INFO info) != RESULT.OK)
            return 0f;

        if (info.numchannels == 0) return 0f;

        float sumSq = 0f;
        for (int i = 0; i < info.numchannels; i++)
            sumSq += info.rmslevel[i] * info.rmslevel[i];

        _debugRms = Mathf.Sqrt(sumSq / info.numchannels);
        return _debugRms;
    }
}
