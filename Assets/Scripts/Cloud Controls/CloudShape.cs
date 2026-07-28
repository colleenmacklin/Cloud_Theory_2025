using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Events;
using static UnityEngine.ParticleSystem;
/*

    The CloudShape contains all the controlling behavior for all the particle system
    manipulation required for our game. We can additionally define more behavior
    as needed going forward.

    //probably should create random range timers for changing a clouds shape on the cloud, and signal the CloudManager when it is ready to be changed

    The primary function is the SetTexture method which will rescale the images into

*/
// TO DO

public class CloudShape : MonoBehaviour
{
    [Header("Nested Objects")]
    [SerializeField]
    public GameObject collider_object;
    public GameObject cloudSystem;
    public GameObject Highlighter;
    public BoxCollider cloudCollider;

    [SerializeField]
    private bool isHighlighted = false;
    [SerializeField]
    private ParticleSystem ps;
    [SerializeField]
    private ParticleSystem.ShapeModule psShape;
    public Texture2D defaultShape;
    public float colliderScaleFactor = 1.5f;

    [Header("Control Properties")]
    [SerializeField]
    public Transform location;
    public Texture2D currentShape;
    public Texture2D incomingShape;
    public string CurrentShapeName;
    public bool ready;
    public bool isTarget; //cloudManager checks this to see if the cloud is a target shape, i.e. a shape of something, not just a generic cloud
    public float timeLeft;
    //public GameObject outline;
    // Assigned once by MainCloudManager: this cloud's cell in the (elevation x azimuth)
    // grid over the sky region, so both top-to-bottom AND left-to-right coverage stay
    // even across both initial placement and later respawns.
    [HideInInspector] public int elevationBand = -1;
    [HideInInspector] public int azimuthBand = -1;

    [Header("Variable Timings and Scales")]
    [SerializeField]
    [Tooltip("Min and Max for Random timings behind clouds changing")] //CM added 4/19
    public float changeTimeMin;
    public float changeTimeMax;

    [Tooltip("default was 10.0f")]
    public float minScale = 8;
    public float maxScale = 12;
    public float scale = 8;
    public float currScale;
    [SerializeField]
    public Vector3 scaleRatio;

    [Header("Particle Start Size")]
    [Tooltip("Particle system startSize range (random between two constants) at the reference transform scale (minScale). Scales up proportionately as the cloud's randomized transform scale grows toward maxScale.")]
    public float particleStartSizeMin = 20f;
    public float particleStartSizeMax = 30f;

    [SerializeField]
    [Tooltip("visible sky bounds = ")]
    public int cloudVisX = -70; // TODO: Adjust

    [SerializeField]
    [Tooltip("Set to Quad renderer")]
    private Renderer shapeRenderer;

    public bool isGameLoop = true;
    private FadeObjectInOut _fadeObject;
    private float _startX;               // world X at scene start
    private float _wrapCooldown;         // seconds remaining before the respawn check is allowed
    //bool to be set to true when cloud is nearing edge of screen and moved to the other side
    private bool _cloudIsBusyResetting = false;
    private bool _narratorSpeaking = false;
    public bool keepDebugObjectsVisible = false;
    private Coroutine _changeCoroutine;
    private MainCloudManager _cloudManager;
    // leftExitX/rightSpawnX/zMin/zMax are authored as local offsets from the CloudManager
    // parent; these are the resolved world-space bounds actually used at runtime.
    private float _worldLeftExitX, _worldRightSpawnX, _worldZMin, _worldZMax;

    [Header("Sky Orientation")]
    [Tooltip("Clouds orient their top toward this point. Create an empty GameObject high in the sky above the scene centre and assign it here. Leave unset to default to (0, 1000, 0).")]
    public Transform SkyCenter;

