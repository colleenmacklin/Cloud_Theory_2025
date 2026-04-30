using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

public class MouseRay : MonoBehaviour
{
    public Camera cam;
    //public GameState gameState;

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
    public UnityTemplateProjects.SimpleCameraController gazeMover;
    
    [SerializeField]
    [Range(.01f, 1f)]
    private float focusInSpeed = .01f;
    [SerializeField]
    [Range(.01f, 1f)]
    private float focusOutSpeed = .01f;
    [SerializeField]
    [Range(0.1f, 3f)]
    private float dwellTimeRequired = 1.5f; // how long the player must hover before locking
    private float _dwellTimer = 0f;
    private GameObject _dwellTarget = null; // track which cloud is being dwelt on

    Coroutine activeCoroutine;
    RaycastHit hit;

    // --- NEW: lock flag and cooldown to prevent re-triggering ---
    private bool _isProcessingCloud = false;
    private float _cooldown = 0f;
    private const float CooldownDuration = 0.5f;

    void OnEnable()
    {
        Actions.ConversationEnded += StartGazeTracking;
        Actions.Cutscene += ReadingMode;
    }

    void OnDisable()
    {
        Actions.ConversationEnded -= StartGazeTracking;
        Actions.Cutscene -= ReadingMode;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("No Camera component found on this GameObject.");
        }
    }

    void Update()
    {
        // Tick down the cooldown timer
        if (_cooldown > 0f)
        {
            _cooldown -= Time.deltaTime;
        }

        CastToClouds();
    }

    private void CastToClouds()
    {
        // --- NEW: skip raycasting entirely while processing or on cooldown ---
        
        if (_isProcessingCloud || _cooldown > 0f) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            switch (state)
            {
                case MouseState.EMPTY:
                    if (hit.transform)
                    {
                        state = MouseState.HOVERING;
                        Actions.OnHoverOverTargetCloud?.Invoke(hit.transform.gameObject);
                        Debug.Log("1----hovering over: " + hit.transform.gameObject.name);
                    }
                    else
                    {
                        Actions.OnHoverExit?.Invoke();
                    }
                    break;

                case MouseState.HOVERING:
                    if (!hit.transform)
                    {
                        // Mouse left — reset everything
                        ResetDwell();
                        state = MouseState.EMPTY;
                        Selected = null;
                        Actions.OnHoverExit?.Invoke();
                    }
                    else
                    {
                        Actions.OnHoverOverTargetCloud?.Invoke(hit.transform.gameObject);
                        Debug.Log("2-----------hovering over: " + hit.transform.gameObject.name);
                    if (_dwellTarget != hit.transform.gameObject)
                    {
                        ResetDwell();
                        _dwellTarget = hit.transform.gameObject;
                    }

                    // Count up the dwell timer
                    _dwellTimer += Time.deltaTime;

                    // Optional: broadcast progress so a UI fill or glow can respond
                    float dwellProgress = Mathf.Clamp01(_dwellTimer / dwellTimeRequired);
                    Actions.OnHoverDwellProgress?.Invoke(dwellProgress); // wire up if you want visual feedback

                    Debug.Log($"Dwelling on {hit.transform.gameObject.name}: {_dwellTimer:F1}s / {dwellTimeRequired:F1}s");

                    // Only trigger once the player has held long enough
                    if (_dwellTimer >= dwellTimeRequired)
                    {
                        ResetDwell();
                        StartCloudTalking();
                    }
                }
                break;

                case MouseState.READING:
                    //if (gameState.Gameloop)
                    //{
                        StopGazeTracking();
                    //}
                    break;
            }

            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.red);
        }
        else
        {
                    // Ray hit nothing — if we were hovering, reset
        if (state == MouseState.HOVERING)
        {
            ResetDwell();
            state = MouseState.EMPTY;
            Selected = null;
            Actions.OnHoverExit?.Invoke();
        }
    

        }
    }

    private void ResetDwell()
    {
        _dwellTimer = 0f;
        _dwellTarget = null;
        // Reset progress to zero if you're driving a UI element
        Actions.OnHoverDwellProgress?.Invoke(0f);
    }   

    void ReadingMode()
    {
        Debug.Log ("ReadingMode");
        state = MouseState.READING;
        StartGazeTracking();
    }

    void StartGazeTracking()
    {
        Debug.Log ("StartGazeTracking");
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }
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
    // gazeMover and centering are now handled by CenterThenWaitForConversation
    // so we no longer need to kick off LookAtSelection here
}


    public void StartCloudTalking()
    {
        // --- NEW: lock immediately so this can't be called again until released ---
        _isProcessingCloud = true;

        Debug.Log("Raycaster calls GetClickedCloud");
        Selected = hit.transform.gameObject;
        GameObject c = Selected;
        
        // Lock camera movement immediately
        gazeMover.enabled = false;

        Actions.GetClickedCloud?.Invoke(c); //for cloudmanager
        Actions.Respond?.Invoke(); //for narrator
        state = MouseState.READING;

        // --- NEW: wait for the conversation to end before unlocking ---
        StartCoroutine(CenterThenWaitForConversation());
    }


    // --- NEW: coroutine that waits for ConversationEnded before releasing the lock ---
private IEnumerator CenterThenWaitForConversation()
{
    // First, smoothly rotate to face the selected cloud
    Quaternion rot = Quaternion.LookRotation(
        Selected.transform.position - Camera.main.transform.position, 
        Camera.main.transform.up
    );

    while (Quaternion.Angle(Camera.main.transform.rotation, rot) > 1f)
    {
        Camera.main.transform.rotation = Quaternion.Slerp(
            Camera.main.transform.rotation, rot, focusInSpeed
        );
        yield return null;
    }

    Debug.Log("Camera centered on cloud, waiting for conversation to end.");

    // Now wait for the narrator to finish
    bool conversationEnded = false;
    Action onEnded = () => conversationEnded = true;
    Actions.ConversationEnded += onEnded;

    yield return new WaitUntil(() => conversationEnded);

    Actions.ConversationEnded -= onEnded;

    // Return camera control to the player
    gazeMover.enabled = true;

    _cooldown = CooldownDuration;
    _isProcessingCloud = false;

    Debug.Log("Conversation ended, camera control returned to player.");
}
    
}