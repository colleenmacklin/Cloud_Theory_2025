using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class Actions
{
    // Contains all the actions for our game!!

    public static Action SharpenCloud;
    public static Action BlurCloud;
    public static Action ClarifyClouds;
    public static Action SlowdownClouds;
    public static Action Cutscene;
    public static Action ConversationEnded;
    public static Action<GameObject> GetClickedCloud;
    public static Action<CloudShape> CloudIsReady;
    public static Action<OpeningCloudShape> OpeningCloudIsReady;
    public static Action<GameObject> FadeInCloud;
    public static Action<GameObject> FadeOutCloud;
    public static Action<String> GetModel;
    public static Action<List<string>> SetEndingClouds;
    public static Action<TextAsset> SetStory;
    public static Action MoveButterfly;
    public static Action<GameObject> Destroy;
    public static Action Shake;
    public static Action <GameObject> OnHoverOverTargetCloud;
    public static Action OnHoverExit;
    public static Action Setup;
    public static Action Respond;
    public static Action DoneReading;
    public static Action StopClouds;
    public static Action IntroDone;
    public static Action Musing;
    public static Action Correct;
    public static Action Sunrise;
    public static Action Sunset;
    public static Action EndingConclusion;
    public static Action CloudsGenerated;
    public static Action Talk;
    public static Action TurnOffCloud;
    public static Action SpawnShape;
    public static Action FadeInSpace;
    public static Action FadeOutSpace;
    
    
    //testscene actions
    public static Action <string> RespondToShape;
}
