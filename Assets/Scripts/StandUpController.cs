using System.Collections;
using UnityEngine;
using UnityTemplateProjects;

// Attach to the player root GameObject.
// Listens for Actions.Climax, waits a configurable delay, then smoothly
// rises the camera from ground level to eye height, pivots to face the
// forest path, and hands off control to the walking controller.
public class StandUpController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Camera transform (child of this object).")]
    public Transform playerCamera;
    [Tooltip("The lying-down gaze controller to disable.")]
    public SimpleCameraController gazeController;
    [Tooltip("The walking controller to enable after standing.")]
    public SimpleController_UsingPlayerInput walkController;
    [Tooltip("MouseRay cloud-selection script to disable.")]
    public MouseRay mouseRay;

    [Header("Timing")]
    [Tooltip("Seconds after Climax fires before the stand-up begins.")]
    public float delayBeforeStanding = 3f;
    [Tooltip("Duration of the stand-up animation in seconds.")]
    public float standUpDuration = 2.5f;

    [Header("Standing Pose")]
    [Tooltip("World Y position of the camera when fully standing (eye height).")]
    public float standingEyeHeight = 1.7f;
    [Tooltip("World Y rotation the player faces after standing. Set this to point toward the forest path entrance.")]
    public float standingYaw = 0f;

    private void OnEnable()  => Actions.Climax += OnClimax;
    private void OnDisable() => Actions.Climax -= OnClimax;

    private void OnClimax() => StartCoroutine(StandUpSequence());

    private IEnumerator StandUpSequence()
    {
        yield return new WaitForSeconds(delayBeforeStanding);

        // Freeze gaze and cloud interaction.
        if (gazeController != null) gazeController.enabled = false;
        if (mouseRay       != null) mouseRay.enabled       = false;

        Vector3    startPos = playerCamera.position;
        Quaternion startRot = playerCamera.rotation;
        Vector3    endPos   = new Vector3(startPos.x, standingEyeHeight, startPos.z);
        Quaternion endRot   = Quaternion.Euler(0f, standingYaw, 0f);

        // Smoothly rise and reorient using SmoothStep for an ease-in/ease-out feel.
        float elapsed = 0f;
        while (elapsed < standUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / standUpDuration);
            playerCamera.position = Vector3.Lerp(startPos, endPos, t);
            playerCamera.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        // Snap to exact final pose, then align root and camera separately
        // so the walking controller inherits the correct yaw from the root.
        transform.rotation             = Quaternion.Euler(0f, standingYaw, 0f);
        playerCamera.localRotation     = Quaternion.identity;
        playerCamera.localPosition     = new Vector3(0f, standingEyeHeight, 0f);

        // Prime the walking controller's accumulated yaw to match,
        // preventing a snap on the player's first mouse move.
        walkController.InitializeYaw(standingYaw);
        walkController.enabled = true;
    }
}
