using UnityEngine;
using FMODUnity;

public class PlatterObject : MonoBehaviour
{
    [Tooltip("Drag a PlatterConfig asset here to set this object's mesh and sound.")]
    public PlatterConfig config;

    private void Awake()
    {
        if (config == null) return;

        var mf = GetComponentInChildren<MeshFilter>();
        var mr = GetComponentInChildren<MeshRenderer>();

        if (mf != null && config.mesh != null)
            mf.sharedMesh = config.mesh;

        if (mr != null && config.materials != null && config.materials.Length > 0)
            mr.sharedMaterials = config.materials;
    }

    public void Trigger()
    {
        if (config == null || config.fmodEvent.IsNull) return;

        var inst = RuntimeManager.CreateInstance(config.fmodEvent);
        inst.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
        inst.setVolume(config.volume);
        inst.start();
        inst.release();
    }
}
