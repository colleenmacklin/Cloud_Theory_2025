using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class Controller : MonoBehaviour
{
    [CanBeNull] //is null in opening, needs to be refereneced in main scene
    [SerializeField]
    private Storyteller _storyTeller;
    public Vector3 _mousePos;
    private Vector3 targetPos;
    public bool _ismoving;
    public bool _isTalking;
    public Camera mainCam;
    [CanBeNull] //null in opening, referenced in main
    [SerializeField]
    private Raycaster _raycasterScript;

    [CanBeNull] //null in main, referenced in opening
    [SerializeField]
    private RaycasterOpening _raycasterOpening;

    [CanBeNull] //null in main, referenced in opening
    [SerializeField]
    private Opening _opening;
    //this is for opening only 
    private GameObject _selectedCloudObject = null;

    private void Awake()
    {
        /*
        if (_raycasterScript)
        {
            _raycasterScript.OnHoverOverTargetCloud += LookAtCloud;
            _raycasterScript.OnHoverExit += StopLooking;
        }

        if (_raycasterOpening)
        {
            _raycasterOpening.OnHoverOverTargetCloud += LookAtCloud;
            _raycasterOpening.OnHoverExit += StopLooking;

        }

        foreach (Camera c in Camera.allCameras)
        {
            if (c.gameObject.name == "Main Camera")
            {
                mainCam = c;
            }
        }
        */
    }

    public void LookAtCloud(GameObject cloud)
    {
        Debug.Log("lllooooooking at : " + cloud);
            _selectedCloudObject = cloud;
      
        
    }
    public void StopLooking()
    {
        Debug.Log("stopppp loookingt");

    }

    private void Update()
    {

        //_mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
       // var mouseVector = new Vector3(_mousePos.x, _mousePos.y, 100);
        //var mouseVector = new Vector3(_mousePos.x, 60, _mousePos.y);

        //targetPos = mouseVector; //set the butterfly to the mouse cursor
        //this.transform.position = targetPos;

    }

}
