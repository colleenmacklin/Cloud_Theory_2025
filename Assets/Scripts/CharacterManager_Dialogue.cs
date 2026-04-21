using OpenCover.Framework.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;
using LLMUnity;
/*
public enum CharacterType
{
    Ferret,
    FrogMan,
    FishTankFace,
    CameraGirl,
    Shit,
    PlugMan,
    Dumbass,
    Raccoon,
    HappyGuy,
    PikaMouse,
    CuteFrog
}
*/
/*
public enum CharacterModel
{
    Philosopher,
    Comedian,
    Earth,
    CouplesCounselor,
    Basic
}

*/
public class CharacterManager_Dialogue: MonoBehaviour
{
    private Character[] _characters;

    [SerializeField]
    private Character _testCharacter;

    [SerializeField]
    private List<Character> _speakers;

    [SerializeField]
    private TMP_Text _panelText;

    public Dictionary<FallingObject, List<Character>> conversationalGroups = new Dictionary<FallingObject, List<Character>>(); //unused? (7_12)

    private Vector3 _targetPosCenterGroup;

    [SerializeField]
    private Transform _roofTarget;

    [SerializeField]
    public bool typeLines = true;

    [SerializeField]
    [Range(1f, 240f)]
    float charactersPerSecond;

    Coroutine typingCoroutine; //needed to stop the specific coroutine

    private bool _dialogueIsReady = false;
    private Dialogue _readyDialog;
    private int _speakerIndex = 0;
    public event Action<Dialogue> OnStartDialogue;

    public event Action OnFinishedTypingRemark;

    public void SetDialogueReady(Dialogue dialogue)
    {
        if (_dialogueIsReady)
        {
            return;
        }
        else
        {
            _readyDialog = dialogue;
            _dialogueIsReady = true;
        }
    }

    private void Awake()
    {
        _characters = GetComponentsInChildren<Character>();

        for (int i = 0; i < _characters.Length -1; i++) {

            //Debug.Log(_characters[i].gameObject.name + i);

            //_characters[i].StartDancing();
            _characters[i].OnCharacterArriveAtObject += CharacterArriveAtObject;
        }
    }

    //decides who will talk about the fallen object and adds them to the dialogue object
    public List<Character> setUpSpeakers(FallingObject fObject) //called from gamemanager
    {
        //Debug.Log("Setting up speakers in character manager for "+fObject.name);
        List<Character> _characters_shuffled = new List<Character>(); //creating a seperate list with all of the characters so that it can be shuffled and then added to conversational groups -CM
        List<Character> _speakingCharacters = new List<Character>(); //speakingCharacters are different for each object 

        foreach (Character character in _characters)
        {
            _characters_shuffled.Add(character);
        }
           
        int numSpeakers = UnityEngine.Random.Range(3, 7); //choosing just a few of the whole group to speak (at least 3, no more than 7)
        _characters_shuffled.Shuffle();

                for (int i = 0; i < numSpeakers; i++)
                {
                    _speakingCharacters.Add(_characters[i]);
                    //Debug.Log(numSpeakers + " speakers added: " + _characters[i].name);
                }
        return _speakingCharacters;
     }

    //on object land:

        private void CharacterArriveAtObject(Character character)
    {
        //Debug.Log(character.GetCharacterType);
        //check if all the speakers have arrived 

       

        if (_readyDialog.speakers.Contains(character))
        {
            _speakerIndex++;
        }

        if (_speakerIndex >= _readyDialog.speakers.Count)
        {
            //start dialogue 
            Debug.Log("all speakers have arrived");
            if (_dialogueIsReady)
            {
                //send start dialogue to dialogue manager
                OnStartDialogue?.Invoke(_readyDialog);

                _dialogueIsReady = false;
                _speakerIndex = 0;
            }
        }
    }


    // move character towards object
    private void Update()
    {
       /* if (Input.GetKeyDown(KeyCode.R))
        {
            //_testCharacter.IsMovingTowardsObject = true;
           /// _testCharacter.TargetPos = _targetPos;
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            CharacterSpeak(CharacterType.Ferret, "heeeyyy im a ferret");
        }

        if (Input.GetKeyDown(KeyCode.X)){
            CharacterSpeak(CharacterType.CameraGirl, "heeeyyy ferry im cammy");
        } */
    }

