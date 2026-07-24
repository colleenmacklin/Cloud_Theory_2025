using System.Collections;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class FollowCursor : MonoBehaviour
{
    private Camera _camera;
    public GameObject _reticule;


    private void Awake()
    {

        
        foreach(Camera c in Camera.allCameras)
        {
            if (c.gameObject.name == "Main Camera")
            {
                //Debug.Log("main camera founddddd");
                _camera = c;
            }
        }
       
    }

    private void Update()
    {
        if (_reticule == null || _camera == null || Camera.main == null || Mouse.current == null) return;
        Vector2 mouse = Mouse.current.position.ReadValue();
        _reticule.transform.position = _camera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, Camera.main.transform.position.z + 10));
    }

}







