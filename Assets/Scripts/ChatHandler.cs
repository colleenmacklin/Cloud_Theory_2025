using System;
using System.Collections;
using System.Collections.Generic;
using LLMUnity;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using static UnityEditor.PlayerSettings;
//using UnityEngine.XR;

public class ChatHandler : MonoBehaviour
{
    /*
    public List<Dialogue> _dialogues;
    public Dialogue _dialogue;
    public int _speakerNum;

    //private string _modelURL;
    //private string _modelAPI;
    private string _prompt;
    public string data;
    //public bool isWaitingForResponse;
    public event Action<string, Dialogue, int> HasReceivedResponse; //Checked by dialogueManager, AddResponseToDialogue
    private List<string> philosopher_terms;
    private List<string> tween_terms;
    private List<string> couples_terms;
    private List<string> conspiracy_terms;
    private List<string> marxist_terms;
    private List<string> comedian_terms;
    private List<string> grandma_terms;
    private int randTerm = 0;

    void ReplyCompleted()
    {
        // do something when the reply from the model is complete
        Debug.Log("The AI replied");
    }

    void HandleReply(string reply)
    {
        // do something with the reply from the model
        data = reply;
        Debug.Log("data from chatmanager = " + data);
        HasReceivedResponse?.Invoke(data, _dialogue, _speakerNum); //listened by Dialogue Manager AddResponseToDialogue()

    }


    // Start is called before the first frame update
    public void SendQuery(string _prompt, Dialogue d, CharacterModel modelName, int speakerNum) //TODO: does it even need the speakerNum?
    {
        _speakerNum = speakerNum;
        _dialogue = d;

        if (string.IsNullOrEmpty(_prompt))
        {
            Debug.Log("null text in prompt");
            return;
        }


        switch (modelName)
        {
            case CharacterModel.Philosopher:
                // adding random word list to prompt
                string[] p_input = { "profound", "dizzying", "non - human", "ontological", "epistemology", "phenomenology", "reality", "agency", "perception", "mind", "subconscious", "philosophy", "slippery", "abstract", "concept", "absurd", "logical", "ethics", "metaphysics", "ideology", "belief", "nature", "political", "being", "origin", "dogma", "universe", "life", "art", "practical", "identity", "moral", "culture", "formalism", "worldview", "materialist", "fallacy", "poststructuralism", "normative", "absolute time and space", "art", "solipsistic", "understanding", "fuzzy", "being", "society", "humanity", "Turing test", "posthuman", "sentience", "paradigm", "liquid", "biopolitics", "class struggle", "cognitive bias", "game", "condition of possibility", "cultural hegemony", "consent", "cuteness", "vectorialinsm", "discourse", "transient", "sensory", "fellowship", "alterity", "simulation", "epistemic possibility", "existential phenomenology", "work", "play", "ideology", "meaning", "tactical", "cosmic", "superstructure", "quantum", "utopia", "non-place", "free will", "ludic fallacy", "hard problem of consciousness", "Marx's theory of alienation", "love", "mind-body problem", "panopticon", "possible world", "multiplicity", "politics", "power", "simulation hypothesis" };
                List<string> philosopher_terms = new List<string>(p_input);
                randTerm = UnityEngine.Random.Range(0, philosopher_terms.Count);
                _prompt = _prompt.Replace("~", philosopher_terms[randTerm]);
                _ = d.speakers[speakerNum]._lLMCharacter.Chat(_prompt, HandleReply);
                
                break;

            case CharacterModel.Basic:
                // adding random word list to prompt
                string[] tw_input = { "hot-take", "omg", "totally", "sweaty", "beat", "serve", "random", "surreal", "like", "gum", "cringe", "fire", "sweet", "amaze", "groan", "Uber", "text", "DM", "represent", "disaster", "ground", "midwest princess", "hot to go", "farm", "summer camp", "school", "dance", "homecoming", "flex", "Tinder" };
                List<string> tween_terms = new List<string>(tw_input);
                randTerm = UnityEngine.Random.Range(0, tween_terms.Count);
                _prompt = _prompt.Replace("~", tween_terms[randTerm]);
                Debug.Log("+++++++++++PROMPT: "+_prompt);
                _ = d.speakers[speakerNum]._lLMCharacter.Chat(_prompt, HandleReply);


                break;
            default:
                // adding random word list to prompt
                string[] default_input = { "hot-take", "omg", "totally", "sweaty", "beat", "serve", "random", "surreal", "like", "gum", "cringe", "fire", "sweet", "amaze", "groan", "Uber", "text", "DM", "represent", "disaster", "ground", "midwest princess", "hot to go", "farm", "summer camp", "school", "dance", "homecoming", "flex", "Tinder" };
                List<string> default_terms = new List<string>(default_input);
                randTerm = UnityEngine.Random.Range(0, default_terms.Count);
                _prompt = _prompt.Replace("~", default_terms[randTerm]);
                _ = d.speakers[speakerNum]._lLMCharacter.Chat(_prompt, HandleReply);

                break;
        }

    }
*/
}
