using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamShake : MonoBehaviour
{

    private CinemachineImpulseSource _source;

    private FallingObject _fallingObject;

    private void Awake()
    {
        _source = GetComponent<CinemachineImpulseSource>();
        _fallingObject = GetComponent<FallingObject>();
    }

    private void Start()
    {
        _fallingObject.OnObjectLanded += Shake;
    }
   
    private void Shake(ObjectType type, FallingObject obj)
    {
        _source.GenerateImpulse();
    }
}
