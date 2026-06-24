using UnityEngine;

public class ScaleFromAudioClip : MonoBehaviour
{
    [Header("FMOD (preferred)")]
    [Tooltip("Assign an FMODBusLoudness component to drive scale from FMOD bus metering.")]
    public FMODBusLoudness fmodBus;

    [Header("Unity Audio (legacy fallback)")]
    public AudioSource source;
    public AudioLoudnessDetection detector;

    [Header("Scale Settings")]
    public Vector3 minScale;
    public Vector3 maxScale;
    public float loudnessSensitivity = 100f;
    public float threshHold          = 0.1f;
    public float smoothSpeed         = 10f;

    private float _currentLoudness = 0f;

    void Update()
    {
        float loudness = 0f;

        if (fmodBus != null)
        {
            loudness = fmodBus.GetLoudness() * loudnessSensitivity;
        }
        else if (source != null && source.clip != null && detector != null && source.isPlaying)
        {
            loudness = detector.GetLoudnessFromAudioClip(source.timeSamples, source.clip)
                       * loudnessSensitivity;
        }

        if (loudness < threshHold)
            loudness = 0f;

        _currentLoudness = Mathf.Lerp(_currentLoudness, loudness, Time.deltaTime * smoothSpeed);
        transform.localScale = Vector3.Lerp(minScale, maxScale, _currentLoudness);
    }
}
