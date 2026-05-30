using UnityEngine;
using Crosstales.RTVoice;
using Crosstales.RTVoice.Model;
using NUnit.Framework.Internal;
using System;
using System.Collections;
using Unity.VisualScripting;
using System.Collections.Generic;


public class VoiceHandler : MonoBehaviour

{

public string Dialogue = "Hello world, I am RT-Voice!";
public string Culture = "en";
public bool UseDefaultVoice;
public bool SpeakWhenReady;
public AudioSource SpeakerAudio;
public event Action OnSpeechComplete;
private bool isSpeaking = false;
public TMPro.TMP_Dropdown voiceDropdown;
public string currentVoiceName;


public bool UseNative;
public bool Running;
public bool SkipDropdown;

[Range(0f, 3f)] public float Rate = 1f;

[Range(0f, 2f)] public float Pitch = 1f;

[Range(0f, 1f)] public float Volume = 1f;

private string uid; //Unique id of the speech
private bool playing;
    // ── Voice selection (inspector-only) ─────────────────────────────────
    // These are public so VoiceHandlerEditor can read and write them,
    // but you interact with them through the custom Inspector dropdown —
    // not by editing the raw fields directly.
    [HideInInspector] public List<string> VoiceNames  = new List<string>();
    [HideInInspector] public int          VoiceIndex  = 0;

    // Shown read-only in the inspector by the custom editor
    [HideInInspector] public string SelectedVoiceName = "(not yet loaded)";
    private List<Voice> _voices = new List<Voice>();


private void OnEnable()
{
   Actions.ChooseVoice += SetVoice;
Speaker.Instance.OnVoicesReady += voicesReady;
Speaker.Instance.OnSpeakStart += speakStart;
Speaker.Instance.OnSpeakComplete += speakComplete;
Speaker.Instance.OnErrorInfo += speakError;
}

private void OnDisable()
{
if (Speaker.Instance != null)
{
Actions.ChooseVoice -= SetVoice;
Speaker.Instance.OnVoicesReady -= voicesReady;
Speaker.Instance.OnSpeakStart -= speakStart;
Speaker.Instance.OnSpeakComplete -= speakComplete;
Speaker.Instance.OnErrorInfo -= speakError;
}
}

void Start()
{
      // Removed startup test line — calling Speak before voices are ready causes RTVoice
      // to error without firing OnSpeakComplete, permanently locking isSpeaking = true.
        // RTVoice may have already fired OnVoicesReady before this component
        // enabled (common when Speaker is a scene singleton that starts first).
        // Poll as a fallback, and keep trying via coroutine until voices arrive.
        if (Speaker.Instance != null && Speaker.Instance.Voices.Count > 0)
            PopulateVoiceList();
        else
            StartCoroutine(WaitForVoices());

        // Warn early if SpeakerAudio is missing — avoids silent failures
        if (SpeakerAudio == null && !UseNative)
            Debug.LogWarning("VoiceHandler: SpeakerAudio is not assigned. " +
                             "Assign an AudioSource or enable UseNative.");
}
    private System.Collections.IEnumerator WaitForVoices()
    {
        // Keeps checking until RTVoice finishes enumerating voices.
        // Covers async providers (Azure, Google Cloud) that take longer
        // than one frame, and the common race where Speaker starts first.
        while (Speaker.Instance == null || Speaker.Instance.Voices.Count == 0)
            yield return new WaitForSeconds(0.25f);

        PopulateVoiceList();
    }
    // ── Voice list ────────────────────────────────────────────────────────
    public void PopulateVoiceList()
    {
        _voices.Clear();
        VoiceNames.Clear();
      List<string> menu_voices = new List<string>();

        foreach (Voice v in Speaker.Instance.Voices)
        {
            _voices.Add(v);
            VoiceNames.Add($"{v.Name}  [{v.Culture}]");
            menu_voices.Add($"{v.Name}  [{v.Culture}]");
        }
        if (!SkipDropdown && voiceDropdown != null)
        {
            voiceDropdown.ClearOptions();
            voiceDropdown.AddOptions(menu_voices);
            voiceDropdown.onValueChanged.RemoveAllListeners();
            voiceDropdown.onValueChanged.AddListener(index =>
            {
                VoiceIndex = index;
                RefreshSelectedName();
            });
            VoiceIndex = Mathf.Clamp(VoiceIndex, 0, Mathf.Max(0, _voices.Count - 1));
            voiceDropdown.SetValueWithoutNotify(VoiceIndex);
        }
        else
        {
            VoiceIndex = Mathf.Clamp(VoiceIndex, 0, Mathf.Max(0, _voices.Count - 1));
        }
        RefreshSelectedName();
        Debug.Log($"RT-Voice: {_voices.Count} voices ready. Selected: {SelectedVoiceName}");
    }

