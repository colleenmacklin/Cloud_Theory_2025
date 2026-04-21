using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Random = UnityEngine.Random;

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

public class CharacterManager : MonoBehaviour
{
    private Character[] _characters;

    [SerializeField]
    private Character _testCharacter;
    public Dictionary<FallingObject, List<Character>> conversationalGroups = new Dictionary<FallingObject, List<Character>>(); //unused? (7_12)


}
