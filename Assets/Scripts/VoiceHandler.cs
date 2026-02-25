using UnityEngine;
using Crosstales.RTVoice;
using Crosstales.RTVoice.Model;
using NUnit.Framework.Internal;
using System;
using System.Collections;
using Unity.VisualScripting;


public class VoiceHandler : MonoBehaviour

{

   public string Dialogue = "Hello world, I am RT-Voice!";
   public string Culture = "en";
   public bool UseDefaultVoice;
   public bool SpeakWhenReady;
   public AudioSource SpeakerAudio;
    public event Action OnSpeechComplete;
    private bool isSpeaking = false;


   public bool UseNative;
    public bool Running;

    [Range(0f, 3f)] public float Rate = 1f;

    [Range(0f, 2f)] public float Pitch = 1f;

    [Range(0f, 1f)] public float Volume = 1f;

   private string uid; //Unique id of the speech
    private bool playing;


   private void OnEnable()
   {
      // Subscribe event listeners
      Speaker.Instance.OnVoicesReady += voicesReady;
      Speaker.Instance.OnSpeakStart += speakStart;
      Speaker.Instance.OnSpeakComplete += speakComplete;
   }

   private void OnDisable()
   {
      if (Speaker.Instance != null)
      {
         // Unsubscribe event listeners
         Speaker.Instance.OnVoicesReady -= voicesReady;
         Speaker.Instance.OnSpeakStart -= speakStart;
         Speaker.Instance.OnSpeakComplete -= speakComplete;
      }
   }

   void Start()
   {
      //SpeakLine("hey Colleen, let's make this game amazing!");
   }

    public void SpeakLine(string text)
    {
        if (isSpeaking) return;
        
        isSpeaking = true;
        
        // RTVoice speak with callback
        //Speaker.Instance.Speak(text, null, Speaker.Instance.VoiceForCulture("en"), true, 1f, 1f, 1f, "", OnSpeakComplete);
        if (UseNative)
      {
         uid = Speaker.Instance.SpeakNative(text, UseDefaultVoice ? null : Speaker.Instance.VoiceForCulture(Culture)); //Speak (native TTS) with the first voice matching the given culture or the default voice
      }
      else
      {
         uid = Speaker.Instance.Speak(text, SpeakerAudio, UseDefaultVoice ? null : Speaker.Instance.VoiceForCulture(Culture)); //Speak (audio file) with the first voice matching the given culture or the default voice
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
      if (wrapper.Uid == uid) //Only write the log message if it's "our" speech
         Debug.Log($"RT-Voice: speak completed: {wrapper}");
         //TODO:
         //action to tell narrator and subtitle text that the line has been spoken, and to go to the next line, if available
        isSpeaking = false;
        OnSpeechComplete?.Invoke(); // Notify Narrator
        Debug.Log("Speak Complete");
        //playing = false;
        //Actions.DoneSpeaking();
   }
   }
