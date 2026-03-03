using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


public static class Actions
{
    // Contains all the actions for our game!!

//Cloud Actions
    public static Action SharpenCloud;
    public static Action BlurCloud;
    public static Action ClarifyClouds;
    public static Action SlowdownClouds;
    public static Action Cutscene;
    public static Action ConversationEnded;
    public static Action<GameObject> GetClickedCloud;
    public static Action<CloudShape> CloudIsReady;
    public static Action<float> SetRandomScale;


    //visual controls
    public static Action<GameObject> FadeIn;
    public static Action<GameObject> FadeOut;
    public static Action<GameObject> FadeInCloud;
    public static Action<GameObject> FadeOutCloud;
    public static Action<GameObject> FadeIn50;
    public static Action<GameObject> FadeOut50;
    public static Action Shake;
    public static Action<Camera> LookAtCamera;

    public static Action<String> GetModel;
    public static Action<List<string>> SetEndingClouds;
    public static Action<TextAsset> SetStory;
    public static Action<GameObject> Destroy;
    public static Action <GameObject> OnHoverOverTargetCloud;
    public static Action OnHoverExit;
    public static Action Setup;
    public static Action Respond;
    public static Action StopClouds;
    public static Action IntroDone;
    public static Action Correct;
    public static Action EndingConclusion;
    public static Action Speak;
    public static Action DoneSpeaking;
    public static Action TurnOffCloud;
    public static Action SpawnShape;
    public static Action FadeInSpace;
    public static Action FadeOutSpace;
    public static Action<Texture2D> ChangeCloudShape;
    public static Action<CloudShape> InactivateCloud;
    public static Action<float> OnHoverDwellProgress;
    
    //time transitions
    public static Action Sunrise;
    public static Action Sunset;

    // inter-actions
    public static Action Highlight;

    //testscene actions
    public static Action <string> ChooseTheme;
    public static Action <string> ChooseCloud; //sends name of cloud to Narrator, etc...
}