    [Header("Wind Movement")]
    [Tooltip("The WindZone used for wind speed (Prefab_Wind).")]
    public WindZone windZone;
    [Tooltip("Direction clouds travel. Defaults to left (-X). Does not have to match WindZone orientation.")]
    public Vector3 windDirection = Vector3.left;
    [Tooltip("Multiplier on the WindZone's main speed.")]
    public float windSpeedMultiplier = 1f;
    [Tooltip("X at which the cloud is considered off-screen to the left and triggers a respawn. Local offset from the CloudManager parent transform, not a world coordinate — so the whole rig can be repositioned without retuning these.")]
    public float leftExitX = -70f;
    [Tooltip("X at which the cloud reappears after wrapping around. Local offset from the CloudManager parent transform.")]
    public float rightSpawnX = 210f;
    [Tooltip("How quickly the cloud rotates to face the camera as it drifts. Higher = snappier.")]
    [Range(0.1f, 10f)] public float rotationSpeed = 3f;
    [Tooltip("Seconds for the cloud to fade out before wrapping, and fade in after respawning.")]
    [Range(0.5f, 30f)] public float cloudFadeDuration = 5f;
    [Tooltip("Z range for cloud placement, as a local offset from the CloudManager parent transform. Randomized on each respawn to vary depth.")]
    public float zMin = -30f;
    public float zMax = 30f;


    private void OnEnable()
    {
        Actions.ChangeCloudShape += SetShape;
        Actions.ClarifyClouds += ClarifyCloud;
        Actions.SlowdownClouds += SlowDownCloud;
        Actions.StopClouds += StopCloud;
        //Actions.SharpenCloud += SharpenCloud;
        //Actions.BlurCloud += BlurCloud;
        Actions.LookAtCamera += lookatcamera;
        Actions.FadeInCloud += fadeInParticleSystem;
        Actions.FadeOutCloud += fadeOutParticleSystem;
        Actions.InactivateCloud += inactivateCloud;
        Actions.Speak += OnNarratorSpeak;
        Actions.ConversationEnded += OnConversationEnded;
        Actions.SetRandomScale += scaleMe;
        //_fadeObject.ResetCloudPos += ResetCloudPos;
    }

    private void OnDisable()
    {
        Actions.ChangeCloudShape -=SetShape;
        Actions.ClarifyClouds -= ClarifyCloud;
        Actions.SlowdownClouds -= SlowDownCloud;
        Actions.StopClouds -= StopCloud;
        //Actions.SharpenCloud -= SharpenCloud;
        //Actions.BlurCloud -= BlurCloud;
        Actions.LookAtCamera -= lookatcamera;
        Actions.FadeInCloud -= fadeInParticleSystem;
        Actions.FadeOutCloud -= fadeOutParticleSystem;
        Actions.InactivateCloud -= inactivateCloud;
        Actions.Speak -= OnNarratorSpeak;
        Actions.ConversationEnded -= OnConversationEnded;
        Actions.SetRandomScale -= scaleMe;
        //_fadeObject.ResetCloudPos -= ResetCloudPos; 
    }

    //Transform _camTransform;

    private void Awake()
    {
        psShape = ps.shape; // do not forget to set this first! will throw null reference exception
        psShape.texture = defaultShape;
        currentShape = psShape.texture;
        _fadeObject = GetComponentInChildren<FadeObjectInOut>();

        _cloudManager = GetComponentInParent<MainCloudManager>();
        Transform origin = transform.parent != null ? transform.parent : transform;
        _worldLeftExitX = origin.position.x + leftExitX;
        _worldRightSpawnX = origin.position.x + rightSpawnX;
        _worldZMin = origin.position.z + zMin;
        _worldZMax = origin.position.z + zMax;

        // Done in Awake (not Start) so transform.localScale is already final by the time
        // MainCloudManager.Start() runs its placement pass — it reads EstimateVisualRadius()
        // (which depends on this scale) to keep clouds from overlapping, and Unity
        // guarantees every object's Awake() completes before any object's Start() runs.
        scaleMe(Random.Range(minScale, maxScale));
    }

