using UnityEngine;
using System.Collections;
using System;

public class Raycaster : MonoBehaviour
{
    //public event Action<GameObject> OnHoverOverTargetCloud;
    //public event Action OnHoverExit;
    public GameState gameState;

    [SerializeField]
    public GameObject Selected
    {
        get;
        private set;
    }

    private enum MouseState
    {
        EMPTY,
        HOVERING,
        READING
    }

    MouseState state = MouseState.EMPTY;
    Vector3 lookAtSelected = new Vector3();
    //Get the camera mover so we can turn it on and off during dialogue
    public UnityTemplateProjects.SimpleCameraController gazeMover; //attached to the camera *it probably shouldn't be
    //public TextBoxController textBoxControl;

    //View FOcus settings
    [SerializeField]
    [Range(.01f, 1f)]
    private float focusInSpeed = .01f;
    [SerializeField]
    [Range(.01f, 1f)]
    private float focusOutSpeed = .01f;

    // Raycasting variables 
    Ray ray;
    RaycastHit hit;
    LayerMask mask;

    Quaternion initialCameraRot;
    Coroutine activeCoroutine;


    void OnEnable()
    {
        Actions.ConversationEnded += StartGazeTracking;
        Actions.Speak += StopGazeTracking;
        Actions.Cutscene += ReadingMode;

    }

    void OnDisable()
    {
        Actions.ConversationEnded -= StartGazeTracking;
        Actions.Speak -= StopGazeTracking;
        Actions.Cutscene -= ReadingMode;

    }

    void ReadingMode()
    {
        state = MouseState.READING;
        StartGazeTracking(); //shouldnt this be stop gazeTracking? //CM COMMENTED OUT 7/31
    }

    //None of the tracking should be doing as many mutations as it is now
    //gazeMover, state, and the coroutines all require some reconfiguration in the future
    void StartGazeTracking()
    {
        if (activeCoroutine != null)
        {

            StopCoroutine(activeCoroutine);

        }
       // activeCoroutine = StartCoroutine(ReturnToDefaultView());

        gazeMover.enabled = true;
        state = MouseState.EMPTY;
    }
    void StopGazeTracking()
    {
        ReadingMode();
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }
        gazeMover.enabled = false;
        activeCoroutine = StartCoroutine(LookAtSelection());
        //EventManager.TriggerEvent("closeEye");
    }

    //CM turned this on again so that the centire cloud can be seen when it is being talked about (7/30/2023)
    //Look directly at target
    IEnumerator LookAtSelection()
    {
        Quaternion rot = Quaternion.LookRotation(Selected.transform.position, Camera.main.transform.up);

        while (Quaternion.Angle(rot, Camera.main.transform.localRotation) > 1f)
        {
            Camera.main.transform.rotation = Quaternion.Slerp(Camera.main.transform.rotation, rot, focusInSpeed);

            //Debug.Log($"looking at target, {rot},{Camera.main.transform.localRotation}");
            yield return null;
        }
      
        Debug.Log("TargetFound");
    }


    void Update()
    {
        //always cast the ray
        ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out hit, Mathf.Infinity, mask);

        switch (state)
        {
            case MouseState.EMPTY:
                //if empty and hit, then switch to hovering
                if (hit.transform)
                {
                    state = MouseState.HOVERING;
                    //EventManager.TriggerEvent("openEye");
                    //callback to start butterfly glow - when entering cloud over hover
                    //OnHoverOverTargetCloud?.Invoke(hit.transform.gameObject);
                    Actions.OnHoverOverTargetCloud?.Invoke(hit.transform.gameObject);
                    Debug.Log("1----hovering over: " + hit.transform.gameObject.name);

                }
                else
                {
                    //OnHoverExit?.Invoke(); //DeGlow callback on Butterfly
                    Actions.OnHoverExit();

                }
                break;

            case MouseState.HOVERING:
                //if hovering and no hit, then switch to empty
                if (!hit.transform)
                {
                    
                    state = MouseState.EMPTY;
                    Selected = null;
                    //EventManager.TriggerEvent("closeEye");

                    //if exit cloud then stop glow
                   // OnHoverExit?.Invoke(); //DeGlow callback on Butterfly
                    Actions.OnHoverExit?.Invoke();
                }
                else
                {
                    Actions.OnHoverOverTargetCloud(hit.transform.gameObject);
                    //OnHoverOverTargetCloud?.Invoke(hit.transform.gameObject); //calbackk to butterfly startglow TODO: make callback to cloud object
                    Debug.Log("2-----------hovering over: " + hit.transform.gameObject.name);
                    StartCloudTalking();
                }               
                break;

            case MouseState.READING:

                if (gameState.Gameloop)
                {
                    StopGazeTracking();
                    //butterflyControl._isTalking = true;
                }
                    //textBoxControl.Check();//bad mutation management.
                
                break;
        }


    }

    public void StartCloudTalking()
    {
        Debug.Log("Raycaster calls GetClickedCloud");
        Selected = hit.transform.gameObject;

        GameObject c = Selected;

        Actions.GetClickedCloud(c); //lets cloudmanager know which cloud has been clicked

        //EventManager.TriggerEvent("Respond");
        Actions.Respond();
        state = MouseState.READING;
    }

}
