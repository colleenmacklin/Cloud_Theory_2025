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

    [Header("Blink Control")]
    [Tooltip("BlinkFrequency while idle (not focused on a target cloud).")]
    [Range(0.35f, 4f)]
    public float idleBlinkFrequency = 1f;
    [Tooltip("BlinkFrequency while hovering/reading a target cloud — higher reads as more attentive.")]
    [Range(0.35f, 4f)]
    public float focusedBlinkFrequency = 2.5f;

    [Header("Pupil Focus Effect")]
    [Tooltip("Renderer of the pupil/iris mesh. Dilates and tints while focused on a target cloud.")]
    public Renderer pupil;
    [Tooltip("How much the pupil scales up (relative to its rest scale) at full focus.")]
    [Range(1f, 3f)]
    public float dilatedScale = 1.4f;
    [Tooltip("How quickly dilation, blink frequency, and the rainbow tint ease toward their target.")]
    [Range(0.1f, 10f)]
    public float focusBlendSpeed = 4f;
    [Tooltip("How strongly the rainbow hue is mixed into the pupil at full focus. Keep this low — it's meant to be a subtle shimmer, not a full color swap.")]
    [Range(0f, 1f)]
    public float rainbowIntensity = 0.2f;
    [Tooltip("How fast the rainbow hue cycles while focused.")]
    [Range(0.1f, 5f)]
    public float rainbowCycleSpeed = 1f;

    private float _targetOpenValue = 0f;
    private bool _isFocused = false;
    private float _focusBlend = 0f;
    private float _hue = 0f;
    private Quaternion[] _eyeBaseRotations;
    private Vector3 _pupilBaseScale;
    private Material _pupilMat;
    private Color _pupilBaseColor;
    private Color _pupilBaseEmission;

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
        // FEyesAnimator would otherwise recompute BlinkFrequency itself every frame
        // from RandomMovementPreset, clobbering whatever we set in AnimateBlinkFrequency().
        eyesAnimator.SyncWithRandomPreset = false;
    }

    private void Start()
    {
        // Capture eye bone world rotations in their Edit-mode rest pose, with the
        // baseEulerOffset correction baked in so they represent "iris facing camera."
        // This runs before the Animator's first evaluation, giving a stable neutral base.
        if (eyesAnimator.Eyes != null && eyesAnimator.Eyes.Count > 0)
        {
            Quaternion correction = Quaternion.Euler(baseEulerOffset);
            _eyeBaseRotations = new Quaternion[eyesAnimator.Eyes.Count];
            for (int i = 0; i < eyesAnimator.Eyes.Count; i++)
                _eyeBaseRotations[i] = correction * eyesAnimator.Eyes[i].rotation;
        }

        if (pupil != null)
        {
            _pupilBaseScale = pupil.transform.localScale;
            _pupilMat = pupil.material; // instances it, so the shared source material is untouched
            _pupilBaseColor = _pupilMat.HasProperty("_BaseColor") ? _pupilMat.GetColor("_BaseColor") : Color.black;
            _pupilBaseEmission = _pupilMat.HasProperty("_EmissionColor") ? _pupilMat.GetColor("_EmissionColor") : Color.black;
            _pupilMat.EnableKeyword("_EMISSION");
        }
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

        _focusBlend = Mathf.MoveTowards(_focusBlend, _isFocused ? 1f : 0f, Time.deltaTime * focusBlendSpeed);
        AnimateBlinkFrequency();
        AnimatePupilDilation();
        AnimateRainbowTint();
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

    private void AnimateBlinkFrequency()
    {
        eyesAnimator.BlinkFrequency = Mathf.Lerp(idleBlinkFrequency, focusedBlinkFrequency, _focusBlend);
    }

    private void AnimatePupilDilation()
    {
        if (pupil == null) return;
        pupil.transform.localScale = _pupilBaseScale * Mathf.Lerp(1f, dilatedScale, _focusBlend);
    }

    private void AnimateRainbowTint()
    {
        if (_pupilMat == null) return;

        if (_focusBlend > 0f)
            _hue = Mathf.Repeat(_hue + Time.deltaTime * rainbowCycleSpeed, 1f);

        Color rainbow = Color.HSVToRGB(_hue, 1f, 1f);
        float blend = rainbowIntensity * _focusBlend;

        _pupilMat.SetColor("_BaseColor", Color.Lerp(_pupilBaseColor, rainbow, blend));
        _pupilMat.SetColor("_EmissionColor", Color.Lerp(_pupilBaseEmission, rainbow, blend));
    }

    private void OnHoverCloud(GameObject _)
    {
        _targetOpenValue = 1f;
        _isFocused = true;
    }

    private void OnHoverExit()
    {
        _targetOpenValue = 0f;
        _isFocused = false;
    }

    private void OnClicked(GameObject _)
    {
        _targetOpenValue = 0f;
        _isFocused = false;
        eyesAnimator.ForceBlink();
    }

    private void OnConversationEnded()
    {
        _targetOpenValue = 0f;
        _isFocused = false;
    }
}
