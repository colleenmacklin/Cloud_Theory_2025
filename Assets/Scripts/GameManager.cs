using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum State
{
    Intro,
    GameLoop,
    Climax
}
public class GameManager : MonoBehaviour
{
    [SerializeField]
    private ObjectManager _objectManager;

    [SerializeField]
    private CharacterManager _characterManager;

    //[SerializeField]
    //private DialogueManager _dialogueManager;

    public static State CurrentGameState { get; private set; }

    private void Awake()
    {
        CurrentGameState = State.Intro;
        _objectManager.OnGameLoopFinished += TriggerClimax;
        //_dialogueManager.OnDialogueReady += OnDialogueReady; //TODO: Check this
        //_objectManager.OnFallingObjectsOrdered += SetUpDialogues;
        _objectManager.OnNextObjectToFall += GetDialogueforNextFallingObject; //TODO: this calls via a loop - break

        //TODO: add an action on Object Manager to prepare an object to fall...?

        _objectManager.OnObjectFallen += ObjectHasFallen;
        //_dialogueManager.OnCharacterSpeak += CharacterSpeak;
       // _dialogueManager.OnFinishedSpeaking += CharacterFinishSpeaking;
        //_dialogueManager.OnObjectDialogueFinished += DialogueFinished;
        //_dialogueManager.OnSpeakerSetup += SetUpSpeakers;
       // _characterManager.OnStartDialogue += StartDialogue;
        //_characterManager.OnFinishedTypingRemark += OnFinishedTypingRemark;

    }

    private void Update()
    {
        //test keys for state!
        /*
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetGameState(State.GameLoop);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetGameState(State.Climax);
        }
        */
    }
    private void TriggerClimax()
    {
        SetGameState(State.Climax);
    }


    private void OnDialogueReady(Dialogue dialogue) //This should call when livedialogues have been retrieved
    {
        if (CurrentGameState == State.Intro)
        {
            Debug.Log("DIALOGUE READY.........");
            SetGameState(State.GameLoop);
            //TODO: this starts off the object manager to start dropping objects on a timer - in the UPDATE Function though.
        }

        //_characterManager.SetDialogueReady(dialogue); 
       
    }


    public void SetGameState(State state)
    {
        switch (state)
        {
            case State.Intro:
                CurrentGameState = State.Intro;
                Debug.Log("GameState is Intro");
                break;

            case State.GameLoop:
                CurrentGameState = State.GameLoop;
                Debug.Log("GameState is GameLoop");

                break;

            case State.Climax:
                CurrentGameState = State.Climax;
                //_characterManager.SendCharactersToRoof();
                //_objectManager.SendObjectsToRoof();
                Debug.Log("GameState is Climax");

                break;
        }
    }

    private void GetDialogueforNextFallingObject(FallingObject obj) //Called from ObjectManager update loop
    {
        //TODO: test, make sure this fires
        //tested - does not!
        Debug.Log("GameManager has called GetLiveRemarks for:" + obj.name);
        //_dialogueManager.GetLiveRemarks(obj); //this happens right after an object falls, to load the liveremarks for the next object in the sequence before the object falls

    }

    private void ObjectHasFallen(FallingObject obj)
    {
        //_characterManager.MoveCharactersTowardsFallenObject(obj);
        //_dialogueManager.DialogueReady()
    }
/*
    private void CharacterSpeak(CharacterType friend, string text)
    {
        _characterManager.CharacterSpeak(friend, text);
    }
    
    private void CharacterFinishSpeaking(CharacterType friend)
    {
        _characterManager.CharacterFinishSpeaking(friend);
    }

    private void DialogueFinished()
    {
        _characterManager.OnDialogueFinished();
    }
    private void SetUpSpeakers(FallingObject obj)
    {
        List<Character> speakerList = _characterManager.setUpSpeakers(obj);
        _dialogueManager.SetUpDialogues(speakerList);
    }

    private void SetUpDialogues(List<GameObject> objs)
    {
        _dialogueManager.CreateDialogues(objs);
    }

    private void StartDialogue(Dialogue dialogue)
    {
        _dialogueManager.StartDialogue(dialogue);
    }

    private void OnFinishedTypingRemark()
    {
        _dialogueManager.CharacterFinishedSpeaking();
    }
        */

    
}
