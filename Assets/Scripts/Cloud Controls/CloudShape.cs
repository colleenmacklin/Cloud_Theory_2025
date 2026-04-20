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

    [Header("Variable Timings and Scales")]
    [SerializeField]
    [Tooltip("Min and Max for Random timings behind clouds changing")] //CM added 4/19
    public float changeTimeMin;
    public float changeTimeMax;

    [Tooltip("default was 10.0f")]
    public float minScale = 1;
    public float maxScale = 10;
    public float scale = 1;
    public float currScale;
    [SerializeField]
    public Vector3 scaleRatio;

    [SerializeField]
    [Tooltip("visible sky bounds = ")]
    public int cloudVisX = -70; // TODO: Adjust

    [SerializeField]
    [Tooltip("Set to Quad renderer")]
    private Renderer shapeRenderer;

    public bool isGameLoop = true;
    private FadeObjectInOut _fadeObject;
    //bool to be set to true when cloud is nearing edge of screen and moved to the other side
    private bool _cloudIsBusyResetting = false;
    public bool keepDebugObjectsVisible = false;

    //to be set by the fadeobject in/out object (not very elegant)
    //public bool IsFading;


    private void OnEnable()
    {
        Actions.ChangeCloudShape += SetShape;
        Actions.ClarifyClouds += ClarifyCloud;
        Actions.SlowdownClouds += SlowDownCloud;
        Actions.StopClouds += StopCloud;
        //Actions.SharpenCloud += SharpenCloud;
        //Actions.BlurCloud += BlurCloud;
        Actions.OnHoverOverTargetCloud += GlowCloud;
        Actions.OnHoverExit += UnGlowCloud;
        Actions.LookAtCamera += lookatcamera;
        Actions.FadeInCloud += fadeInParticleSystem;
        Actions.FadeOutCloud += fadeOutParticleSystem;
        Actions.InactivateCloud += inactivateCloud;
        //Actions.SetRandomScale += scaleMe;
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
        Actions.OnHoverOverTargetCloud -= GlowCloud;
        Actions.OnHoverExit -= UnGlowCloud;
        Actions.LookAtCamera -= lookatcamera;
        Actions.FadeInCloud -= fadeInParticleSystem;
        Actions.FadeOutCloud -= fadeOutParticleSystem;
        Actions.InactivateCloud -= inactivateCloud;
        //Actions.SetRandomScale -= scaleMe;
        //_fadeObject.ResetCloudPos -= ResetCloudPos; 
    }

    //Transform _camTransform;

    private void Awake()
    {
        psShape = ps.shape; // do not forget to set this first! will throw null reference exception
        psShape.texture = defaultShape;
        currentShape = psShape.texture;
        //_fadeObject = GetComponent<FadeObjectInOut>();

    }

    //In start we look at the camera and
    //We set the collider reference
    private void Start()
    {
        //scaleMe(5f);
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
        //transform.LookAt(camera, Vector3.back);
        //adjustScaleRatio();

    
    }

    private void Update()
    {

        //transform.LookAt(_camTransform, Vector3.back);
        /*
        if (!_cloudIsBusyResetting)
        {
            CheckCloudVis();
        }
        else
        {
          //  if(_fadeObject.IsFading)
        //    { 
                //in progress 
           // }
        }
        
        if (Input.GetKeyDown(KeyCode.C))
        {
            fadeOutParticleSystem();
        }
        */
    }
    /*
    public void scaleMe(float scaleNum)
    {
        Debug.Log("Scaling_me: " + scaleNum);
        //ps.
        Vector3 newScale = new Vector3(scaleNum, scaleNum, 2f);
        transform.localScale = newScale;
    }
*/
    private void lookatcamera(Camera c)
    {
        if (c != null)
        {
            transform.LookAt(c.transform.position, Vector3.back);
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x,transform.rotation.eulerAngles.y,0);
        }
    }
    //checks to see if cloud is close to being offscreeen, and if it is, starts fade out and reposition sequence
    //bool to prevent it from calling once sequence has started
    private void CheckCloudVis()
    {
        if (transform.position.x <= cloudVisX)
        {
            _cloudIsBusyResetting = true;
            //_fadeObject.StopAllCoroutines();
            //StartCoroutine(_fadeObject.FadeCloudInOut());
        }
    }

    //moves cloud to other side of screen 
    //sets flag to false so it starts checking for being offscreen again
    private void ResetCloudPos()
    {
        Vector3 cloudPos = transform.position;
        //todo: should probs think abt wind changing exponentially 
        //like stronger when on sides of screen etc
        //also to stop while u r watching cloud
        cloudPos.x = 210;
        transform.position = cloudPos;
        _cloudIsBusyResetting = false;
    }

    public void GlowCloud(GameObject cloud)
    {
        if(this.gameObject == cloud)
        {
            Debug.Log("GLOWCLOUD" + this.CurrentShapeName);
            ShowShape();
            //outline.SetActive(true); //TODO: add a script to the outline for greater control
        }

    }
    public void UnGlowCloud()
    {
        //Debug.Log("UNGLOWCLOUD: "+ this.CurrentShapeName);
        //outline.SetActive(false); //TODO: add a script to the outline for greater control
        HideShape();
    }

    private void ShowShape()
    {
        Texture2D myShape = ps.shape.texture;
        Highlighter.SetActive(true);
        Actions.FadeIn50?.Invoke(Highlighter);

        MeshRenderer quadRenderer = Highlighter.GetComponent<MeshRenderer>();
        quadRenderer.bounds = matchBounds(ps.shape, Highlighter);
        Material quadMaterial = quadRenderer.material;
        quadMaterial.mainTexture = myShape;
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
            HideShape();
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
        TurnOnCollider(); //makes this cloud seeable by the raycaster
        incomingShape = shapeTexture;
        psShape.scale = ScaleToShape(incomingShape);
        psShape.texture = incomingShape;

        currentShape = incomingShape;
        //Set the scale and texture value in the particle system shape module
        adjustScaleRatio();

        CurrentShapeName = currentShape.name;
        StartCoroutine(TimeToChange());

    }

        public void SetGenericShape(Texture2D shapeTexture)
    {
        TurnOffCollider();
        incomingShape = shapeTexture;
        psShape.scale = ScaleToShape(incomingShape);
        psShape.texture = incomingShape;

        currentShape = incomingShape;
        //Set the scale and texture value in the particle system shape module
        adjustScaleRatio();

        CurrentShapeName = currentShape.name;

        StartCoroutine(TimeToChange());

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
        var ratio = Mathf.Max(scale / srcWidth, scale / srcHeight);

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
