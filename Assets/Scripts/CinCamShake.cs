using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CinCamShake : MonoBehaviour
{
    CinemachineCamera vcam;
    CinemachineBasicMultiChannelPerlin noise;

    void Start()
    {
        vcam = GameObject.Find("CM vcam1").GetComponent<CinemachineCamera>();
        noise = vcam.GetComponentInChildren<CinemachineBasicMultiChannelPerlin>();

        if (noise == null)
        {
            Debug.LogError("CinemachineBasicMultiChannelPerlin component not found on children.");
        }
    }

    public void Noise(float amplitudeGain, float frequencyGain)
    {
        noise.AmplitudeGain = amplitudeGain;
        //noise.m_AmplitudeGain = amplitudeGain;
        //noise.m_FrequencyGain = frequencyGain;
        noise.FrequencyGain = frequencyGain;
    }

}
