using UnityEngine;
using System.Collections;
using System;

public class MouseRay : MonoBehaviour
{
    // Reference to the Camera component (gets it automatically in Start)
    public Camera cam;
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
    //View FOcus settings
    [SerializeField]
    [Range(.01f, 1f)]
    private float focusInSpeed = .01f;
    [SerializeField]
    [Range(.01f, 1f)]
    private float focusOutSpeed = .01f;
    Coroutine activeCoroutine;
    RaycastHit hit;


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

    void Start()
    {
        // Get the Camera component attached to this GameObject
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("No Camera component found on this GameObject.");
        }

        //StartGazeTracking();

    }

    void Update()
    {
        // Cast a ray every frame to follow the mouse position
        //CastMouseRay(); //defaul good for debugging
        CastToClouds();
    }

    private void CastToClouds(){
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        { 
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
                    Actions.OnHoverExit?.Invoke();
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
                    Actions.OnHoverExit?.Invoke();
                }
                else
                {
                    Actions.OnHoverOverTargetCloud?.Invoke(hit.transform.gameObject);
                    Debug.Log("2-----------hovering over: " + hit.transform.gameObject.name);
                    StartCloudTalking();
                }               
                break;

            case MouseState.READING:

                if (gameState.Gameloop)
                {
                    StopGazeTracking();
                }
                    //textBoxControl.Check();//bad mutation management.
                break;
        }
            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.red);
         }
}
    void CastMouseRay()
    {
        // Create a ray from the current mouse position in screen space
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Perform the raycast
        // The max distance can be set to a specific value or Mathf.Infinity
        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            // If the ray hits an object, you can access its information
            Debug.Log("Hit object: " + hit.transform.name + " at point: " + hit.point);
            
            // Optional: Draw a debug line in the Scene view to visualize the raycast
            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.red);
            
            // You can add code here to make another object move to `hit.point`,
            // change the color of the hit object, or trigger other events.
        }
        else
        {
            // Optional: Draw a debug line if the ray doesn't hit anything within the max distance
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.blue);
        }
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

    public void StartCloudTalking()
    {
        Debug.Log("Raycaster calls GetClickedCloud");
        Selected = hit.transform.gameObject;

        GameObject c = Selected;

        Actions.GetClickedCloud?.Invoke(c); //lets cloudmanager know which cloud has been clicked

        //EventManager.TriggerEvent("Respond");
        Actions.Respond?.Invoke();
        state = MouseState.READING;
    }


}