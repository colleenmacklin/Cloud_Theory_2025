using UnityEngine;
using FMODUnity;

[CreateAssetMenu(fileName = "NewPlatterConfig", menuName = "Platter/Config")]
public class PlatterConfig : ScriptableObject
{
    [Header("Visuals")]
    public Mesh mesh;
    public Material[] materials;

    [Header("Audio")]
    public EventReference fmodEvent;

    [Range(0f, 1f)]
    public float volume = 1f;
}
