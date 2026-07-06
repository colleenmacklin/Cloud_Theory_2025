using UnityEngine;
using UnityEngine.InputSystem;
using FIMSpace.FEyes;

[DefaultExecutionOrder(200)]
public class EyeReticule : MonoBehaviour
{
    [Header("References")]
    public Camera _camera;
    public FEyesAnimator eyesAnimator;

    [Header("Cursor Follow")]
    [Tooltip("World-space distance in front of the camera")]
    public float cursorDepth = 10f;
    [Tooltip("Drag the bone that should sit on the cursor (e.g. Sphere or HeadBone1)")]
    public Transform pivotBone;

    [Header("Eye Look")]
    [Tooltip("Max degrees the iris moves from center as the cursor reaches the screen edge")]
    [Range(0f, 90f)]
    public float maxLookAngle = 30f;
    [Tooltip("Euler correction baked into the neutral iris rotation. Set to (180,0,0) if the iris faces backwards.")]
    public Vector3 baseEulerOffset = new Vector3(180f, 0f, 0f);

    [Header("Open/Close")]
    [Range(1f, 10f)]
    public float openSpeed = 4f;
    [Range(1f, 10f)]
    public float closeSpeed = 3f;
    [Tooltip("Force the eye open regardless of hover state. For debugging.")]
    public bool forceOpen = false;

    private float _targetOpenValue = 0f;
    private Quaternion[] _eyeBaseRotations;

    private void Awake()
    {
        if (eyesAnimator == null)
            eyesAnimator = GetComponentInChildren<FEyesAnimator>();
        if (_camera == null)
            _camera = Camera.main;

        // FEyesAnimator still runs each frame for blinking (EyeLids bones).
        // We drive the iris bones (Eyes[]) ourselves so we null the track target.
        eyesAnimator.EyesTarget = null;
        eyesAnimator.MinOpenValue = 0f;
    }

    private void Start()
    {
        // Capture eye bone world rotations in their Edit-mode rest pose, with the
        // baseEulerOffset correction baked in so they represent "iris facing camera."
        // This runs before the Animator's first evaluation, giving a stable neutral base.
        if (eyesAnimator.Eyes == null || eyesAnimator.Eyes.Count == 0) return;

        Quaternion correction = Quaternion.Euler(baseEulerOffset);
        _eyeBaseRotations = new Quaternion[eyesAnimator.Eyes.Count];
        for (int i = 0; i < eyesAnimator.Eyes.Count; i++)
            _eyeBaseRotations[i] = correction * eyesAnimator.Eyes[i].rotation;
    }

    private void OnEnable()
    {
        Actions.OnHoverOverTargetCloud += OnHoverCloud;
        Actions.OnHoverExit            += OnHoverExit;
        Actions.GetClickedCloud        += OnClicked;
        Actions.ConversationEnded      += OnConversationEnded;
    }

    private void OnDisable()
    {
        Actions.OnHoverOverTargetCloud -= OnHoverCloud;
        Actions.OnHoverExit            -= OnHoverExit;
        Actions.GetClickedCloud        -= OnClicked;
        Actions.ConversationEnded      -= OnConversationEnded;
    }

    // Runs after FEyesAnimator (execution order 200 > 16).
    private void LateUpdate()
    {
        FollowCursor();
        ApplyIrisLook();
        AnimateOpenClose();
    }

    private void FollowCursor()
    {
        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector3 cursorWorld = _camera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, cursorDepth));

        Transform anchor = pivotBone != null ? pivotBone : eyesAnimator?.HeadReference;

        if (anchor != null)
        {
            Vector3 boneOffset = anchor.position - transform.position;
            transform.position = cursorWorld - boneOffset;
        }
        else
        {
            transform.position = cursorWorld;
        }
    }

    private void ApplyIrisLook()
    {
        if (_eyeBaseRotations == null || eyesAnimator.Eyes == null) return;

        Vector2 mouse = Mouse.current.position.ReadValue();
        Ray ray = _camera.ScreenPointToRay(new Vector3(mouse.x, mouse.y, 0f));

        // How much the cursor ray deviates from straight ahead, in world space
        Quaternion deviation = Quaternion.FromToRotation(_camera.transform.forward, ray.direction);
        // Clamp so the iris never tilts more than maxLookAngle degrees
        deviation = Quaternion.RotateTowards(Quaternion.identity, deviation, maxLookAngle);

        int count = Mathf.Min(eyesAnimator.Eyes.Count, _eyeBaseRotations.Length);
        for (int i = 0; i < count; i++)
            eyesAnimator.Eyes[i].rotation = deviation * _eyeBaseRotations[i];
    }

    private void AnimateOpenClose()
    {
        float target = forceOpen ? 1f : _targetOpenValue;
        float speed = (target > eyesAnimator.MinOpenValue) ? openSpeed : closeSpeed;
        eyesAnimator.MinOpenValue = Mathf.MoveTowards(eyesAnimator.MinOpenValue, target, Time.deltaTime * speed);
    }

    private void OnHoverCloud(GameObject _) => _targetOpenValue = 1f;
    private void OnHoverExit()              => _targetOpenValue = 0f;
    private void OnClicked(GameObject _)    => _targetOpenValue = 0f;
    private void OnConversationEnded()      => _targetOpenValue = 0f;
}
