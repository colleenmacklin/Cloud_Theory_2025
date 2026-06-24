using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Synthic;

/// <summary>
/// Attach to the "Play" cylinder GameObject (child of the platter rig).
/// Highlights on hover, animates a press, and toggles the linked PlatterSpinner.
/// Trigger: left-click OR spacebar while hovering.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class PlatterToggleButton : MonoBehaviour
{
    [Header("References")]
    public PlatterSpinner platter;
    public Camera         playerCamera;
    public LayerMask      buttonLayer;

    [Header("Colors")]
    public Color normalColor  = new Color(0.15f, 0.15f, 0.15f);
    public Color hoveredColor = new Color(0.45f, 0.45f, 0.45f);
    public Color playingColor = new Color(0.15f, 0.75f, 0.15f);
    public Color waitingColor = new Color(0.75f, 0.75f, 0.15f);

    [Header("Press Animation")]
    [Tooltip("How far the button moves along its local -Y axis when pressed.")]
    public float pressDepth = 0.05f;
    [Tooltip("Speed of the press and release movement.")]
    public float pressSpeed = 15f;

    private Renderer _renderer;
    private Vector3  _restLocalPos;
    private bool     _isHovered;
    private bool     _animating;

    private void Start()
    {
        _renderer     = GetComponent<Renderer>();
        _restLocalPos = transform.localPosition;
    }

    private void Update()
    {
        _isHovered = IsMouseOver();
        UpdateColor();

        if (_isHovered && !_animating)
        {
            bool clicked  = Mouse.current.leftButton.wasPressedThisFrame;
            bool spaceBar = Keyboard.current.spaceKey.wasPressedThisFrame;
            if (clicked || spaceBar)
            {
                platter.Toggle();
                StartCoroutine(PressAnimation());
            }
        }
    }

    private bool IsMouseOver()
    {
        Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        return Physics.Raycast(ray, out RaycastHit hit, 100f, buttonLayer)
               && hit.collider.gameObject == gameObject;
    }

    private void UpdateColor()
    {
        if (platter == null) return;
        _renderer.material.color =
            platter.WaitingForSync ? waitingColor :
            platter.IsPlaying      ? playingColor :
            _isHovered             ? hoveredColor :
                                     normalColor;
    }

    private IEnumerator PressAnimation()
    {
        _animating = true;

        // press down along local -Y
        Vector3 pressed = _restLocalPos - Vector3.up * pressDepth;

        for (float t = 0f; t < 1f; t = Mathf.MoveTowards(t, 1f, Time.deltaTime * pressSpeed))
        {
            transform.localPosition = Vector3.Lerp(_restLocalPos, pressed, t);
            yield return null;
        }

        for (float t = 0f; t < 1f; t = Mathf.MoveTowards(t, 1f, Time.deltaTime * pressSpeed))
        {
            transform.localPosition = Vector3.Lerp(pressed, _restLocalPos, t);
            yield return null;
        }

        transform.localPosition = _restLocalPos;
        _animating = false;
    }
}
