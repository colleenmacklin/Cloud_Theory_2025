using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends narration text to the local RVC service (Python/rvc_service.py),
/// receives a WAV file back, and plays it through an AudioSource.
///
/// Pipeline:
///   text  →  rvc_service.py (edge-tts + RVC)  →  WAV  →  AudioSource
///            ↑ localhost:8765                              ↑ AutotuneFilter sits here
///
/// Setup:
///   1. Run Python/rvc_service.py (pip install -r Python/requirements.txt first).
///   2. Add this component to a new GameObject.
///   3. Add an AudioSource to the same GameObject (it is auto-found via RequireComponent).
///   4. Add AutotuneFilter to the same GameObject, set ChordSource = your CloudChordPlayer.
///   5. In NewNarrator, assign this GameObject to the RvcVoice field.
///
/// The service runs in TTS-only mode if no RVC model is installed — useful for
/// testing the pipeline before you have a voice model.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RVCVoiceConverter : MonoBehaviour
{
    // ── Service connection ─────────────────────────────────────────────────
    [Header("Service")]
    [Tooltip("URL of the local RVC service. Run Python/rvc_service.py first.")]
    public string ServiceUrl = "http://127.0.0.1:8765/speak";

    [Tooltip("edge-tts voice name used as the base TTS voice.\n" +
             "Examples: en-US-AriaNeural, en-US-JennyNeural, en-GB-SoniaNeural\n" +
             "Full list: run 'edge-tts --list-voices' in a terminal.")]
    public string Voice = "en-US-AriaNeural";

    [Tooltip("Speech rate offset. '+10%' speeds up, '-10%' slows down.")]
    public string Rate = "+0%";

    [Tooltip("Base pitch offset applied by edge-tts before RVC. '+5Hz' lifts, '-5Hz' lowers.")]
    public string BasePitch = "+0Hz";

    [Tooltip("RVC semitone shift. 0 = no shift. +12 = one octave up (useful for higher voices).")]
    [Range(-24, 24)]
    public int F0UpKey = 0;

    [Tooltip("RVC pitch extraction algorithm. 'rmvpe' is most accurate; 'crepe' is slower but smoother.")]
    public string F0Method = "rmvpe";

    [Tooltip("Seconds before a network request times out.")]
    public int TimeoutSeconds = 30;

    // ── Behaviour ──────────────────────────────────────────────────────────
    [Header("Behaviour")]
    [Tooltip("If the service is unreachable, log a warning and fire OnSpeechComplete rather than hanging.")]
    public bool ContinueOnError = true;

    // ── Events ─────────────────────────────────────────────────────────────
    /// <summary>Fired when the clip finishes playing (mirrors VoiceHandler.OnSpeechComplete).</summary>
    public event Action OnSpeechComplete;

    // ── Private ────────────────────────────────────────────────────────────
    private AudioSource _source;
    private string      _tempWavPath;
    private bool        _busy;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        _source      = GetComponent<AudioSource>();
        _source.loop = false;
        _tempWavPath = Path.Combine(Application.temporaryCachePath, "rvc_out.wav");
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Convert <paramref name="text"/> to a singing voice and play it.
    /// Fires <see cref="OnSpeechComplete"/> when the clip ends.
    /// Calls while already speaking are silently ignored.
    /// </summary>
    public void SpeakLine(string text)
    {
        if (_busy || string.IsNullOrWhiteSpace(text)) return;
        StartCoroutine(SpeakRoutine(text));
    }

    /// <summary>Stop any current playback immediately.</summary>
    public void Silence()
    {
        StopAllCoroutines();
        _source.Stop();
        _busy = false;
    }

    // ── Core coroutine ─────────────────────────────────────────────────────
    private IEnumerator SpeakRoutine(string text)
    {
        _busy = true;

        // 1. Build JSON payload.
        string json = JsonUtility.ToJson(new Payload
        {
            text       = text,
            voice      = Voice,
            rate       = Rate,
            pitch      = BasePitch,
            f0_up_key  = F0UpKey,
            f0_method  = F0Method,
        });

        // 2. POST to service and receive WAV bytes.
        byte[] wavBytes = null;
        using (var req = new UnityWebRequest(ServiceUrl, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = TimeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                var msg = $"RVCVoiceConverter: service error — {req.error}. " +
                          "Is Python/rvc_service.py running?";
                if (ContinueOnError)
                    Debug.LogWarning(msg);
                else
                    Debug.LogError(msg);

                _busy = false;
                OnSpeechComplete?.Invoke();
                yield break;
            }

            wavBytes = req.downloadHandler.data;
        }

        // 3. Write WAV to temp file so UnityWebRequest can load it as an AudioClip.
        File.WriteAllBytes(_tempWavPath, wavBytes);

        // 4. Load AudioClip from temp file.
        AudioClip clip = null;
        var fileUri = new Uri(_tempWavPath).AbsoluteUri;
        using (var clipReq = UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.WAV))
        {
            ((DownloadHandlerAudioClip)clipReq.downloadHandler).streamAudio = false;
            yield return clipReq.SendWebRequest();

            if (clipReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"RVCVoiceConverter: WAV load failed — {clipReq.error}");
                _busy = false;
                OnSpeechComplete?.Invoke();
                yield break;
            }

            clip = DownloadHandlerAudioClip.GetContent(clipReq);
        }

        // 5. Play and wait for the clip to finish.
        _source.clip = clip;
        _source.Play();

        while (_source.isPlaying)
            yield return null;

        _busy = false;
        OnSpeechComplete?.Invoke();
    }

    // ── JSON payload ───────────────────────────────────────────────────────
    [Serializable]
    private class Payload
    {
        public string text;
        public string voice;
        public string rate;
        public string pitch;
        public int    f0_up_key;
        public string f0_method;
    }
}
