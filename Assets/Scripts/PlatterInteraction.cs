using UnityEngine;
using UnityEngine.InputSystem;

namespace Synthic
{
    public class PlatterInteraction : MonoBehaviour
    {
        [SerializeField] private Camera    playerCamera;
        [SerializeField] private float     raycastRange = 5f;
        [SerializeField] private LayerMask interactableLayer;
[SerializeField] private RhythmicMasterClock masterClock;

        [Header("Visual")]
        [SerializeField] private Color highlightColor = Color.cyan;
        [SerializeField] private Color waitingColor   = Color.yellow;
        [SerializeField] private Color stoppedColor   = Color.red;

        private PlatterSpinner _lookedAtPlatter;
        private BPMSlider      _lookedAtSlider;
        private Renderer       _lookedAtRenderer;
        private Color          _originalColor;

        private void Update()
        {
            if (playerCamera == null || Keyboard.current == null) return;


    DetectInteractable();
    HandleInput();
}

        private void DetectInteractable()
{
    // clear previous highlight
    if (_lookedAtRenderer != null)
    {
        _lookedAtRenderer.material.color = _originalColor;
        _lookedAtRenderer = null;
    }

    _lookedAtPlatter?.SetInRange(false);
    _lookedAtSlider?.SetInRange(false);
    _lookedAtPlatter = null;
    _lookedAtSlider  = null;

    Ray ray = playerCamera.ScreenPointToRay(
        new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));

    // debug - draw the ray in the scene view
    Debug.DrawRay(ray.origin, ray.direction * raycastRange, Color.red);

    if (!Physics.Raycast(ray, out RaycastHit hit, raycastRange, interactableLayer))
    {
        //Debug.Log("Raycast hit nothing");
        return;
    }

    float distance = Vector3.Distance(
        playerCamera.transform.position, hit.point);

    var platter = hit.collider.GetComponentInParent<PlatterSpinner>();
            // check for BPM slider

    var slider = hit.collider.GetComponentInParent<BPMSlider>();
            if (platter != null && distance <= platter.InteractionRange)
            {
                _lookedAtPlatter = platter;
                _lookedAtPlatter.SetInRange(true);

                _lookedAtRenderer = hit.collider.GetComponent<Renderer>();
                if (_lookedAtRenderer != null)
                {
                    _originalColor = _lookedAtRenderer.material.color;
                    _lookedAtRenderer.material.color = platter.WaitingForSync
                        ? waitingColor
                        : platter.IsPlaying
                            ? highlightColor
                            : stoppedColor;
                }
                return;
            }

            // check for BPM slider
            if (slider != null && distance <= slider.InteractionRange)
            {
                _lookedAtSlider = slider;
                _lookedAtSlider.SetInRange(true);

                _lookedAtRenderer = hit.collider.GetComponent<Renderer>();
                if (_lookedAtRenderer != null)
                {
                    _originalColor = _lookedAtRenderer.material.color;
                    _lookedAtRenderer.material.color = highlightColor;
                }
            }
        }

private void HandleInput()
{
    bool shiftHeld = Keyboard.current.leftShiftKey.isPressed ||
                     Keyboard.current.rightShiftKey.isPressed;

    // E key - toggle platter only when looking at one
    if (Keyboard.current.eKey.wasPressedThisFrame && _lookedAtPlatter != null)
        _lookedAtPlatter.Toggle();

    // BPM adjustment - works globally from anywhere
    float increment = shiftHeld ? 10f : 1f;

    if (Keyboard.current.minusKey.wasPressedThisFrame)
        AdjustBPM(-increment);

    if (Keyboard.current.equalsKey.wasPressedThisFrame)
        AdjustBPM(increment);
}

private void AdjustBPM(float amount)
{
    if (masterClock == null)
    {
        Debug.Log("AdjustBPM: masterClock is null");
        return;
    }
    masterClock.SetBPM(Mathf.Clamp(masterClock.BPM + amount, 60f, 180f));
    Debug.Log($"BPM set to {masterClock.BPM}");
}
    }
}