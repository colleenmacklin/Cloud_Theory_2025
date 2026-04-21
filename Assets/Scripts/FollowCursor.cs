using System.Collections;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

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
        //_reticule.transform.position = _camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 100)); //note that 'z' is actually 'y' in our laid-down position
        _reticule.transform.position = _camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.transform.position.z+10)); //note that 'z' is actually 'y' in our laid-down position
        
    }

}







