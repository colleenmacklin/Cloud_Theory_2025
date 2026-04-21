using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SequencerData", menuName = "ScriptableObjects/SequencerDataScriptableObject", order = 1)]
public class SequencerDataScriptableObject : ScriptableObject
{
    public List<AudioClip> audioSamples;
}