    public void MoveCharactersTowardsFallenObject(FallingObject fallenObject)
    {
               
        float radius = fallenObject.GetComponentInChildren<MeshRenderer>().bounds.extents.x;

        int ranCharIndex = Random.Range(0, _characters.Length - 1);
        Vector3 origin = _characters[ranCharIndex].CharacterPos;
        Vector3 closestPoint = fallenObject.GetComponentInChildren<MeshRenderer>().bounds.ClosestPoint(origin);

        _targetPosCenterGroup = new Vector3(closestPoint.x, 0, closestPoint.z);

        foreach (Character character in _characters)
            {
                character.IsMovingTowardsTarget = true;
                character.TargetPos = _targetPosCenterGroup;
                character.SetAnimState(Character.AnimState.Walking);
            }
       
    }

    public void CharacterSpeak(CharacterType friend, string text)
    {
        string col = colorToHex(GetCharacterFromCharacterType(friend).CharacterColor);
        GetCharacterFromCharacterType(friend).Speak(text);
        //string my_name = GetCharacterFromCharacterType(friend)._lLMCharacter.name;

        if (typeLines)
        {
            //_panelText.text += " <br>" + "<color=#" + col + ">" + friend + ": ";
            //_panelText.text += " <br>" + "<color=#" + col + ">" + my_name + ": ";
            typingCoroutine = StartCoroutine(TypeString(text)); //begin typing

        }
        else
        {
            //_panelText.text += " <br>" + "<color=#" + col + ">" + friend + ": " + text;
            //_panelText.text += " <br>" + "<color=#" + col + ">" + my_name + ": " + text;
            GetCharacterFromCharacterType(friend).Speak(text);
        }
        //_panelText.text += " <br>" + "<color=#" + col + ">" +  friend + ": " + text;

    }

    public void CharacterFinishSpeaking(CharacterType friend)
    {
        GetCharacterFromCharacterType(friend).StopSpeaking();
    }
    IEnumerator TypeString(string t)
    {
        foreach (char character in t.ToCharArray())
        {
            _panelText.text += character;
            //dialogueAudio?.Play(); //play audio event
            yield return new WaitForSeconds(1f / charactersPerSecond);

        }

        OnFinishedTypingRemark?.Invoke();
    }


    private Character GetCharacterFromCharacterType(CharacterType friend)
    {
        switch (friend)
        {
            case CharacterType.Ferret:
                return _characters[0];
            case CharacterType.FrogMan:
                return _characters[1];
            case CharacterType.FishTankFace:
                return _characters[2];
            case CharacterType.CameraGirl:
                return _characters[3];
            case CharacterType.Shit:
                return _characters[4];
            case CharacterType.PlugMan:
                return _characters[5];
            case CharacterType.Dumbass:
                return _characters[6];
            case CharacterType.Raccoon:
                return _characters[7];
            case CharacterType.HappyGuy:
                return _characters[8];
            case CharacterType.PikaMouse:
                return _characters[9];
            case CharacterType.CuteFrog:
                return _characters[10];

            default:
                return null;
        }
    }
    public static string colorToHex(Color32 color)
    {
        string hex = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
        return hex;
    }

    public void SendCharactersToRoof()
    {
        //send each character to a point on a circle of a certain radius with center as roof target 
        float radius = 5; //radius of circle
        float angleStep = 2 * Mathf.PI / _characters.Length;

        for (int i = 0; i < _characters.Length; i++)
        {
            float angle = i * angleStep;
            
            float xPos = _roofTarget.position.x + radius * Mathf.Cos(angle);
            float zPos = _roofTarget.position.z + radius * Mathf.Sin(angle);

            _characters[i].TargetPos = new Vector3(xPos, _roofTarget.position.y, zPos);
            _characters[i].IsMovingTowardsTarget = true;
            //Debug.Log(_characters[i].name + ": " + angle + " " +  _characters[i].TargetPos);
        }
    }

    public void OnDialogueFinished()
    {
        for (int i = 0; i < _characters.Length; i++)
        {
            _characters[i].StartDancing();
        }
    }
}

