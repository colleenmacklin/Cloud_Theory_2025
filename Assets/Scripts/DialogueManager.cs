using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Xml;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using System.Text;
using TMPro;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine.Windows;
using System.Net.NetworkInformation;
using Unity.VisualScripting;
using Input = UnityEngine.Input;
using LLMUnity;


public class DialogueManager : MonoBehaviour
{

    [SerializeField]
    [Tooltip("Pause between sending new remarks")]
    [Range(0, 25)]
    public float conversational_pause = 30;

    [SerializeField]
    public bool DialogueReady;

    //[SerializeField]
    //public LLMCharacter llmCharacter;

    [SerializeField]
    public int max_remark_length;

    [SerializeField]
    public int DialogueNum;

    [SerializeField]
    private ChatHandler _chatHandler;

    [SerializeField]
    private List<string> _prompts;

    private List<FallingObject> _fallingObjects = new List<FallingObject>();

    private List<Dialogue> _dialogues = new List<Dialogue>();

    [SerializeField]
    private int _readyDialogueNum;

    private bool _characterIsSpeaking;

    //[SerializeField]
    public StringBuilder all_remarks = new StringBuilder();

    [SerializeField]
    private GameObject _sideBarCanvas;

    private int total_remarks = 0;
    public GameObject DialogueText;
   
    private int currentIndex;

    private Dialogue currentDialogue;


    //Actions
    public event Action<Dialogue> OnDialogueReady;
    public event Action<CharacterType, string> OnCharacterSpeak;
    public event Action<CharacterType> OnFinishedSpeaking;
    public event Action OnObjectDialogueFinished;
    public event Action<FallingObject> OnSpeakerSetup;
    //public event Action<FallingObject> OnDialogueSetup;
    public event Action<FallingObject> GetDialogue; //TODO: Migrate from HF Handler

   


    void OnEnable()
    {
        //_chatHandler.HasReceivedResponse += AddResponseToDialogue;
    }

    void OnDisable()
    {
        //_chatHandler.HasReceivedResponse -= AddResponseToDialogue;
    }


    private void Awake()
    {
        //hide the side bar on awake
        _sideBarCanvas.SetActive(false);
        /*
        _prompts.Add("What is that _ doing");
        _prompts.Add("Why did that _ fall");
        _prompts.Add("Look! A _");
        _prompts.Add("Now this rave is making it rain _s! I wonder");
        */
        _prompts.Add("A _ just fell from the sky! Why do you think that happened? Include the terms _ and ~ in your response.");
        _prompts.Add("Look! a _! What does it symbolize? Include the terms _ and ~ in your response.");
        _prompts.Add("Who just dropped that _? Include the terms _ and ~ in your response.");
        _prompts.Add("Now this rave is making it rain _s! Connect this to the concept of ~.");


    }



    private void Update()
    {
        /*
        if (Input.GetKeyDown(KeyCode.D))
        {
            Debug.Log("Dialogue Check: " + _dialogues[0]);
        }
        */

        if (Input.GetKeyDown(KeyCode.Z))
        {
            //hide or show side bar 

            if (_sideBarCanvas.activeSelf == true)
            {
                _sideBarCanvas.SetActive(false);
            }
            else
            {
                _sideBarCanvas.SetActive(true);
            }
        }
    }

    public void SetCharacterSpeakingFlag()
    {
        if (!_characterIsSpeaking)
        {
            _characterIsSpeaking = true;
        }
    }
    public void CharacterFinishedSpeaking()
    {
        if (_characterIsSpeaking)
        {
            _characterIsSpeaking = false;
        }
    }

