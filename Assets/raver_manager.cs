using OpenCover.Framework.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class raver_manager : MonoBehaviour
{
    private Character[] _characters;

    [SerializeField]
    private Transform _roofTarget;



    private void Awake()
    {
        _characters = GetComponentsInChildren<Character>();

        for (int i = 0; i < _characters.Length - 1; i++)
        {

            //Debug.Log(_characters[i].gameObject.name + i);

            _characters[i].StartDancing();
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

    /*
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
    */


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

}

