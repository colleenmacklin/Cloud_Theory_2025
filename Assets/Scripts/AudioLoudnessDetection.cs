using UnityEngine;

public class AudioLoudnessDetection : MonoBehaviour
{
    public int sampleWindow = 64;
    //private AudioClip microphoneClip;

    //void Start()
    //{
        //MicrophoneToAudioClip();
    //}
/*
    public void MicrophoneToAudioClip()
    {
        //get the first microphone in the devices list
        string microphoneName = Microphone.devices[0]; 
        microphoneClip = Microphone.Start(microphoneName, true, 20, AudioSettings.outputSampleRate);

    }

    public float GetLoudnessFromMicrophone()
    {
        return GetLoudnessFromAudioClip(Microphone.GetPosition(Microphone.devices[0]), microphoneClip);
    }
*/
public float GetLoudnessFromAudioClip(int clipPosition, AudioClip clip)
{
    int startPosition = clipPosition - sampleWindow;

    // clamp to start of clip instead of returning 0
    if (startPosition < 0)
        startPosition = 0;

    // make sure we don't go past the end of the clip
    if (startPosition + sampleWindow > clip.samples)
        return 0f;

    float[] waveData = new float[sampleWindow];
    clip.GetData(waveData, startPosition);

    float totalLoudness = 0f;
    for (int i = 0; i < sampleWindow; i++)
        totalLoudness += Mathf.Abs(waveData[i]);

    return totalLoudness / sampleWindow;
}

}
