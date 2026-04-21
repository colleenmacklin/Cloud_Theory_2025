using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    public class SynthOut : MonoBehaviour
    {
        //[SerializeField] private SynthProvider provider;
        [SerializeField] private List<SynthProvider> providers;

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels != 2) { 
            //Debug.LogError("Synthic only works with unity STEREO output mode."); 
            return; 
            }
        foreach (var provider in providers)
        {
            if (provider != null) provider.FillBuffer(data);
        }
    }

    }
}