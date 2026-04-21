using UnityEngine;

public class ScaleFromMicrophone : MonoBehaviour
{
    //see the cool examples here: https://www.youtube.com/watch?v=dzD0qP8viLw&t=628s
    //I could use this to scale a mouth on a virtual character, or to blow clouds, or to react to environmental music and sounds
    public AudioSource source;
    public Vector3 minScale;
    public Vector3 maxScale;
    public AudioLoudnessDetection detector;

    public float loudnessSensitivity=100;
    public float threshHold = 0.1f;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    // Update is called once per frame
    void Update()
    {
        float loudness = detector.GetLoudnessFromAudioClip(source.timeSamples, source.clip) * loudnessSensitivity;
       
       if (loudness < threshHold)
        {
            loudness = 0;
        }
       transform.localScale = Vector3.Lerp(minScale, maxScale, loudness);
    }
}
