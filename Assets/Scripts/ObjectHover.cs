using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectHover : MonoBehaviour
{
    private float _speed = 2f;
    private float _height = 0.5f; //not implemented

    private float _hoverOffset;

    private Vector3 _pos;

    float _offset;
    float _speedRandomness;

    private void Awake()
    {
         _pos = transform.position;
        _offset = Random.Range(0, 2);
        _speedRandomness = Random.Range(0.1f, 0.6f);
    }
    void Update()
    {
        _hoverOffset = Mathf.Sin(Time.time * (_speed + _speedRandomness))  + _offset;
        transform.localPosition = new Vector3(transform.localPosition.x, _pos.y + _hoverOffset, transform.localPosition.z) ;
    }
}
