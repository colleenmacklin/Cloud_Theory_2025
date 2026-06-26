using System;
using UnityEngine;
using FMODUnity;
using FMOD;
using FMOD.Studio;

public class PlatterObject : MonoBehaviour
{
    [Tooltip("Drag a PlatterConfig asset here to set this object's mesh and sound.")]
    public PlatterConfig config;

    [Header("Loudness Scale")]
    [Tooltip("How many times larger the object grows at full loudness, relative to its rest size.")]
    [Range(1f, 10f)] public float hitScaleMultiplier  = 2f;
    [Range(1f, 500f)] public float loudnessSensitivity = 100f;
    [Range(0f, 0.1f)] public float threshold           = 0.01f;
    [Range(1f, 30f)]  public float smoothSpeed         = 10f;

    private Vector3       _minScale;
    private Vector3       _maxScale;
    private EventInstance _instance;
    private DSP           _meterDsp;
    private bool          _meterReady;
    private float         _smoothedLoudness;

    private void Awake() => Apply();

    private void Start()
    {
        _minScale = transform.localScale;
        _maxScale = transform.localScale * hitScaleMultiplier;
    }

    private void Update()
    {
        float loudness = 0f;

        if (_instance.isValid())
        {
            _instance.getPlaybackState(out PLAYBACK_STATE state);

            if (state == PLAYBACK_STATE.STOPPED)
            {
                _instance.release();
                _instance   = default;
                _meterDsp   = default;
                _meterReady = false;
            }
            else
            {
                loudness = GetInstanceLoudness() * loudnessSensitivity;
                if (loudness < threshold) loudness = 0f;
            }
        }

        _smoothedLoudness = Mathf.Lerp(_smoothedLoudness, Mathf.Clamp01(loudness),
                                        Time.deltaTime * smoothSpeed);
        transform.localScale = Vector3.Lerp(_minScale, _maxScale, _smoothedLoudness);
    }

    private float GetInstanceLoudness()
    {
        if (!_meterReady)
        {
            if (_instance.getChannelGroup(out ChannelGroup cg) != RESULT.OK) return 0f;
            if (cg.getDSP(CHANNELCONTROL_DSP_INDEX.HEAD, out _meterDsp) != RESULT.OK) return 0f;
            _meterDsp.setMeteringEnabled(false, true);
            _meterReady = true;
        }

        if (_meterDsp.getMeteringInfo(IntPtr.Zero, out DSP_METERING_INFO info) != RESULT.OK) return 0f;
        if (info.numchannels == 0) return 0f;

        float sumSq = 0f;
        for (int i = 0; i < info.numchannels; i++)
            sumSq += info.rmslevel[i] * info.rmslevel[i];
        return Mathf.Sqrt(sumSq / info.numchannels);
    }

    public void Apply()
    {
        if (config == null) return;

        var mf = GetComponentInChildren<MeshFilter>();
        var mr = GetComponentInChildren<MeshRenderer>();

        if (mf != null && config.mesh != null)
        {
            mf.sharedMesh = config.mesh;

            // auto-scale so the mesh's longest axis matches targetSize
            if (config.targetSize > 0f)
            {
                Vector3 s = config.mesh.bounds.size;
                float longest = Mathf.Max(s.x, s.y, s.z);
                if (longest > 0f)
                    transform.localScale = Vector3.one * (config.targetSize / longest);
            }
        }

        if (mr != null && config.materials != null && config.materials.Length > 0)
            mr.sharedMaterials = config.materials;

        // sync collider to the new mesh so it stays centred and correctly sized
        if (config.mesh != null)
        {
            var mc = GetComponentInChildren<MeshCollider>();
            if (mc != null)
            {
                if (config.mesh.isReadable)
                {
                    mc.sharedMesh = config.mesh;
                }
                else
                {
                    // mesh not marked Read/Write — disable MeshCollider and use a
                    // BoxCollider from bounds instead (bounds work without Read/Write)
                    mc.enabled = false;
                    var bc = mc.gameObject.GetComponent<BoxCollider>();
                    if (bc == null) bc = mc.gameObject.AddComponent<BoxCollider>();
                    bc.isTrigger = mc.isTrigger;
                    bc.center    = config.mesh.bounds.center;
                    bc.size      = config.mesh.bounds.size;
                }
            }
            else
            {
                var bc = GetComponentInChildren<BoxCollider>();
                if (bc != null)
                {
                    bc.center = config.mesh.bounds.center;
                    bc.size   = config.mesh.bounds.size;
                }
            }
        }
    }

    public void Trigger()
    {
        if (config == null || config.fmodEvent.IsNull) return;

        // stop previous instance if still playing
        if (_instance.isValid())
        {
            _instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _instance.release();
            _meterDsp   = default;
            _meterReady = false;
        }

        _instance = RuntimeManager.CreateInstance(config.fmodEvent);
        _instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
        _instance.setVolume(config.volume);
        _instance.start();
        // intentionally not calling release() here — we hold it for metering
    }

    private void OnDestroy()
    {
        if (_instance.isValid())
        {
            _instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _instance.release();
        }
    }
}
