using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using LLMUnity;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEditor.Build.Reporting;
using Crosstales.RTVoice;
using Crosstales.RTVoice.Model;
using UnityEngine.Android;
using Unity.VisualScripting;


public class NewNarrator : MonoBehaviour
{
    public TMP_Text Narration;
    public List<string> cloudHistory;
    public List<string> targetClouds;
    public LLM llm;
    public LLMAgent llmCharacter;
    public string Prompt = "...";
    public string Response = "";
    public string chosenCloud;
    public string chosenTheme;
    public TextMeshProUGUI ChatText;
    public TextMeshProUGUI CompleteText;
    public Subtitle_Handler Subtitles;
    public TextMeshProUGUI SummaryText;
    public TextMeshProUGUI PromptText;
    //public TextMeshProUGUI RandomWord;
    //public TextMeshProUGUI Role;
    public int numWords;
    public VoiceHandler voice;
    public SentenceSplitter sentenceSplitter;

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
        var prompt = CreatePrompt();
        _ = llmCharacter.Chat(prompt, SetChatText, AIReplyComplete);
    }

    public void SetChatText(string text)
    {
        //Debug.Log("setting Narration Text: " + text);
        Response = text;
        ChatText.text = Response;
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
        List<string> splitLines = sentenceSplitter.SplitParagraphIntoSentences(Response);

        lines.Clear();
        
        foreach (string line in splitLines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Enqueue(line);
        }
        
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
        }
    }
    
    private void OnLineComplete()
    {
        SpeakNextLine(); // Speak the next line when current one finishes
    }

    public void SetSummaryText(string text)
    {
        Debug.Log("Summary: " + text);
        SummaryText.text = text;
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
        int num = Random.Range(1,2);
        string Prompt;
        if (num == 1 && cloudHistory.Count > 1)
        {
            cloudHistory.Shuffle(); 
            var pc = cloudHistory.Last();
            string pastCloud = parseName(pc);
            Prompt = "<start_of_turn>user"+"\n"+"Act as \"Bertrand,\" a thoughtful and slightly melancholic socialist philosopher. You are lying on a grassy hill with your lifelong friend, Colleen. "+"\n"+ "TASK: "+"\n" + "Write only Bertrand's side of a dialogue with no attribution. Colleen has just pointed at a cloud and said, \"Look at that one, Bertrand—it looks like a "+ chosenCloud+ ".\"" + "\n" + "CONSTRAINTS:"+"\n"+"1. Speak directly to Colleen." + "\n"+"2. Weave in the philosophical theme of "+chosenTheme+"\n"+"3. Keep the tone contemplative, poetic, and intimate."+"\n"+"4. Do not write Colleen's responses; leave space or use '...' to imply her pauses, but focus on Colleen's spoken words."+"\n"+"5. Connect your comments to the previously seen cloud shape, "+pastCloud+"."+"\n"+"6. Keep your comments to " + numWords + " words or less."+"\n"+"<end_of_turn>"+"\n"+"<start_of_turn>model";
            //Prompt = "In the style of " +Role.text +" tell a hypothetical story about a cloud shaped like " + s + " and how it is related to "+pastCloud+". Ponder the significance of seeing these two shapes in one day, and include the shape names in your theory. You must include the word "+RandomWord.text+" in your response. Stay in the present tense and keep your remarks to "+wordCount+" words or less.";
        }else
        {
            Prompt = "<start_of_turn>user"+"\n"+"Act as \"Bertrand,\" a thoughtful and slightly stoned student of philosophy. You are lying on a grassy hill with your lifelong friend, Colleen. "+"\n"+ "TASK: "+"\n" + "Write only Bertrand's side of a dialogue with no attribution. Colleen has just pointed at a cloud and said, \"Look at that one, Bertrand—it looks like a "+ chosenCloud+ ".\"" + "\n" + "CONSTRAINTS:"+"\n"+"1. Speak directly to Colleen" + "\n" + "2. Reference the shape by name"+"("+chosenCloud+")"+" in the first sentence." + "\n" + "3. Weave in the philosophical theme of "+chosenTheme+"\n"+"4. Keep the tone contemplative, poetic, and intimate but don't be afraid to say something weird."+"\n"+"5. Do not write Colleen's responses; leave space or use '...' to imply their pauses, but focus on Colleen's spoken words."+"\n"+"6. Keep your comments to " + numWords + " words or less."+"\n"+"<end_of_turn>"+"\n"+"<start_of_turn>model";
        }
        ///need to add null checks
        PromptText.text = Prompt;
        return Prompt;
    }

    public void summarize()
    {
        var text = ChatText.text;
        var preface_prompt = PromptText.text;
        var prompt = CreateSummaryPrompt(PromptText.text +" "+text);
        Debug.Log("Summarize: "+prompt);
        _ = llmCharacter.Chat(prompt, SetSummaryText, AIReplyComplete);
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
   // bool onValidateWarning = true;



    /*    
public void SetCompleteText(string text)
    {
        Debug.Log("setting Narration Text: " + text);
        CompleteText.text = text;
    }
    */

    /*    
public void respond_to_shape(string s)
    {
        var shape = parseName(chosenCloud);
        //Debug.Log("respond to: "+shape);
        //var prompt = CreatePrompt(shape);
        //_ = llmCharacter.Chat(prompt, SetNarrationText, AIReplyComplete);
        //var prompt = CreateCompletionPrompt(shape);
        //_ = llmCharacter.Complete(prompt, SetCompleteText, AIReplyComplete);
        //some more code here
    }
*/
/*
    public string CreateCompletionPrompt(string s)
    {
        //flip a coin
        int num = Random.Range(1,2);
        string Prompt;
        //check if the friend has commented on other clouds
        if (num == 1 && cloudHistory.Count > 1)
        {
            cloudHistory.Shuffle(); 
            var pc = cloudHistory.Last();
            string pastCloud = parseName(pc);
            Prompt = "I think that cloud looks like " + s + " which, after seeing a "+pastCloud+" makes me wonder";

        }else
        {
            Prompt = "That cloud looks like " + s + " which makes me wonder ";
        }
        ///need to add null checks
        //string Prompt = "tell me a story about a cloud shaped like a " + s + ". reference the shape in the story, and also include the word "+RandomWord.text+" in the story. Keep the story to 50 words or less.";
        PromptText.text = Prompt;
        return Prompt;
    }
*/

/*
    void OnValidate()
    {
        if (onValidateWarning && !llmCharacter.remote && llmCharacter.llm != null && llmCharacter.llm.model == "")
        {
            Debug.LogWarning($"Please select a model in the {llmCharacter.llm.gameObject.name} GameObject!");
            onValidateWarning = false;
        }
    }
            protected void CheckLLM(LLMCaller llmCaller, bool debug)
        {
            if (!llmCaller.remote && llmCaller.llm != null && llmCaller.llm.model == "")
            {
                string error = $"Please select a llm model in the {llmCaller.llm.gameObject.name} GameObject!";
                if (debug) Debug.LogWarning(error);
                else throw new System.Exception(error);
            }
        }

    // Add data to CSV file
    public async Task addData()
    {
        // Following line adds data to CSV file
        //llmCharacter.Save("outputs.txt");
        //File.AppendAllText(getPath() + "/Assets/", "testing");
//need to add null checks for adding other text fields
        File.AppendAllText(getPath() + "/Assets/output.txt", "\n" + "Prompt: "+PromptText.text + "\n" + "CHAT:" + ChatText.text + "\n" + "COMPLETE:" + CompleteText.text);
        // Following lines refresh the editor and print data
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    // Get path for given CSV file
    private static string getPath()
    {
#if UNITY_EDITOR
        return Application.dataPath;
#elif UNITY_ANDROID
return Application.persistentDataPath;// +fileName;
#elif UNITY_IPHONE
return GetiPhoneDocumentsPath();// +"/"+fileName;
#else
return Application.dataPath;// +"/"+ fileName;
#endif
    }
    // Get the path in iOS device
    private static string GetiPhoneDocumentsPath()
    {
        string path = Application.dataPath.Substring(0, Application.dataPath.Length - 5);
        path = path.Substring(0, path.LastIndexOf('/'));
        return path + "/Documents";
    }

*/

/*
    public void completePrompt(string s)
    {
        var shape = parseName(chosenCloud);
        var prompt = CreateCompletionPrompt(shape);
        //_ = llmCharacter.Complete(prompt, SetCompleteText, AIReplyComplete);

    }
*/


}