    public void RefreshSelectedName()
    {
        SelectedVoiceName = (VoiceNames.Count > 0 && VoiceIndex < VoiceNames.Count)
            ? VoiceNames[VoiceIndex]
            : "(no voices loaded)";
    }

    public Voice GetSelectedVoice()
    {
        if (_voices.Count == 0) return null;
        return _voices[Mathf.Clamp(VoiceIndex, 0, _voices.Count - 1)];
    }

public void SetVoice(string voiceName)
   {
         foreach (Voice v in Speaker.Instance.Voices)
         {
               if (voiceName == $"{v.Name}  [{v.Culture}]")
               {
                  Debug.Log($"VoiceHandler: setting voice to {voiceName}");
                  VoiceIndex = Speaker.Instance.Voices.IndexOf(v);
                  RefreshSelectedName();
                  return;
               }
         }
         Debug.LogWarning($"VoiceHandler: could not find voice named {voiceName}");
   }


public void SpeakLine(string text)
{
if (isSpeaking) return;

isSpeaking = true;

// RTVoice speak with callback
Voice selectedVoice = UseDefaultVoice ? null : GetSelectedVoice();
if (UseNative)
{
uid = Speaker.Instance.SpeakNative(text, selectedVoice);
}
else
{
uid = Speaker.Instance.Speak(text, SpeakerAudio, selectedVoice);
}


}

private void OnSpeakComplete(string uid)
{
isSpeaking = false;
OnSpeechComplete?.Invoke(); // Notify Narrator
}

/*
  public IEnumerator SpeakLine(string line)
  {
       if (UseNative)
     {
        uid = Speaker.Instance.SpeakNative(line, UseDefaultVoice ? null : Speaker.Instance.VoiceForCulture(Culture)); //Speak (native TTS) with the first voice matching the given culture or the default voice
     }
     else
     {
        uid = Speaker.Instance.Speak(line, SpeakerAudio, UseDefaultVoice ? null : Speaker.Instance.VoiceForCulture(Culture)); //Speak (audio file) with the first voice matching the given culture or the default voice
     }

       if (!Running)
       {
           Running = true;
           playing = false;
           
           while(Running){
           
               yield return null;

           }
           do
              {
                 yield return null;
              } while (!playing && Running);

              //wait until played
           do
              {
                 yield return null;
              } while (playing && Running);

           Dialogue = string.Empty;

           yield return null;

           Running = false;

       }

  }
  */

public void Silence()
{
StopAllCoroutines();
if (this != null)
{
if (this.SpeakerAudio != null)
this.SpeakerAudio.Stop();
this.Running = false;
}
Speaker.Instance.Silence();
if (this != null)
this.Dialogue = string.Empty;
}


private void voicesReady()
{
Debug.Log($"RT-Voice: {Speaker.Instance.Voices.Count} voices are ready to use!");

if (SpeakWhenReady) //Speak after the voices are ready
SpeakLine(Dialogue);
}

private void speakStart(Wrapper wrapper)
{
if (wrapper.Uid == uid) //Only write the log message if it's "our" speech
Debug.Log($"RT-Voice: speak started: {wrapper}");
//playing = true;
}

private void speakComplete(Wrapper wrapper)
{
if (wrapper.Uid == uid)
Debug.Log($"RT-Voice: speak completed: {wrapper}");
isSpeaking = false;
OnSpeechComplete?.Invoke();
}

private void speakError(Wrapper wrapper, string error)
{
// RTVoice fires OnErrorInfo instead of OnSpeakComplete on failure,
// so we must reset isSpeaking here or all future speech calls are blocked.
if (wrapper.Uid == uid)
{
Debug.LogWarning($"RT-Voice error (resetting isSpeaking): {error}");
isSpeaking = false;
}
}
}