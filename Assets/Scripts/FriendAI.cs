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
public class FriendAI : MonoBehaviour
{

    public LLM llm;
    public LLMCharacter llmCharacter;
    public string Prompt = "...";
    public string chosenCloud;
    public TextMeshProUGUI ChatText;    public int numWords;

    private void OnEnable()
    {
      Actions.ChooseCloud += LookAtCloud;  
    }

    private void OnDisable()
    {
      Actions.ChooseCloud -= LookAtCloud;  
    }

    public void LookAtCloud(string s)
    {
        //cloudHistory.Add(s); //need to reference CloudManager...
        //Debug.Log("cloudhistory: "+cloudHistory.Last());
        chosenCloud = s;
    }

}
