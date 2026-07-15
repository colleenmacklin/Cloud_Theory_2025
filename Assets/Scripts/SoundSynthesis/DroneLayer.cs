using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

/// <summary>
/// Adds one drone layer each time a previously-unseen cloud is chosen.
/// Assign up to 5 FMOD events in DroneEvents; they are started in order and
/// left running indefinitely (sustaining or looping inside FMOD Studio).
/// </summary>
public class DroneLayer : MonoBehaviour
{
    [Header("Drone Events")]
    [Tooltip("One FMOD event per drone layer, played in order as new clouds are discovered.")]
    public EventReference[] DroneEvents = new EventReference[5];

    [Range(0f, 1f)] public float DroneVolume = 0.6f;

    private readonly HashSet<string> _seenClouds = new();
    private readonly EventInstance[] _instances   = new EventInstance[5];
    private int _layerCount;

    private void OnEnable()  => Actions.ChooseCloud += OnCloudChosen;
    private void OnDisable() => Actions.ChooseCloud -= OnCloudChosen;

    private void OnDestroy()
    {
        foreach (var inst in _instances)
        {
            if (inst.isValid())
            {
                inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                inst.release();
            }
        }
    }

    private void OnCloudChosen(string cloudName)
    {
        if (string.IsNullOrEmpty(cloudName)) return;
        if (!_seenClouds.Add(cloudName)) return;      // already seen — do nothing
        if (_layerCount >= DroneEvents.Length) return; // all layers already running

        StartDrone(_layerCount);
        _layerCount++;
    }

    private void StartDrone(int index)
    {
        if (DroneEvents[index].IsNull)
        {
            Debug.LogWarning($"[DroneLayer] DroneEvents[{index}] is not assigned.");
            return;
        }

        try
        {
            EventInstance inst = RuntimeManager.CreateInstance(DroneEvents[index]);
            inst.setVolume(DroneVolume);
            inst.start();
            _instances[index] = inst;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DroneLayer] Failed to start drone {index}: {e.Message}");
        }
    }
}