    //In start we look at the camera and
    //We set the collider reference
    private void Start()
    {
        ps.Play(); //start particle system
        //set collider size
        Highlighter.GetComponent<Renderer>().bounds = matchBounds(ps.shape, Highlighter);
        collider_object.GetComponent<Renderer>().bounds = matchBounds(ps.shape, collider_object);
        //cloudCollider = collider_object.GetComponent<BoxCollider>();
        collider_object.transform.localScale = ScaleToShape(ps.shape.texture);
        Highlighter.transform.localScale = ScaleToShape(ps.shape.texture);

        if (!keepDebugObjectsVisible)
        {
            Actions.FadeOut?.Invoke(collider_object);
            Actions.FadeOut50?.Invoke(Highlighter);
        }

        //rotate to look at the camera
        lookatcamera(Camera.main);

        _startX = transform.position.x;
        _wrapCooldown = 1f; // prevent wrap check from firing on the very first frame

        Debug.Log($"[CloudShape] {name}: Start pos.x={_startX:F1}  leftExitX(world)={_worldLeftExitX:F1}  fadeObject={(_fadeObject != null ? _fadeObject.gameObject.name : "NULL")}");

        //transform.LookAt(camera, Vector3.back);
        //adjustScaleRatio();

    
    }

    private void OnNarratorSpeak() => _narratorSpeaking = true;
    private void OnConversationEnded() => _narratorSpeaking = false;

    private void Update()
    {
        if (!isHighlighted && !_narratorSpeaking)
        {
            MoveWithWind();
            SmoothFaceCamera();
            if (!_cloudIsBusyResetting)
                CheckCloudVis();
        }
    }

    private void SmoothFaceCamera()
    {
        Camera c = Camera.main;
        if (c == null) return;
        Quaternion target = DomeOrientation(c.transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    // Computes a billboard rotation that faces toward viewerPos using a
    // wind-axis reference for the dome "north" — this avoids the singularity
    // that world-up causes when a cloud is near the apex directly overhead.
    private Quaternion DomeOrientation(Vector3 viewerPos)
    {
        Vector3 outward = transform.position - viewerPos;
        if (outward.sqrMagnitude < 0.001f) return transform.rotation;
        outward.Normalize();
        Vector3 toViewer = -outward;

        // Use the axis perpendicular to the wind in the horizontal plane as the
        // dome's "north" reference. Because this is horizontal, its projection
        // onto the sphere tangent plane stays non-zero even at the apex (where
        // outward ≈ Vector3.up), so overhead clouds never spin.
        Vector3 northRef = Vector3.Cross(windDirection.normalized, Vector3.up);
        if (northRef.sqrMagnitude < 0.001f)
            northRef = Vector3.forward; // safety: wind was vertical
        northRef.Normalize();

        float dot = Vector3.Dot(northRef, outward);
        Vector3 upOnDome = northRef - dot * outward;

        // Fallback for the rare case where the cloud is directly along northRef
        // from the viewer (e.g. straight ahead perpendicular to the wind).
        if (upOnDome.sqrMagnitude < 0.01f)
        {
            float dotUp = Vector3.Dot(Vector3.up, outward);
            upOnDome = Vector3.up - dotUp * outward;
        }

        if (upOnDome.sqrMagnitude < 0.001f)
            return transform.rotation;

        return Quaternion.LookRotation(toViewer, upOnDome.normalized);
    }

    private void MoveWithWind()
    {
        if (windZone == null) return;
        float speed = windZone.windMain * windSpeedMultiplier;
        transform.position += windDirection.normalized * speed * Time.deltaTime;
    }

    private void CheckCloudVis()
    {
        if (windZone == null) return;
        if (_wrapCooldown > 0f) { _wrapCooldown -= Time.deltaTime; return; }

        if (transform.position.x <= _worldLeftExitX)
        {
            Debug.Log($"[CloudShape] {name}: Respawn triggered. pos.x={transform.position.x:F1}");
            _cloudIsBusyResetting = true;
            StartCoroutine(RespawnCloud());
        }
    }

    private IEnumerator RespawnCloud()
    {
        TurnOffCollider();

        ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();

        // Stop new particle emission, then fade existing particles out.
        ps.Stop();
        if (psRenderer != null)
            yield return StartCoroutine(FadeCloudAlpha(psRenderer, 1f, 0f, cloudFadeDuration));

        // Reposition within the marker-defined sky region — the same dome sampling the
        // initial layout uses — rather than the old flat world-space wind-conveyor
        // teleport, so respawned clouds land back in the intended visible area instead
        // of drifting toward wherever leftExitX/rightSpawnX/zMin/zMax happen to point.
        transform.position = _cloudManager != null
            ? _cloudManager.PickDomeRespawnPosition(this)
            : FallbackRespawnPosition();
        _startX = transform.position.x;

        lookatcamera(Camera.main);

        // Restart emission, then fade back in.
        ps.Play();
        if (psRenderer != null)
            yield return StartCoroutine(FadeCloudAlpha(psRenderer, 0f, 1f, cloudFadeDuration));

        _cloudIsBusyResetting = false;
    }

    // Used only if no MainCloudManager reference is available (e.g. this cloud isn't
    // parented under one): the original flat world-space wind-conveyor teleport, with a
    // local physics overlap check standing in for the manager's angular-gap check.
    private Vector3 FallbackRespawnPosition()
    {
        float cycleWidth = _worldRightSpawnX - _worldLeftExitX;
        Vector3 pos = transform.position;
        pos.x += cycleWidth;

        Vector3 halfExtents = cloudCollider.bounds.extents;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float z = Random.Range(_worldZMin, _worldZMax);
            Collider[] hits = Physics.OverlapBox(new Vector3(pos.x, pos.y, z), halfExtents, Quaternion.identity);
            bool overlapping = false;
            foreach (Collider hit in hits)
            {
                if (hit.transform.root != transform.root)
                { overlapping = true; break; }
            }
            if (!overlapping) { pos.z = z; return pos; }
        }
        pos.z = Random.Range(_worldZMin, _worldZMax); // fallback if no clear spot found after 10 tries
        return pos;
    }

    public IEnumerator FadeOutAndDeactivate(float duration)
    {
        _cloudIsBusyResetting = true;
        TurnOffCollider();
        ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            float currentAlpha = psRenderer.material.color.a;
            yield return StartCoroutine(FadeCloudAlpha(psRenderer, currentAlpha, 0f, duration));
        }
        ps.Stop();
        gameObject.SetActive(false);
    }