    public void CreateDialogues(List<GameObject> fo) 
    {

        //Access FallingObject Class (TODO: Review this - does objectmanager need to reference gameobject, or can FallingObject extend Gameobject?)

        foreach (GameObject x in fo)
        {
            //convert to falling object
            FallingObject _fx = x.GetComponent<FallingObject>();
            _fallingObjects.Add(_fx);
        }

        for (currentIndex = 0; currentIndex < _fallingObjects.Count; currentIndex++)
        {
            //get a random prompt
            int n = UnityEngine.Random.Range(0, _prompts.Count - 1);
            string text = _prompts[n];

            //replace "-" in prompt with name of object
            string prompt = text.Replace("_", _fallingObjects[currentIndex].GetObjectName(_fallingObjects[currentIndex]));

            //Debug.Log("prompt" + i +": " + prompt);

            Dialogue d = new Dialogue();

            d.sequence = currentIndex;
            d.fallingObject = _fallingObjects[currentIndex];
            d.fallingObjectName = _fallingObjects[currentIndex].GetObjectName(_fallingObjects[currentIndex]);
            d.prompt = prompt;
            _dialogues.Add(d);
            //Debug.Log("setting up speakers for " + _dialogues[currentIndex].fallingObjectName);
            //Solution - we need to create every dialogue in the dialogue list, and THEN make this call x times after
            OnSpeakerSetup?.Invoke(_dialogues[currentIndex].fallingObject); //TODO: fix this - it sets off a chain that calls liveRemarks for all dialogues. 
        }

        //TODO: We need to call this from gameManager.....
        //_readyDialogueNum = 0;
        //GetLiveRemarks(_dialogues[_readyDialogueNum].fallingObject); //preload first set of remarks

        //TODO: Invoke  Game Manager to call first Dialogue

        //check that max_remark_length is >0, set to 100 (characters)
        if (max_remark_length < 1)
        {
            max_remark_length = 300;
        }
        currentDialogue = _dialogues[0];
        GetLiveRemarks(_dialogues[0].fallingObject); //preload first set of remarks


    }

    public void SetUpDialogues(List<Character> speakerList) //this is calling via a loop based on the # of dialogues/fallingObjects
    {
        Debug.Log("---SetUpDialogues for: "+ _dialogues[currentIndex].fallingObject.name);
        _dialogues[currentIndex].speakers = speakerList;
        for(int i = 0; i< speakerList.Count; i++) { 
            string _empty = "...";
            _dialogues[currentIndex].remarks.Add(_empty);
        }
    }


    //This is called first by DialogueManager for object 0, then by GameManager - here we preload the remarks for the NEXT object to land so it's ready in advance of when the object has fallen
    public void GetLiveRemarks(FallingObject fallingObject)
    {
        var fo = fallingObject;
        string _prompt = "...";

        //Loop through dialogues to find the referenced object
        for (int i = 0; i < _dialogues.Count; i++) //finding the correct dialogue for the object
        {
            if (_dialogues[i].fallingObject == fo)
            {
                currentDialogue = _dialogues[i];
            }
        }

        Debug.Log("getting Dialogue for " + fo.name);
        _readyDialogueNum = currentDialogue.sequence;
        string firstPrompt = currentDialogue.prompt; //TODO: change to a list of prompts
        //_chatHandler.isWaitingForResponse = false; //TODO: might need to use this to check if HF is waiting for a response...

        //Get the remarks for each speaker
        for (int i = 0; i < currentDialogue.speakers.Count; i++) //just gets first remark, all other remarks are added in AddResponseToDialogue
        {

            if (i == 0) //TODO: might need to change this to a check on whether there's a remark other than "..."
            {
                _prompt = firstPrompt;
                //_chatHandler.SendQuery(_prompt, currentDialogue, currentDialogue.speakers[i]._characterModel, i);
                Debug.Log("send query: " + _prompt);
            }


            //OnSendQuery?.Invoke(_prompt, currentDialogue, i);

        }

        //Debug.Log("Getting Dialogue for " + _dialogues[_readyDialogueNum].fallingObjectName + " with prompt: " + _dialogues[_readyDialogueNum].prompt);
        //Actions.GetHFDIalogue(_dialogues[_readyDialogueNum]); //fetch only one Dialogue at a time (A Dialogue contains all remarks about one fallenObject)

        /*
        public void GetDialogue(Dialogue d) //TODO: this loops through all the speakers sending the same prompt. need to prompt chain.
        {
        _dialogue = d;
            for (int i = 0; i < _dialogue.speakers.Count; i++) //TODO: remove this looping - should happen in dialogue manager
            {
                if (i == 0)
                {
                    _prompt = _dialogue.prompt;
                }

                isWaitingForResponse = false;

                //Debug.Log("Remarks COUNT: " + _dialogue.remarks.Count);
                SendQuery(_prompt, _dialogue, i);
                Debug.Log("send query: " + _prompt);
            }
        //}
        */

    }

