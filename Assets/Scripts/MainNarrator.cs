using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using LLMUnity;
using TMPro;

public class MainNarrator : MonoBehaviour
{
    public enum NarratorEngine { LLM, Tracery }

    [Header("Engine")]
    public NarratorEngine engine = NarratorEngine.LLM;

    [Header("LLM Settings")]
    public LLM llm;
    public LLMAgent llmCharacter;
    public string Prompt = "...";
    public string Response = "";
    public int numWords;
    public String PromptAddition;

    [Header("Tracery Settings")]
    public TextAsset traceryGrammar;
    public string traceryStartRule = "origin";
    private TraceryEngine _tracery;

    [Header("Shared")]
    public List<string> cloudHistory;
    public List<string> targetClouds;
    public string chosenCloud;
    public string chosenTheme;
    public VoiceHandler voice;
    public SentenceSplitter sentenceSplitter;
    public Subtitle_Handler Subtitles;

    private Queue<string> lines = new Queue<string>();
    private bool isNarrating = false;


        private void OnEnable()
    {
        Actions.ChooseCloud += LookAtCloud;  
        Actions.ChooseTheme += setTheme;
        voice.OnSpeechComplete += OnLineComplete;
    }

    private void OnDisable()
    {
        Actions.ChooseCloud -= LookAtCloud;  
        Actions.ChooseTheme -= setTheme;
        voice.OnSpeechComplete -= OnLineComplete;
    }
    public void setTheme(string theme) 
    {
        //set the theme variable for the prompt
        chosenTheme = theme;
    }
    public void LookAtCloud(string s)
    {
        cloudHistory.Add(s);
        chosenCloud = parseName(s);
        respondToShape(s);
    }
    public void respondToShape(string s)
    {
        if (engine == NarratorEngine.Tracery)
        {
            if (_tracery == null)
            {
                if (traceryGrammar == null) { Debug.LogError("[MainNarrator] No Tracery grammar assigned."); return; }
                _tracery = TraceryEngine.FromTextAsset(traceryGrammar);
            }
            string result = _tracery.Generate(traceryStartRule, chosenCloud);
            StartNarration(result);
        }
        else
        {
            var prompt = CreatePrompt();
            _ = llmCharacter.Chat(prompt, SetChatText, AIReplyComplete);
        }
    }
    public void SetChatText(string text)
    {
        //Debug.Log("setting Narration Text: " + text);
        Response = text;
        //ChatText.text = Response;
    }
        public void AIReplyComplete()
    {
        Debug.Log("AI reply complete");
        //speak();
        StartNarration(Response);
        //llmCharacter.Save("CloudChatHistory");
        //Debug.Log(Application.persistentDataPath);
        //addData();
        //playerText.text = "";
    }

    public void StartNarration(string paragraph)
    {
        List<string> splitLines = sentenceSplitter.SplitParagraphIntoSentences(paragraph);

        lines.Clear();

        foreach (string line in splitLines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Enqueue(line);
        }

        Actions.Speak?.Invoke();
        SpeakNextLine();
    }
    
    private void SpeakNextLine()
    {
        if (lines.Count > 0)
        {
            string nextLine = lines.Dequeue();
            voice.SpeakLine(nextLine);
            printSubtitle(nextLine);
        }
        else
        {
            isNarrating = false;
            fadeOutSubtitle();
            Actions.ConversationEnded?.Invoke(); //tells system this cloud is no longer beng talked about
        }
    }
    
    private void OnLineComplete()
    {
        SpeakNextLine(); // Speak the next line when current one finishes
    }

    //subtitle StartFadeSequence
    private void printSubtitle(string text)
    {
        //should print one line to screen, then when the line has been read, print the next line
        Subtitles.StartTextSequence(text);
    }

    private void fadeOutSubtitle()
    {
        Subtitles.speakEnd();
    }

