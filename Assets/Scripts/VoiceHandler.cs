using UnityEngine;
using Crosstales.RTVoice;
using Crosstales.RTVoice.Model;
using System;
using System.Collections.Generic;
using TMPro;


public class VoiceHandler : MonoBehaviour
{
    public string Dialogue = "Hello world, I am RT-Voice!";
    public string Culture = "en";
    public bool UseDefaultVoice;
    public bool SpeakWhenReady;
    public AudioSource SpeakerAudio;
    public event Action OnSpeechComplete;
   public TMP_Dropdown allVoiceNames;


    public bool UseNative;
    public bool Running;

    [Range(0f, 3f)] public float Rate   = 1f;
    [Range(0f, 2f)] public float Pitch  = 1f;
    [Range(0f, 1f)] public float Volume = 1f;

    // ── Voice selection ──────────────────────────────────────────────────
    [Header("Voice Selection")]
    [Tooltip("All available system voices. Populated automatically when voices are ready.")]
    public List<string> AvailableVoiceNames = new List<string>();

    [Tooltip("Index into AvailableVoiceNames. Change this in the Inspector to pick a voice.")]
    public int SelectedVoiceIndex = 0;

    [Tooltip("Read-only: name of the currently selected voice.")]
    [SerializeField] private string _currentVoiceName = "(not yet loaded)";

    // ── Private state ────────────────────────────────────────────────────
    private List<Voice> _voices = new List<Voice>();
    private string _uid;
    private bool _isSpeaking = false;

    // ────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        Speaker.Instance.OnVoicesReady  += OnVoicesReady;
        Speaker.Instance.OnSpeakStart   += OnSpeakStart;
        Speaker.Instance.OnSpeakComplete += OnSpeakComplete;
      Actions.ChooseVoice += SetVoiceByName;  
    }

    private void OnDisable()
    {
         Actions.ChooseVoice -= SetVoiceByName;  

        if (Speaker.Instance != null)
        {
            Speaker.Instance.OnVoicesReady   -= OnVoicesReady;
            Speaker.Instance.OnSpeakStart    -= OnSpeakStart;
            Speaker.Instance.OnSpeakComplete -= OnSpeakComplete;
        }
    }

    void Start()
    {
        // Voices may already be ready if this script starts late
        if (Speaker.Instance != null && Speaker.Instance.Voices.Count > 0)
            PopulateVoiceList();
    }

    // ── Voice list population ─────────────────────────────────────────────
    private void PopulateVoiceList()
    {
        _voices.Clear();
        AvailableVoiceNames.Clear();

        foreach (Voice v in Speaker.Instance.Voices)
        {
            _voices.Add(v);
            // Display name: "Name (Culture)" e.g. "Samantha (en-US)"
            AvailableVoiceNames.Add($"{v.Name} ({v.Culture})");
            allVoiceNames.AddOptions(AvailableVoiceNames); //adds name to UI dropdown
        }

        SelectedVoiceIndex = Mathf.Clamp(SelectedVoiceIndex, 0, _voices.Count - 1);
        UpdateCurrentVoiceName();

        Debug.Log($"RT-Voice: {_voices.Count} voices loaded. " +
                  $"Selected: {_currentVoiceName}");
    }

    private void UpdateCurrentVoiceName()
    {
        if (_voices.Count == 0)
        {
            _currentVoiceName = "(no voices loaded)";
            return;
        }
        SelectedVoiceIndex = Mathf.Clamp(SelectedVoiceIndex, 0, _voices.Count - 1);
        _currentVoiceName  = AvailableVoiceNames[SelectedVoiceIndex];
    }

    /// <summary>
    /// Called by the Inspector or other scripts to change voice by index.
    /// Safe to call at runtime between lines of dialogue.
    /// </summary>
    public void SetVoiceIndex(int index)
    {
        SelectedVoiceIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _voices.Count - 1));
        UpdateCurrentVoiceName();
        Debug.Log($"RT-Voice voice → {_currentVoiceName}");
    }

    /// <summary>
    /// Set voice by exact name string (case-insensitive partial match).
    /// Useful from other scripts or UnityEvents.
    /// </summary>
    public void SetVoiceByName(string voiceName)
    {
        string lower = voiceName.ToLower();
        for (int i = 0; i < _voices.Count; i++)
        {
            if (_voices[i].Name.ToLower().Contains(lower))
            {
                SetVoiceIndex(i);
                return;
            }
        }
        Debug.LogWarning($"RT-Voice: no voice found matching '{voiceName}'");
    }

    /// <summary>Returns the currently selected Voice object, or null.</summary>
    public Voice GetSelectedVoice()
    {
        if (_voices.Count == 0) return null;
        return _voices[Mathf.Clamp(SelectedVoiceIndex, 0, _voices.Count - 1)];
    }

    // ── Speaking ──────────────────────────────────────────────────────────
    public void SpeakLine(string text)
    {
        if (_isSpeaking) return;
        _isSpeaking = true;

        Voice voice = ResolveVoice();

        if (UseNative)
        {
            _uid = Speaker.Instance.SpeakNative(text, voice);
        }
        else
        {
            _uid = Speaker.Instance.Speak(text, SpeakerAudio, voice, false, Rate, Pitch, Volume);
        }
    }

    /// <summary>
    /// Resolve which voice to use, in priority order:
    ///   1. SelectedVoiceIndex (if voices are loaded and UseDefaultVoice is false)
    ///   2. Culture fallback (if no voice list yet but culture is set)
    ///   3. Default voice (UseDefaultVoice = true, or nothing else available)
    /// </summary>
    private Voice ResolveVoice()
    {
        if (UseDefaultVoice) return null;

        if (_voices.Count > 0)
            return GetSelectedVoice();

        // Voices not yet loaded — fall back to culture
        if (!string.IsNullOrEmpty(Culture))
            return Speaker.Instance.VoiceForCulture(Culture);

        return null;
    }

    public void Silence()
    {
        StopAllCoroutines();
        if (this != null)
        {
            if (SpeakerAudio != null) SpeakerAudio.Stop();
            Running = false;
        }
        Speaker.Instance.Silence();
        if (this != null) Dialogue = string.Empty;
    }

    // ── RTVoice callbacks ─────────────────────────────────────────────────
    private void OnVoicesReady()
    {
        PopulateVoiceList();

        if (SpeakWhenReady)
            SpeakLine(Dialogue);
    }

    private void OnSpeakStart(Wrapper wrapper)
    {
        if (wrapper.Uid == _uid)
            Debug.Log($"RT-Voice speak started: {wrapper}");
    }

    private void OnSpeakComplete(Wrapper wrapper)
    {
        if (wrapper.Uid == _uid)
        {
            Debug.Log($"RT-Voice speak completed: {wrapper}");
            _isSpeaking = false;
            OnSpeechComplete?.Invoke();
        }
    }

    // ── Editor helper: validate index when changed in Inspector ───────────
    private void OnValidate()
    {
        if (_voices != null && _voices.Count > 0)
            UpdateCurrentVoiceName();
    }
}