    private void AddResponseToDialogue(string t, Dialogue d, int remarkNum)
    {

        //var editedDialogue = CleanRemark(t, d.prompt, remarkNum); //------>clean up HF text, select sentences for response based on char limit (max_remark_length)

        Debug.Log("current dialogue num: "+ d.sequence);
        Debug.Log("remarks Count: " + d.remarks.Count);
        Debug.Log("Current remarkNum: " + remarkNum);

        d.remarks[remarkNum] = t;


        //saving all dialogue into a file
        all_remarks.AppendLine("=======" + d.fallingObjectName + "=======");
        //all_remarks.Append(d.speakers[remarkNum].name + "(model: " + d.speakers[remarkNum]._characterModel+"): ");
        all_remarks.AppendLine("prompt: " + d.prompt);
        all_remarks.AppendLine("raw output: "+t);

        Debug.Log("Total remarks: " + total_remarks);
        //Debug.Log(d.speakers[remarkNum]._lLMCharacter.name+" remarks: " + d.remarks[remarkNum]);

        total_remarks++;


        if ((total_remarks +1) >= d.remarks.Count) //All remarks have been retrieved
        {
            _readyDialogueNum++; //increment to next Dialogue about next object
            Debug.Log("next DialogueNum: " + _readyDialogueNum);
            Debug.Log(all_remarks);
            total_remarks = 0; //reset
            OnDialogueReady?.Invoke(d);
        }
        else
        {
            //chain prompting
            //TODO: MAke up a new prompt
            var chainPrompt = ChainPrompt(t, d.prompt);
            //Seperate prompt from "t" in sendQuery
            Debug.Log("Chain prompt: "+chainPrompt);

            remarkNum++;
            Debug.Log("++++++remarknum " + remarkNum +" SpeakerNumbers: "+d.speakers.Count +" remarks_Count: "+d.remarks.Count);
            //_chatHandler.SendQuery(chainPrompt, d, d.speakers[remarkNum]._characterModel, remarkNum);
        }
    }

    //no longer needed?
    private string CleanRemark(String r, String p, int rNum)
    {
        //split into sentences, return xnum of sentences based on character count so that the remarks are not too long
        //return "...";
        // Split the paragraph into sentences
        //string[] sentences = Regex.Split(r, @"(?<=[\.\!\?])\s+");
        var clean_Sentence = Regex.Replace(r, "[^\\w\\ \\!\\?\\.\'\\,\\;]", "");

        string[] sentences = Regex.Split(clean_Sentence, @"(?<=[\.\!\?])"); //remove whitespace flag

        //string[] sentences = r.Split(@"(?<=[\.\!\?])\s+", StringSplitOptions.RemoveEmptyEntries);

        Debug.Log("first cleaned sentence: "+sentences[0]);

        /*
        string[] sentences = r.Split(".", StringSplitOptions.RemoveEmptyEntries); //TODO: TEST (6/27)
        */
        // We need to convert the array to a list - Create a new list to store the sentences
        List<string> sentenceList = new List<string>();

        // Iterate through the sentences and add each one to the list
        foreach (string sentence in sentences)
        {
            sentenceList.Add(sentence);
            //Debug.Log("processing the text into sentences... " + sentence);
        }
        List<string> chosenSentences = ChooseSentences(sentenceList, p, rNum);

        string cleanChosenSentence = String.Join("\n", chosenSentences); //convert the list back into a string with carriage returns

        return cleanChosenSentence;

    }

