using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SpeechBubble : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _textBox;

    [SerializeField]
    private Canvas _canvas;

    [SerializeField]
    private Image _Speechbubble_image;


    private float _counddownDuration = 30;

    private float _timer = 0;

    private bool _textIsShowing;

    public Camera camera;


    private void Awake()
    {
        HideSpeechBubble();

        //make sure it 
    }

    //public void SetC

    private void Update()
    {
        //removing this and putting it up a level on new pivot object so it rotates on the pivot of the bubble
        //speech bubbles face camera always 
        //transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
       

        //commenting this out so the dialogue manager can handle hiding the speech bubbles using the pause or when the audio is finished once that gets implemented
        //countdown timer 
        /*if (_textIsShowing)
        {
            if (_timer > 0)
            {
                _timer -= Time.deltaTime;
            }
            else
            {
                HideSpeechBubble();
            }
        }*/

        ////for testing, to be removed
        
        if (Input.GetKeyDown(KeyCode.H))
        {
            HideSpeechBubble();
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            ShowSpeechBubble();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            ShowSpeechBubble();
            Speak("\"But listen, mother,\" said Laura. Breathless, half-choking, she told the dreadful story. \n\"Of course, we can't have our party, can we?\" she pleaded. \"The band and everybody arriving. They'd hear us, mother; they're nearly neighbours!\" \n");
        }
        
    }


    public void Speak(string text)
    {
       _textBox.text = text;
       
        if (!_canvas.enabled)
        {
            ShowSpeechBubble();
           
        }
    }


    private void ShowSpeechBubble()
    {
        //_Speechbubble_image.
        _canvas.enabled = true;
        _textIsShowing = true;
        _timer = _counddownDuration;
    }

    public void HideSpeechBubble()
    {
        _canvas.enabled = false;
        _textIsShowing = false;
    }


    
}
