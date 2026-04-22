using UnityEngine;

public class Vocoder : MonoBehaviour
{
    public AudioSource modulatorSource; // The Voice
    public AudioSource carrierSource;   // The Synth/Noise
    
    [Range(0, 5)]
    public float sensitivity = 2.0f;
    
    private float[] modulatorSpectrum = new float[256];

    void Update()
    {
        // 1. Get the frequency data from the voice (modulator)
        modulatorSource.GetSpectrumData(modulatorSpectrum, 0, FFTWindow.Blackman);

        // 2. Calculate the average "energy" of the voice
        float totalEnergy = 0;
        for (int i = 0; i < modulatorSpectrum.Length; i++)
        {
            totalEnergy += modulatorSpectrum[i];
        }

        // 3. Apply that energy to the volume of the carrier
        // This makes the synth "speak" at the rhythm of the voice
        float targetVolume = Mathf.Clamp01(totalEnergy * sensitivity);
        carrierSource.volume = Mathf.Lerp(carrierSource.volume, targetVolume, Time.deltaTime * 15f);
    }
}