    //no longer needed?

    List<string> ChooseSentences(List<string> sentenceList, string prompt, int rNum)
    {
        if (sentenceList.Count <= 1) return sentenceList; //just return the whole thing if it's just one sentence
        List<string> tempSentenceList = new List<string>();

        //add sentences as long as length of the entire remark <= max_remark_length
        int totalLength = 0;
        for(int i=0; i<sentenceList.Count; i++)
        {
            /*
            if (rNum == 0)
            {
                Debug.Log("~~~~This is remark number: "+ rNum);
                tempSentenceList.Add(sentenceList[0]); //this adds the prompt into the remark if it is the first remark on a fallen object
                i++; //increment past the initial sentence, which is always a prompt in the first remark
            }
            */
            //iterate through the sentences and add each one until it exceeds the desired length
            totalLength += sentenceList[i].Length;
            if (totalLength <= max_remark_length)
            {
                //check if the sentence we're adding is a complete sentence
                if (sentenceList[i].EndsWith("?") || sentenceList[i].EndsWith(".") || sentenceList[i].EndsWith("!"))
                {
                    tempSentenceList.Add(sentenceList[i]);
                    //tempSentenceList[i].Replace("/\n", "~~");
                    //Debug.Log("Addiing " + sentenceList[i] + " to sentence list");
                }
               
            }
        }
        return tempSentenceList;

        //Debug.Log("total length of all sentences in "+prompt+" SentenceList: " + totalLength);
        /*
            //keep last sentence if it ends with punctuation
            if (sentenceList[lastIndex].EndsWith(".") || sentenceList[lastIndex].EndsWith("?") || sentenceList[lastIndex].EndsWith("!"))
            {
                tempSentenceList.Add(sentenceList[lastIndex]);
            }
            
 //lookAhead
        string[] lookAhead = { "teapot" }; //put words we don't want to show up from old dialogue here

        for (int i = 0; i < tempSentenceList.Count; i++)
        {
            foreach (string look in lookAhead)
            {
                if (tempSentenceList[i].Contains(look))
                {
                    tempSentenceList.RemoveAt(i);
                }
            }
        }
        */
    }


    string ChainPrompt(string prev_remark, string prompt) //TODO: just get the fallenObject name, add it to the string like: "Broken brain."
    {
        //summarize previous remark - possibly from a summarizer?? TODO: research
        string chainP = "respond to "+ prev_remark;     
        return chainP;
    }

    //UTILITY for taking the last x lines of a remark
    private static List<string> TakeLastLines(string text, int count)
    {
        List<string> lines = new List<string>();
        Match match = Regex.Match(text, "^.*$", RegexOptions.Multiline | RegexOptions.RightToLeft);

        while (match.Success && lines.Count < count)
        {
            lines.Insert(0, match.Value);
            match = match.NextMatch();
        }

        return lines;
    }



    public void StartDialogue(Dialogue d) //should probably be triggered by Characters arriving at object on CharacterManager?
    {
        StopAllCoroutines();
        //invvoke an action to tell Character manager to have Characters Speak   
       MakeRemark(d, conversational_pause);
        
        StartCoroutine(MakeRemark(d, conversational_pause));

    }

   
   
    //TODO change this to work on when character finishes speaking rather than a pause 
    //this will happen when audio is implemented
    private IEnumerator MakeRemark(Dialogue d, float pause)
    {
       //TODO link this to speech bubbles to hide them when they finish talking

        for (int i = 0; i < d.speakers.Count; i++)
        {
            while (_characterIsSpeaking)
            {
                yield return null;
            }

            OnCharacterSpeak?.Invoke(d.speakers[i].GetCharacterType, d.remarks[i]);
            _characterIsSpeaking = true;

            yield return new WaitForSeconds(pause);

            //stop speaking here
            OnFinishedSpeaking?.Invoke(d.speakers[i].GetCharacterType);
        }

        OnObjectDialogueFinished?.Invoke();
    }
}
