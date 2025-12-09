using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using LLMUnity;
using System.IO;
using System.Threading.Tasks;


public class NewNarrator : MonoBehaviour
{
    public TMP_Dropdown allcloudNames;
    public TMP_Text Narration;
    public List<string> cloudHistory;
    public List<string> targetClouds;
    public List<string> allClouds;
    public LLM llm;
    public LLMCharacter llmCharacter;
    public string Prompt = "...";
    public TextMeshProUGUI NarratorText;
    public TextMeshProUGUI PromptText;
    public TextMeshProUGUI RandomWord;
    public TextMeshProUGUI RandomFeeling;

    private void OnEnable()
    {
      Actions.RespondToShape += respond_to_shape;  
    }

        private void OnDisable()
    {
      Actions.RespondToShape -= respond_to_shape;  
    }

    void Start()
    {
        allClouds.Add("a_person_watering_a_fish");
        allClouds.Add("a_jumping_cat");
        allClouds.Add("Abraham_Lincoln");
        allClouds.Add("a_person_playing_saxophone");
        allClouds.Add("a_businesswoman");
        allClouds.Add("a_bucket");
        allClouds.Add("a_walrus_with_a_tophat");
        allClouds.Add("a_poodle");
        allClouds.Add("a_cigarette_butt");
        allClouds.Add("a_telephone");
        allClouds.Add("a_unicorn");
        allClouds.Add("a_cloud");
        allcloudNames.AddOptions(allClouds);
    }

public void respond_to_shape(string s)
    {

        cloudHistory.Add(s);
        Debug.Log("cloudhistory: "+cloudHistory.Last());

        var shape = parseName(s);
        Debug.Log("respond to: "+shape);
        var prompt = CreatePrompt(shape);
        _ = llmCharacter.Chat(prompt, SetNarrationText, AIReplyComplete);

        //some more code here
    }
    public void SetNarrationText(string text)
    {
        Debug.Log("setting Narration Text: " + text);
        NarratorText.text = text;
    }

    // Utilities
    public string parseName(string s)
    {
        string name = s.Replace("_", " ");
        return name;
    }
    public string CreatePrompt(string s)
    {
        //flip a coin
        int num = Random.Range(1,2);
        string Prompt;
        if (num == 1 && cloudHistory.Count > 1)
        {
            cloudHistory.Shuffle(); 
            var pastCloud = cloudHistory.Last();
            Debug.Log("PastCloud: "+pastCloud);
            Prompt = "Tell a hypothetical story about a cloud shaped like a " + s + " and how it is related to "+pastCloud+". Ponder the meaning of seeing these two shapes in one day, and include the shape names in your theory. Stay in the present tense and keep your remarks to 50 words or less.";

        }else 
        ///need to add null checks
        //string Prompt = "tell me a story about a cloud shaped like a " + s + ". reference the shape in the story, and also include the word "+RandomWord.text+" in the story. Keep the story to 50 words or less.";
        Prompt = "Remark on a cloud shaped like a " + s + " and how "+RandomFeeling.text+" it is to see this shape. Ponder the meaning of seeing this shape, and include the shape name in your theory. You must include the word "+RandomWord.text+" in your musing. Be creative and hypothetical! Stay in the present tense and keep your remarks to 50 words or less.";
        PromptText.text = Prompt;
        return Prompt;
    }
    public void AIReplyComplete()
    {
        Debug.Log("Saving history");
        llmCharacter.Save("CloudhatHistory");
        Debug.Log(Application.persistentDataPath);
        
        addData();
        //playerText.text = "";
    }

    public void CancelRequests()
    {
        llmCharacter.CancelRequests();
        AIReplyComplete();
    }
    bool onValidateWarning = true;
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
        llmCharacter.Save("outputs.txt");
        //File.AppendAllText(getPath() + "/Assets/", "testing");
//need to add null checks for adding other text fields
        File.AppendAllText(getPath() + "/Assets/output.txt", "\n" + "Prompt: "+PromptText.text + "\n" + "words:" + Narration.text);
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



}
