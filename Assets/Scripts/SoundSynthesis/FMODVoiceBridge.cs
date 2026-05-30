using System;
using System.Runtime.InteropServices;
using UnityEngine;
using FMODUnity;

/// <summary>
/// Captures fully-processed RTVoice audio from the end of Unity's DSP chain
/// (after AutotuneFilter and VocoderFilter) and streams it through FMOD's mixer.
///
/// Setup:
///   1. Add to the same GameObject as SpeakerAudio (where AutotuneFilter lives).
///   2. Move this component BELOW AutotuneFilter and VocoderFilter in the Inspector
///      so it runs last in the filter chain.
///   3. Set BusPath to an FMOD Studio bus (e.g. "bus:/Voice"). Leave empty for master.
///   4. Unity's AudioSource will be silenced — FMOD handles playback.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FMODVoiceBridge : MonoBehaviour
{
    [Header("FMOD Routing")]
    [Tooltip("FMOD Studio bus path (e.g. \"bus:/Voice\"). Leave empty to use master.")]
    public string BusPath = "bus:/Voice";

    [Range(0f, 3f)] public float Volume = 1f;

    [Header("Debug")]
    [Tooltip("When true, audio passes through Unity's AudioSource unchanged instead of being routed to FMOD. Use this to verify RTVoice is working before enabling FMOD routing.")]
    public bool BypassFMOD = false;

    // Ring buffer shared between Unity audio thread (write) and FMOD mixer thread (read).
    // Lock-free single-producer / single-consumer is safe here.
    private const int RingSize = 131072; // ~1.5 s at 44100 stereo
    private float[] _ring = new float[RingSize];
    private volatile int _writePos;
    private volatile int _readPos;

    private FMOD.Sound   _stream;
    private FMOD.Channel _channel;
    private GCHandle     _selfHandle;

    // Delegates must be held as fields so the GC doesn't collect them while FMOD holds the pointer.
    private FMOD.SOUND_PCMREAD_CALLBACK   _pcmReadCb;
    private FMOD.SOUND_PCMSETPOS_CALLBACK _pcmSetPosCb;

    private int _numChannels;
    private int _sampleRate;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        // Ask FMOD for its output format — more reliable than Unity's AudioSettings
        // when Unity audio output is reduced or disabled in favour of FMOD.
        RuntimeManager.CoreSystem.getSoftwareFormat(out _sampleRate, out _, out _);
        if (_sampleRate <= 0) _sampleRate = 44100;

        // FMOD's software mixer is always stereo unless the project changes it.
        _numChannels = 2;

        _selfHandle  = GCHandle.Alloc(this);
        _pcmReadCb   = PcmReadCallback;
        _pcmSetPosCb = PcmSetPosCallback;

        CreateStream();
    }

    void Update()
    {
        if (_channel.hasHandle())
            _channel.setVolume(Volume);
    }

    void OnDestroy()
    {
        if (_channel.hasHandle()) _channel.stop();
        if (_stream.hasHandle())  _stream.release();
        if (_selfHandle.IsAllocated) _selfHandle.Free();
    }

    // ── Unity DSP chain — last filter runs here ───────────────────────────

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (BypassFMOD) return; // let Unity play it normally for debugging

        // Write fully-processed audio into the ring buffer.
        for (int i = 0; i < data.Length; i++)
        {
            _ring[_writePos] = data[i];
            _writePos = (_writePos + 1) % RingSize;
        }

        // Silence Unity output so FMOD is the sole playback path.
        Array.Clear(data, 0, data.Length);
    }

    // ── FMOD PCM read callback (FMOD mixer thread) ────────────────────────

    [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMREAD_CALLBACK))]
    private static FMOD.RESULT PcmReadCallback(IntPtr soundRaw, IntPtr dataRaw, uint length)
    {
        FMOD.Sound s = new FMOD.Sound(soundRaw);
        s.getUserData(out IntPtr ptr);
        if (ptr == IntPtr.Zero) return FMOD.RESULT.OK;

        FMODVoiceBridge self = GCHandle.FromIntPtr(ptr).Target as FMODVoiceBridge;
        if (self == null) return FMOD.RESULT.OK;

        int floatCount = (int)(length / sizeof(float));
        float[] tmp = new float[floatCount];

        for (int i = 0; i < floatCount; i++)
        {
            int avail = (self._writePos - self._readPos + RingSize) % RingSize;
            if (avail > 0)
            {
                tmp[i] = self._ring[self._readPos];
                self._readPos = (self._readPos + 1) % RingSize;
            }
            // else tmp[i] stays 0f (silence when starved)
        }

        Marshal.Copy(tmp, 0, dataRaw, floatCount);
        return FMOD.RESULT.OK;
    }

    // Required by FMOD when LOOP_NORMAL is set — called when the stream loops.
    // Since we are a live ring-buffer stream (not pre-recorded), seeking is a no-op.
    [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMSETPOS_CALLBACK))]
    private static FMOD.RESULT PcmSetPosCallback(IntPtr sound, int subsound, uint position, FMOD.TIMEUNIT postype)
        => FMOD.RESULT.OK;

    // ── Stream creation ───────────────────────────────────────────────────

    private void CreateStream()
    {
        FMOD.System core = RuntimeManager.CoreSystem;

        FMOD.CREATESOUNDEXINFO info = new FMOD.CREATESOUNDEXINFO();
        info.cbsize            = Marshal.SizeOf(info);
        info.numchannels       = _numChannels;
        info.defaultfrequency  = _sampleRate;
        info.format            = FMOD.SOUND_FORMAT.PCMFLOAT;
        info.length            = (uint)(_sampleRate * _numChannels * sizeof(float)); // 1-second loop
        info.pcmreadcallback   = _pcmReadCb;
        info.pcmsetposcallback = _pcmSetPosCb;
        info.userdata          = GCHandle.ToIntPtr(_selfHandle);

        Debug.Log($"[FMODVoiceBridge] Creating stream — rate={_sampleRate} ch={_numChannels} len={info.length}");

        FMOD.RESULT r = core.createSound(
            IntPtr.Zero,
            FMOD.MODE.OPENUSER | FMOD.MODE.CREATESTREAM | FMOD.MODE.LOOP_NORMAL,
            ref info, out _stream);

        if (r != FMOD.RESULT.OK)
        {
            Debug.LogError($"[FMODVoiceBridge] createSound failed: {r}");
            return;
        }

        FMOD.ChannelGroup group = default;
        if (!string.IsNullOrEmpty(BusPath))
        {
            if (RuntimeManager.StudioSystem.getBus(BusPath, out FMOD.Studio.Bus bus) == FMOD.RESULT.OK)
                bus.getChannelGroup(out group);
            else
                Debug.LogWarning($"[FMODVoiceBridge] Bus '{BusPath}' not found — using master.");
        }

        r = core.playSound(_stream, group, false, out _channel);
        if (r != FMOD.RESULT.OK)
            Debug.LogError($"[FMODVoiceBridge] playSound failed: {r}");
        else
            _channel.setVolume(Volume);
    }
}
