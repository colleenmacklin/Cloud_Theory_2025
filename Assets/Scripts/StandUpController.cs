using System.Collections;
using UnityEngine;
using UnityTemplateProjects;

// Attach to the player root GameObject (the lying-down rig).
// On Climax: animates the camera rising from ground to standing height,
// then activates the separate walking Player and deactivates this rig.
public class StandUpController : MonoBehaviour
{
    [Header("Lying-Down Rig")]
    [Tooltip("The Camera transform on the lying-down rig.")]
    public Transform playerCamera;
    [Tooltip("The gaze controller to stop before animating.")]
    public SimpleCameraController gazeController;
    [Tooltip("MouseRay cloud-selection script to stop before animating.")]
    public MouseRay mouseRay;
    [Tooltip("The 'Reticule' root GameObject (FollowCursor, the glass-ball reticule, and the EyeReticule eye rig all live under it) — deactivated before animating, since mouse-based cloud selection is over once standing begins.")]
    public GameObject reticule;
    [Tooltip("The root GameObject to deactivate after standing. Usually 'this' — drag in this object.")]
    public GameObject lyingDownRig;

    [Header("Walking Player")]
    [Tooltip("The 'Player' prefab instance placed in the scene, starting disabled.")]
    public GameObject walkingPlayer;

    [Header("Timing")]
    [Tooltip("Seconds after Climax fires before the stand-up animation begins.")]
    public float delayBeforeStanding = 3f;
    [Tooltip("Duration of the rising animation in seconds.")]
    public float standUpDuration = 2.5f;

    [Header("Standing Pose")]
    [Tooltip("World Y of the camera when fully standing (eye height above ground).")]
    public float standingEyeHeight = 1.7f;
    [Tooltip("World Y rotation the player faces after standing (toward the forest path).")]
    public float standingYaw = 0f;

    [Header("Debug")]
    [Tooltip("Tick in Play mode to trigger immediately without waiting for Climax.")]
    public bool debugTriggerNow = false;

    private bool _sequenceStarted = false;

    private void OnEnable()  => Actions.StandUp += TriggerStandUp;
    private void OnDisable() => Actions.StandUp -= TriggerStandUp;

    private void Update()
    {
#if UNITY_EDITOR
        if (debugTriggerNow)
        {
            debugTriggerNow = false;
            TriggerStandUp();
        }
#endif
    }

    [ContextMenu("Debug: Trigger Stand Up")]
    private void TriggerStandUp()
    {
        if (_sequenceStarted) return;
        _sequenceStarted = true;
        StartCoroutine(StandUpSequence());
    }

    private IEnumerator StandUpSequence()
    {
        yield return new WaitForSeconds(delayBeforeStanding);

        KillGazeAndMouse();

        Vector3    startPos = playerCamera.position;
        Quaternion startRot = playerCamera.rotation;
        Vector3    endPos   = new Vector3(startPos.x, standingEyeHeight, startPos.z);
        Quaternion endRot   = Quaternion.Euler(0f, standingYaw, 0f);

        Debug.Log($"[StandUp] Animation start. pos={startPos} rot={startRot.eulerAngles}");

        float elapsed = 0f;
        while (elapsed < standUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / standUpDuration);
            playerCamera.position = Vector3.Lerp(startPos, endPos, t);
            playerCamera.rotation = Quaternion.Slerp(startRot, endRot, t);
            // Keep controllers dead in case something re-enables them mid-animation.
            KillGazeAndMouse();
            yield return null;
        }

        Debug.Log($"[StandUp] Animation done. Switching to walking player.");

        // Place the walking player at the same XZ, at ground level, facing standingYaw.
        if (walkingPlayer != null)
        {
            Vector3 spawnPos = new Vector3(startPos.x, 0f, startPos.z);
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
                spawnPos.y = terrain.SampleHeight(spawnPos) + terrain.transform.position.y;

            walkingPlayer.transform.position = spawnPos;
            walkingPlayer.transform.rotation = Quaternion.Euler(0f, standingYaw, 0f);
            walkingPlayer.SetActive(true);
            Debug.Log($"[StandUp] Walking player activated at {spawnPos}.");
        }
        else
        {
            Debug.LogWarning("[StandUp] walkingPlayer is not assigned!");
        }

        // Deactivate the lying-down rig (this also kills any remaining coroutines on it).
        if (lyingDownRig != null)
            lyingDownRig.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    // Stops MouseRay coroutines (preventing CenterThenWaitForConversation from
    // re-enabling gazeMover when ConversationEnded fires) and disables both controllers.
    private void KillGazeAndMouse()
    {
        if (gazeController != null)
            gazeController.enabled = false;
        else
            foreach (var c in GetComponentsInChildren<SimpleCameraController>())
                c.enabled = false;

        if (mouseRay != null)
        {
            mouseRay.StopAllCoroutines();
            mouseRay.enabled = false;
        }
        else
            foreach (var c in GetComponentsInChildren<MouseRay>())
            {
                c.StopAllCoroutines();
                c.enabled = false;
            }

        if (reticule != null)
            reticule.SetActive(false);
    }
}