    // Utilities
    public string parseName(string s)
    {
        string name = s.Replace("_", " ");
        return name;
    }
    public string CreatePrompt()
    {
        //flip a coin
        int num = UnityEngine.Random.Range(1,2);
        string Prompt;
        if (num == 1 && cloudHistory.Count > 1)
        {
            cloudHistory.Shuffle(); 
            var pc = cloudHistory.Last();
            string pastCloud = parseName(pc);
            Prompt = "<start_of_turn>user"+"\n"+"Act as \"Bertrand,\" a thoughtful and slightly melancholic socialist philosopher. You are lying on a grassy hill with your lifelong friend, Colleen. "+"\n"+ "TASK: "+"\n" + "Write only Bertrand's side of a dialogue with no attribution. Colleen has just pointed at a cloud and said, \"Look at that one, Bertrand—it looks like a "+ chosenCloud+ ".\"" + "\n" + "CONSTRAINTS:"+"\n"+"1. Speak directly to Colleen." + "\n"+"2. Weave in the philosophical theme of "+chosenTheme+"\n"+"3. "+PromptAddition+"\n"+"4. Do not write Colleen's responses; leave space or use '...' to imply her pauses, but focus on Colleen's spoken words."+"\n"+"5. Connect your comments to the previously seen cloud shape, "+pastCloud+"."+"\n"+"6. Keep your comments to " + numWords + " words or less."+"\n"+"<end_of_turn>"+"\n"+"<start_of_turn>model";
            //older prompts
            //Prompt = "<start_of_turn>user"+"\n"+"Act as \"Bertrand,\" a thoughtful and slightly melancholic socialist philosopher. You are lying on a grassy hill with your lifelong friend, Colleen. "+"\n"+ "TASK: "+"\n" + "Write only Bertrand's side of a dialogue with no attribution. Colleen has just pointed at a cloud and said, \"Look at that one, Bertrand—it looks like a "+ chosenCloud+ ".\"" + "\n" + "CONSTRAINTS:"+"\n"+"1. Speak directly to Colleen." + "\n"+"2. Weave in the philosophical theme of "+chosenTheme+"\n"+"3. Keep the tone contemplative, poetic, and intimate."+"\n"+"4. Do not write Colleen's responses; leave space or use '...' to imply her pauses, but focus on Colleen's spoken words."+"\n"+"5. Connect your comments to the previously seen cloud shape, "+pastCloud+"."+"\n"+"6. Keep your comments to " + numWords + " words or less."+"\n"+"<end_of_turn>"+"\n"+"<start_of_turn>model";
            //Prompt = "In the style of " +Role.text +" tell a hypothetical story about a cloud shaped like " + s + " and how it is related to "+pastCloud+". Ponder the significance of seeing these two shapes in one day, and include the shape names in your theory. You must include the word "+RandomWord.text+" in your response. Stay in the present tense and keep your remarks to "+wordCount+" words or less.";
        }else
        {
            Prompt = "<start_of_turn>user"+"\n"+"Act as \"Bertrand,\" a thoughtful and slightly stoned student of philosophy. You are lying on a grassy hill with your lifelong friend, Colleen. "+"\n"+ "TASK: "+"\n" + "Write only Bertrand's side of a dialogue with no attribution. Colleen has just pointed at a cloud and said, \"Look at that one, Bertrand—it looks like a "+ chosenCloud+ ".\"" + "\n" + "CONSTRAINTS:"+"\n"+"1. Speak directly to Colleen" + "\n" + "2. Reference the shape by name"+"("+chosenCloud+")"+" in the first sentence." + "\n" + "3. Weave in the philosophical theme of "+chosenTheme+"\n"+"4. "+PromptAddition+"\n"+"5. Do not write Colleen's responses; leave space or use '...' to imply their pauses, but focus on Colleen's spoken words."+"\n"+"6. Keep your comments to " + numWords + " words or less."+"\n"+"<end_of_turn>"+"\n"+"<start_of_turn>model";
        }
        ///need to add null checks
        //PromptText.text = Prompt;
        return Prompt;
    }
    public void SetSummaryText(string text)
    {
        Debug.Log("Summary: " + text);
        //SummaryText.text = text;
    }
    public void summarize()
    {
 
        if (string.IsNullOrEmpty(Response))
            {
                Debug.Log("Can't Summarize the text: The string is null or empty.");
            }
        else
            {
            //Debug.Log("The string has content.");
            var text = Response;
            var preface_prompt = Prompt;
            var _prompt = CreateSummaryPrompt(Prompt +" "+text);
            Debug.Log("Summarize: "+_prompt);
            _ = llmCharacter.Chat(_prompt, SetSummaryText, AIReplyComplete);
            }

    }

    public string CreateSummaryPrompt(string s)
    {
        string summaryPrompt  = "Shorten this story to two sentences: "+s+"\n only respond with the sentences, do not add any commentary. Keep the language simple.";
        return summaryPrompt;
    }
    public void CancelRequests()
    {
        llmCharacter.CancelRequests();
        AIReplyComplete();
    }

}