    private IEnumerator FadeCloudAlpha(ParticleSystemRenderer psRenderer, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Color c = psRenderer.material.color;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            psRenderer.material.color = c;
            yield return null;
        }
        Color final = psRenderer.material.color;
        final.a = to;
        psRenderer.material.color = final;
    }
    
    public void scaleMe(float scaleNum)
    {
        Debug.Log("Scaling_me: " + scaleNum);
        Vector3 newScale = new Vector3(scaleNum, scaleNum, 2f);
        transform.localScale = newScale;

        // Keep particle startSize proportionate to the transform scale, using minScale
        // as the 1x reference point (so bigger clouds get proportionately bigger particles).
        float sizeRatio = scaleNum / minScale;
        var particleMain = ps.main;
        particleMain.startSize = new MinMaxCurve(
            particleStartSizeMin * sizeRatio,
            particleStartSizeMax * sizeRatio
        );
    }

    // Rough estimate of this cloud's visual radius in world units — the shape-fit spawn
    // volume plus the overhang of its largest particles — used by MainCloudManager to
    // keep clouds from overlapping enough to obscure their shapes. Requires scaleMe() to
    // have already run (it's called from Awake, so transform.localScale is final by the
    // time anything reads this in Start-order code).
    public float EstimateVisualRadius()
    {
        float shapeRadius = scale * 0.5f * transform.localScale.x;
        float sizeRatio = transform.localScale.x / Mathf.Max(0.01f, minScale);
        float particleRadius = particleStartSizeMax * sizeRatio * 0.5f;
        return shapeRadius + particleRadius;
    }

    private void lookatcamera(Camera c)
    {
        if (c == null) return;
        transform.rotation = DomeOrientation(c.transform.position);
    }

    public void GlowCloud(GameObject cloud)
    {
        if(this.gameObject == cloud)
        {
            Debug.Log("GLOWCLOUD" + this.CurrentShapeName);
            isHighlighted = true;
            // Pause the shape-change countdown so the cloud won't morph while the narrator is talking about it.
            if (_changeCoroutine != null) { StopCoroutine(_changeCoroutine); _changeCoroutine = null; }
            ShowShape();
        }
    }

    public void UnGlowCloud()
    {
        isHighlighted = false;
        HideShape();
    }

    private void ShowShape()
    {
        Texture2D myShape = ps.shape.texture;
        Highlighter.transform.localScale = ScaleToShape(myShape);
        Highlighter.SetActive(true);
        MeshRenderer quadRenderer = Highlighter.GetComponent<MeshRenderer>();
        Material quadMaterial = quadRenderer.material;
        quadMaterial.SetTexture("_MainTex", myShape);
        Actions.FadeIn50?.Invoke(Highlighter);
        Debug.Log($"[ShowShape] texture={(myShape ? myShape.name : "NULL - no texture on ps.shape!")} " +
                  $"scale={Highlighter.transform.localScale} shader={quadMaterial.shader.name} " +
                  $"mainTex={(quadMaterial.GetTexture("_MainTex") ? quadMaterial.GetTexture("_MainTex").name : "null")} rendererEnabled={quadRenderer.enabled}");
    }

    [ContextMenu("Force Show Shape (Debug)")]
    private void ForceShowShape()
    {
        Texture2D myShape = ps.shape.texture;
        Highlighter.transform.localScale = ScaleToShape(myShape);
        Highlighter.SetActive(true);
        MeshRenderer quadRenderer = Highlighter.GetComponent<MeshRenderer>();
        Material quadMaterial = quadRenderer.material;
        quadMaterial.mainTexture = myShape;
        quadMaterial.color = new Color(1f, 1f, 1f, 1f);
        quadRenderer.enabled = true;
        Debug.Log($"[ForceShowShape] texture={myShape?.name} shader={quadMaterial.shader.name} " +
                  $"scale={Highlighter.transform.localScale} color={quadMaterial.color}");
    }
    public void HideShape()//should this be public?
    {
        Actions.FadeOut50?.Invoke(Highlighter);
        Highlighter.SetActive(false);
    }

    public void inactivateCloud(CloudShape c)
    {
        if(this == c)
        {
            isTarget = false;   // clear before UnGlowCloud so OnCloudReady can assign generic
            UnGlowCloud();      // resets isHighlighted → resumes wind movement
            TurnOffCollider();
            StartCoroutine(resetMe());
        }
    }
        IEnumerator resetMe()
    {
        Debug.Log("2. reset cloud");

        //Start a variable timer countdown to signal when the cloud is ready to change
        //enable some variable timings for clouds to start changing shape
        float timing = Random.Range(changeTimeMin, changeTimeMax);
        for (timeLeft = timing; timeLeft > 0; timeLeft -= Time.deltaTime)
        yield return null;
        Actions.CloudIsReady?.Invoke(this);
        yield return new WaitForSeconds(timing);
    }

    public void TurnOnCollider()
    {
        //collider_object.SetActive(true);
        cloudCollider.enabled = true;
    }

    public void TurnOffCollider()
    {
        cloudCollider.enabled = false;
        //collider_object.SetActive(false);
    }

    Vector3 ScaleToShape(Texture2D shape)
    {
        var srcWidth = shape.width;
        var srcHeight = shape.height;
        Vector3 textureScaleAdjustment = CalculateSquareScaleRatio(srcWidth, srcHeight);
        Debug.Log($"[ScaleToShape] shape={shape.name} srcWidth={srcWidth} srcHeight={srcHeight} " +
                  $"scaleAdjustment={textureScaleAdjustment}");
        return textureScaleAdjustment;
    }

    Bounds matchBounds(ShapeModule psShape, GameObject g)
    {
        Texture2D myShape = psShape.texture;
        Vector3 scale = psShape.scale;
        Vector3 positionOffset = psShape.position;

        Vector3 center = positionOffset;
        Vector3 size = scale;

        Bounds localBounds = new Bounds(center, size);
        Matrix4x4 localToWorldMatrix = ps.transform.localToWorldMatrix;

        Bounds worldBounds = TransformBounds(localBounds, localToWorldMatrix);
        
        return worldBounds;
    }
    //cm added coroutine to this 4/15

    //SetShape takes a texture (and sets it after a rescale)
    //this also sets the collider size to update with it
    public void SetShape(Texture2D shapeTexture)
    {
        if (isHighlighted) return;
        isTarget = true;
        TurnOnCollider(); //makes this cloud seeable by the raycaster
        incomingShape = shapeTexture;
        psShape.scale = ScaleToShape(incomingShape);
        psShape.texture = incomingShape;

        currentShape = incomingShape;
        //Set the scale and texture value in the particle system shape module
        adjustScaleRatio();

        CurrentShapeName = currentShape.name;
        _changeCoroutine = StartCoroutine(TimeToChange());
    }

    public void SetGenericShape(Texture2D shapeTexture)
    {
        if (isHighlighted) return;
        isTarget = false;
        TurnOffCollider();
        incomingShape = shapeTexture;
        psShape.scale = ScaleToShape(incomingShape);
        psShape.texture = incomingShape;

        currentShape = incomingShape;
        //Set the scale and texture value in the particle system shape module
        adjustScaleRatio();

        CurrentShapeName = currentShape.name;
        _changeCoroutine = StartCoroutine(TimeToChange());
    }

    private void adjustScaleRatio()
    {
        Debug.Log("1. adjustScaleRatio");
        var srcWidth = currentShape.width;
        var srcHeight = currentShape.height;
        Vector3 textureScaleAdjustment = CalculateSquareScaleRatio(srcWidth, srcHeight);
        //adjust the scale of the particle system shape
        psShape.scale = textureScaleAdjustment;
        //adjust the scale of the highlight
        Highlighter.transform.localScale = textureScaleAdjustment;
        //adjust the scale of the collider
        cloudCollider.transform.localScale = textureScaleAdjustment;

        //adjust the particle size to the scale
        //var psMain = ps.main;
        //psMain.startSizeMultiplier = scale / 2;
        CurrentShapeName = currentShape.name;
        //Set the scale *of the collider* that represents the shape
        //Collider is rotated, so the values are x and y.
        //And the 7f arbbitrarily for "best fit"
        /*
        Vector3 colliderSize = new Vector3(
            5f * textureScaleAdjustment.x / 7f,
            5f * textureScaleAdjustment.y / 7f,
            2f
        );
*/
        //cloudCollider.size = colliderSize;
    }
    IEnumerator TimeToChange()
    {
        Debug.Log("2. set Change TImer");

        //Start a variable timer countdown to signal when the cloud is ready to change
        //enable some variable timings for clouds to start changing shape
        float timing = Random.Range(changeTimeMin, changeTimeMax);
        for (timeLeft = timing; timeLeft > 0; timeLeft -= Time.deltaTime)
        yield return null;
        Actions.CloudIsReady?.Invoke(this);
        //yield return new WaitForSeconds(timing);
    }

    ////////////////
    //
    //    Utility Function
    //
    ///////////////////


    //Calculates a SQUARE aspect ratio ***SCALE*** for the image texture in particle system
    //This makes it look even if the base image is something like 1024x512
    //It does require that the long edge of an image be 1024. Otherwise we might run into problems
    //Does not actually change the texture, we should probably move to 1024x1024 in the long run.
    //The 10f and 10f are our chosen  minimums.
    //private Vector3 CalculateSquareScaleRatio(float srcWidth, float srcHeight, float minWidth = 10f, float minHeight = 10f)
    private Vector3 CalculateSquareScaleRatio(float srcWidth, float srcHeight)

    {
        //TODO: we might need to move this into cloudManager for greater control, and also for different contexts like the Opening
        //for now, I have a check to see if we're in the Gameloop, and if not, the scale is set by the manager (such as in the opening.)
        //if (isGameLoop)
        //{
            //adding a randomizer here for variable sizes
            //Debug.Log("gameState = GameLoop");
            //scale = UnityEngine.Random.Range(minScale, maxScale);
            //currScale = scale; //just surfacing to the interface for debugging
        //}

        //if (!isGameLoop)
        //{
            //currScale = scale; //just surfacing to the interface for debugging
        //}
        // Fit the longest edge to `scale` so neither axis exceeds it.
        var ratio = Mathf.Min(scale / srcWidth, scale / srcHeight);

        var newsize = new Vector3(srcWidth * ratio, srcHeight * ratio, 1f);

        return newsize;
    }

    private Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        Vector3 max = bounds.max;
        Vector3 min = bounds.min;
        
        Vector3 p1 = matrix.MultiplyPoint(new Vector3(max.x, max.y, max.z));
        Vector3 p2 = matrix.MultiplyPoint(new Vector3(max.x, max.y, min.z));
        Vector3 p3 = matrix.MultiplyPoint(new Vector3(max.x, min.y, max.z));
        Vector3 p4 = matrix.MultiplyPoint(new Vector3(max.x, min.y, min.z));
        Vector3 p5 = matrix.MultiplyPoint(new Vector3(min.x, max.y, max.z));
        Vector3 p6 = matrix.MultiplyPoint(new Vector3(min.x, max.y, min.z));
        Vector3 p7 = matrix.MultiplyPoint(new Vector3(min.x, min.y, max.z));
        Vector3 p8 = matrix.MultiplyPoint(new Vector3(min.x, min.y, min.z));
        
        Bounds transformedBounds = new Bounds(p1, Vector3.zero);
        transformedBounds.Encapsulate(p2);
        transformedBounds.Encapsulate(p3);
        transformedBounds.Encapsulate(p4);
        transformedBounds.Encapsulate(p5);
        transformedBounds.Encapsulate(p6);
        transformedBounds.Encapsulate(p7);
        transformedBounds.Encapsulate(p8);

        return transformedBounds;
    }

    ////////////////
    //
    //    Mutation functions
    //       consider adding some state transitions in the future
    /////////////////
    public void ClarifyCloud() //speeds up the cloud so it takes shape faster
    {
        var particleSystemSettings = ps.main;
        particleSystemSettings.simulationSpeed = 0.30f;
        //particleSystemSettings.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
    }

    public void SharpenCloud() //makes the particles smaller, sharper around the underlying shape
    {
        var particleSystemSettings = ps.main;
        //particleSystemSettings.simulationSpeed = 0.30f;
        //particleSystemSettings.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
    }

    public void SharpenOpeningCloud() //TODO: this is a hack for the opening...change all cloud functions to allow passed in values
    {
        var particleSystemSettings = ps.main;
        particleSystemSettings.simulationSpeed = 0.30f;
        particleSystemSettings.startSize = new ParticleSystem.MinMaxCurve(2f, 3f);
        //particleSystemSettings.maxParticles = 4000;
     }

    public void BlurCloud() //makes the particles more diffuse around the underlying shape
    {
        var particleSystemSettings = ps.main;
        particleSystemSettings.simulationSpeed = 0.30f;
        particleSystemSettings.startSize = new ParticleSystem.MinMaxCurve(3f, 10f);
    }

    public void StopCloud()
    {
        var particleSystem = ps;
        particleSystem.Stop();
    }

    public void StartCloud()
    {
        var particleSystem = ps;
        particleSystem.Play();
        //Actions.CloudIsReady(this);
    }

    public void SlowDownCloud()
    {
        var particleSystemSettings = ps.main;
        particleSystemSettings.simulationSpeed = .08f;
    }

    public void fadeInParticleSystem(GameObject g)
    {
        if(g == this.gameObject)
        {
            StartCloud();
            Actions.FadeIn?.Invoke(cloudSystem);
        }
        
        //_fadeObject.FadeIn(_fadeObject.fadeTime);
        /* ultimately, change the simple fadeinout script to a script that "dissolves" the clouds by removing particles over time
      
        var particleSystemSettings = ps.main;
        particleSystemSettings.maxParticles -= particleSystemSettings.maxParticles * (int)Time.deltaTime;
        Debug.Log("particles: " + particleSystemSettings.maxParticles);
        */
    }

    public void fadeOutParticleSystem(GameObject g)
    {
        if(g == this.gameObject)
        {
            StopCloud();
            Actions.FadeOut?.Invoke(cloudSystem);
        }


        //_fadeObject.FadeOut(_fadeObject.fadeTime);

        /* ultimately, change the simple fadeinout script to a script that "dissolves" the clouds by removing particles over time
      
        var particleSystemSettings = ps.main;
        particleSystemSettings.maxParticles -= particleSystemSettings.maxParticles * (int)Time.deltaTime;
        Debug.Log("particles: " + particleSystemSettings.maxParticles);
        */
    }



}
