using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using Random = UnityEngine.Random;

public class Character : MonoBehaviour
{
    //Should we consider a concurrent state machine to handle - and tie together - both animations and actions like moving, speaking/singing, etc?
    //see https://gameprogrammingpatterns.com/state.html

    public enum AnimState
    {
        Idle,
        Walking,
        Dancing,
        Speaking //might want to add this to animations
    }


    private Animator _animator;
    private int _animState;

    private NavMeshAgent _agent;

    public event Action<Character> OnCharacterArriveAtObject;

    //[SerializeField]
   // private SpeechBubble _speechBubble;

    //[SerializeField]
    //public  LLMUnity.LLMCharacter _lLMCharacter; //++++llm model info

    //[SerializeField]
    //public CharacterModel _characterModel;

    [SerializeField]
    private CharacterType _characterType;

    private CapsuleCollider _collider;

    private AnimState _currentAnimState;

    private int _danceTriggerOffsetTimer = 0;
    public CharacterType GetCharacterType
    {
        get { return _characterType; }
    }

    public Color CharacterColor;

    public Vector3 CharacterPos
    {
        get { return transform.position; }
    }

    public bool IsMovingTowardsTarget;

    public Vector3 TargetPos;

    public AnimState TargetAnimState = AnimState.Idle;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        //_modelInfo.hf_api_key = _modelInfo._modelAPI;
        //ModelInfo.ModelName = _modelName;
        //ModelInfo.modelURL = _modelURL;


        _agent = GetComponent<NavMeshAgent>();


    }


   
    private void Update()
    {

        if (IsMovingTowardsTarget)
        {
            MoveCharacterToTarget();
        }

        if (_danceTriggerOffsetTimer > -1)
        {
            _danceTriggerOffsetTimer--;

            if (_danceTriggerOffsetTimer == 0)
            {
                SetAnimState(AnimState.Dancing);
            }
        }
    }


    private void MoveCharacterToTarget()
    {
        SetAnimState(AnimState.Walking);
        _agent.isStopped = false;

        _agent.SetDestination(TargetPos);

        float dist = Vector3.Distance(TargetPos, transform.position);
        //Debug.Log(dist);
        if (dist < 5)
        {
            _agent.isStopped = true;
            OnCharacterArriveAtObject?.Invoke(this);
            IsMovingTowardsTarget = false;
            SetAnimState(AnimState.Idle);
        }
    }


    public void Speak(string text)
    {
        //_speechBubble.Speak(text);
    }

    public void StopSpeaking()
    {
        //_speechBubble.HideSpeechBubble();
    }

    public void StartDancing()
    {
        _danceTriggerOffsetTimer = Random.Range(0, 1500);
    }
    public void SetAnimState(AnimState targetState)
    {
        TargetAnimState = targetState;
      
        _animState = _animator.GetInteger("AnimState");

        switch (targetState)
        {
            case AnimState.Walking:
                _animator.SetInteger("AnimState", 1);
                _currentAnimState = AnimState.Walking;
                break;
                
            case AnimState.Dancing:
               int randomDance = UnityEngine.Random.Range(2, 12);
                _animator.SetInteger("AnimState", randomDance);
                _currentAnimState= AnimState.Dancing;
                break;

            default:
                _animator.SetInteger("AnimState", 0);
                _currentAnimState = AnimState.Idle;
                break;
        }
    }


}
