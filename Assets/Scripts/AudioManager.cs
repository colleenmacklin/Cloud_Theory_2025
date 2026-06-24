using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AudioManager : MonoBehaviour
{
    [EventRef]
    public string defaultAmbiance;
    public static AudioManager instance;

    private EventInstance _trackA;
    private EventInstance _trackB;
    private bool _isOnA = true;

    public void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(defaultAmbiance)) return;
        _trackA = RuntimeManager.CreateInstance(defaultAmbiance);
        _trackA.setVolume(1f);
        _trackA.start();
    }

    public void swapTrack(string newEvent)
    {
        StopAllCoroutines();
        StartCoroutine(CrossFade(newEvent));
    }

    public void returnToDefault()
    {
        swapTrack(defaultAmbiance);
    }

    private IEnumerator CrossFade(string newEvent)
    {
        float timeToFade = 2.25f;
        float elapsed = 0f;

        EventInstance incoming = RuntimeManager.CreateInstance(newEvent);
        incoming.setVolume(0f);
        incoming.start();

        EventInstance outgoing = _isOnA ? _trackA : _trackB;

        if (_isOnA)
            _trackB = incoming;
        else
            _trackA = incoming;
        _isOnA = !_isOnA;

        while (elapsed < timeToFade)
        {
            float t = elapsed / timeToFade;
            incoming.setVolume(t);
            outgoing.setVolume(1f - t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        incoming.setVolume(1f);
        outgoing.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        outgoing.release();
    }

    private void OnDestroy()
    {
        StopInstance(ref _trackA);
        StopInstance(ref _trackB);
    }

    private static void StopInstance(ref EventInstance inst)
    {
        if (inst.isValid())
        {
            inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            inst.release();
        }
    }
}
