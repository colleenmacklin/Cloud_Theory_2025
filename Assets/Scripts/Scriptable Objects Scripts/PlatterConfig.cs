using UnityEngine;
using FMODUnity;

[CreateAssetMenu(fileName = "NewPlatterConfig", menuName = "Platter/Config")]
public class PlatterConfig : ScriptableObject
{
    [Header("Visuals")]
    public Mesh mesh;
    public Material[] materials;
    [Tooltip("World-space size the mesh's longest axis will be scaled to. Leave at 0 to keep the prefab's own scale.")]
    public float targetSize = 0f;

    [Header("Audio")]
    public EventReference fmodEvent;

    [Range(0f, 1f)]
    public float volume = 1f;
}
