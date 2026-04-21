using UnityEngine;

public class ScaleFromAudioClip : MonoBehaviour
{
    public AudioSource source;
    public Vector3 minScale;
    public Vector3 maxScale;
    public AudioLoudnessDetection detector;

    public float loudnessSensitivity = 100f;
    public float threshHold          = 0.1f;
    public float smoothSpeed         = 10f; // how fast scale lerps

    private float _currentLoudness = 0f;

    void Update()
    {
        if (source == null || source.clip == null)
            return;

        float loudness = 0f;

        // only sample if the clip is actually playing
        if (source.isPlaying)
        {
            int samplePos = source.timeSamples;
            loudness = detector.GetLoudnessFromAudioClip(samplePos, source.clip)
                       * loudnessSensitivity;

            if (loudness < threshHold)
                loudness = 0f;
        }

        // smooth the loudness so scale doesn't snap
        _currentLoudness = Mathf.Lerp(_currentLoudness, loudness, Time.deltaTime * smoothSpeed);

        transform.localScale = Vector3.Lerp(minScale, maxScale, _currentLoudness);
    }
}