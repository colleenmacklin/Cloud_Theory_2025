using System.IO;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using PoissonDisc;
using UnityEngine.UIElements;
using TMPro;


/*

    Cloud Manager is responsible for creating the clouds into an evenly (poisson-distributed) grid
    It is also the source of the event handling, as it can route the event to the clouds. 
    The Manager, then, acts as the cloud conductor as it controls how clouds should be acting

    Events have been mostly shifted over to the clouds

    But generation events and transitions are still on the manager
    * Begin -> GenerateNew
    * StoryEnded -> NewTargets

    //TODO:: BRING THE CLOUD EVENTS INTO THIS MANAGER SO WE CAN CONTROL HOW THEY ALL BEHAVE
    IN TANDEM AND ALSO FIRE EVENTS WHEN THEY FINISH ACTING

    Developmental Note** because poisson positions actually come in an ordered fashion, we 
    can actually use the structure to select for the "middle" if we start there and fan out.
    The fan out naturally allow us to center our target shapes without additional search.
    This is left as a task for a future developer.
*/

public class CloudManager : MonoBehaviour
{
    [Range(0f, 40f)]
    public float pauseBetweenText = 5f;
    public CloudShape clickedCloud;
    public GameState GameState;

    [Header("Cloud Properties")]
    [Tooltip("How many clouds to create")]
    public int numberOfCloudsToGenerate;
    [Tooltip("How many of the clouds to turn into targets")]
    public int numberOfTargetsToGenerate;
    [Tooltip("All the generated clouds in the sky")]
    public List<GameObject> generatedCloudObjects;
    [Tooltip("The base cloud prefab")]
    public GameObject cloudObjectPrefab;
    [Tooltip("delay for making cloud clickable")]
    public int transitionTime;


    [Header("Cloud Data")]//consider refactor as cloud scriptable objects
    public Texture2D[] cloudTargetsArray; //textures stay as an array because we are not generating run time textures
    public List<Texture2D> cloudTargetsList; //should probably try to convert this so we can remove items from the list
    public string model_name; //this is used to set the cloud shape texture arrays - Maybe not needed in new version
    public Texture2D[] cloudGenericsArray; //textures stay as an array because we are not generating run time textures
    [SerializeField]
    private List<string> cloudsSelectedHistory;
    [SerializeField]
    private List<string> cloudsActiveHistory;
    [SerializeField]
    private List<Texture2D> finalCloudTextures;

    [Header("Debug Cloud Selections")]
    [SerializeField]
    public GameObject chosenCloud;
    [SerializeField]
    public Texture2D chosenShape;
    private GameObject shapeCollider; //our testing box that we resuse, will be a GameObject with only a collider on it
    private Bounds shapeBounds;
    private int cloudSelectionIndex = -1;

    [Header("Poisson Settings")]
    //[SerializeField]
    //private Vector2 scaleRange = new Vector2(2f, 2.75f);
    //private Vector2 scaleRange = new Vector2(1f, 1.75f);
    [Tooltip("The collision distance for the poisson discs. Effectively setting grid density")]
    public float poissonRadius = 50;//default is 30
    [Tooltip("The size of the region to create points in")] //This region is from 0,0 and must be translated 
    public Vector2 poissonRegionSize = new Vector2(400f, 300f);//default we will use 150x120
    [Tooltip("How far to translate offset the region")]
    public Vector3 regionTranslation = new Vector3(0f, 0f, 70f);//default is 0,0,300
    [Tooltip("How high the clouds should spawn")]
    public int poisson_y = 100;
    [Tooltip("Number of rejection samples before giving up on a sample. Default is 30 ")]
    public int poissonRejectionSamples = 30;//this can comfortably be a higher number

    ///////////////////////
    //
    // Monobehaviors
    //
    /////////////////////////

    void OnEnable()
    {
        //EventManager.StartListening("Setup", GenerateClouds); //from Storyteller Start()
        //EventManager.StartListening("IntroDone", SetNextShapes); //from Storyteller finishIntro
        Actions.IntroDone += SetNextShapes;

        //EventManager.StartListening("DoneReading", SetNextShapes); //should only apply to the cloud that was being remarked upon - use an Action
        //EventManager.StartListening("DoneReading", SeenCloud);
        Actions.DoneReading += SeenCloud;

        Actions.Setup += GenerateClouds;

        Actions.GetClickedCloud += GetClickedCloud;
        Actions.CloudIsReady += ReadyCloud;
        Actions.FadeInCloud += FadeInCloud;
        Actions.FadeOutCloud += FadeOutCloud;
        Actions.SetEndingClouds += SetEndingClouds;

    }

    void OnDisable()
    {
        //EventManager.StopListening("Setup", GenerateNewClouds);
        //EventManager.StopListening("Setup", GenerateClouds);
        //EventManager.StopListening("IntroDone", SetNextShapes); //from Storyteller finishIntro
        Actions.IntroDone -= SetNextShapes;

        //EventManager.StopListening("DoneReading", SeenCloud); //should only apply to the cloud that was being remarked upon - use an Action
        Actions.DoneReading -= SeenCloud;

        //EventManager.StopListening("DoneReading", SetNextShapes); //should only apply to the cloud that was being remarked upon - use an Action
        Actions.Setup -= GenerateClouds;
        Actions.GetClickedCloud -= GetClickedCloud;
        Actions.CloudIsReady -= ReadyCloud;
        Actions.FadeInCloud -= FadeInCloud;
        Actions.FadeOutCloud -= FadeOutCloud;
        Actions.SetEndingClouds -= SetEndingClouds;


    }
    void Awake()
    {
        //FIX - remove this

        //check to see that the modelURL was passed on from the opening, and if so, assign public vars
        if (string.IsNullOrEmpty(ModelInfo.ModelName))
        {
            Debug.Log("No model URL, defaulting to Philosopher");
            ModelInfo.ModelName = "philosopher";
        }
        model_name = ModelInfo.ModelName;

        SetTextureArrays(model_name); //more intensive(?), so we do this in awake
    }

    void Start()
    {
        Actions.Setup();
        //Start Act Intro, not the cloud shape
        //        EventManager.TriggerEvent("Introduction");
        //       EventManager.TriggerEvent("SpawnShape");
    }

    void SetTextureArrays(string model) 
    {

        switch (model)
        {
            case "philosopher":
                cloudTargetsArray = Resources.LoadAll("Philosopher_Cloud_Targets", typeof(Texture2D)).Cast<Texture2D>().ToArray();
                break;
            case "comedian":
                cloudTargetsArray = Resources.LoadAll("Comedian_Cloud_Targets", typeof(Texture2D)).Cast<Texture2D>().ToArray();
                break;
            case "primordial_earth":
                cloudTargetsArray = Resources.LoadAll("Primordial_Earth_Cloud_Targets", typeof(Texture2D)).Cast<Texture2D>().ToArray();
                break;
            default:
                cloudTargetsArray = Resources.LoadAll("Philosopher_Cloud_Targets", typeof(Texture2D)).Cast<Texture2D>().ToArray();
                break;
        }

        cloudTargetsList = new List<Texture2D>(cloudTargetsArray);
        cloudGenericsArray = Resources.LoadAll("Cloud_Natural", typeof(Texture2D)).Cast<Texture2D>().ToArray();
    }

   //FIX - remove this
   
    void OnValidate()    //called in the editor only

    {
        //check to see that the modelURL was passed on from the opening, and if so, assign public vars
        if (string.IsNullOrEmpty(ModelInfo.ModelName))
        {
            Debug.Log("No model URL, defaulting to Philosopher");
            ModelInfo.ModelName = "philosopher";
        }
        model_name = ModelInfo.ModelName;
        SetTextureArrays(model_name);
    }
   

    ///////////////////////
    //
    // Cloud Functions
    //
    /////////////////////////

    //Generate Clouds
    //1. creates the points grid where it will put the clouds in
    //2. instantiates the clouds in those positions, based on an offset
    //3. use the Cloud's SetShape function to set targets and generics
    //4. Fires a Clouds Generated event
    private void GenerateClouds()
    {
        Debug.Log("generating clouds");
        //generate points first using settings
        List<Vector2> poissonPositions;
        poissonPositions = PoissonDiscSampling.GeneratePoints(poissonRadius, poissonRegionSize, poissonRejectionSamples);
        Vector3 gv3 = new Vector3(poissonRegionSize.x, 0, poissonRegionSize.y); //make a 3d vector from vector2
        Debug.Log("gv3: " + gv3);
        Vector3 center = new Vector3(gv3.x / 2, poisson_y, gv3.z / 2); //center point
        Vector2 center2 = new Vector2(gv3.x / 2, gv3.z / 2); //2d vector used to sort list from center
        Vector3 _scale = new Vector3(0, 0, 0);

        //find center

        //Debugging
        /*
        for (int i = 0; i < poissonPositions.Count; i++)
        {
            Vector3 candidatePosition = new Vector3(poissonPositions[i].x, poisson_y, poissonPositions[i].y);
            Vector3 _shift = new Vector3(candidatePosition.x - center.x, candidatePosition.y, candidatePosition.z - center.z); //need to test and eyeball based on player POV
            Vector3 candidatePosition_shift = ScaleAndShiftVector(candidatePosition, _shift, _scale);
            Debug.Log("Center = " + center.x + "," + center.z);
            Debug.Log("Before Sorting"+i+" -- "+candidatePosition_shift.x +","+ candidatePosition_shift.z);
        }

        */
        //poissonPositions are in clockwise order, we shuffle them
        //poissonPositions = ShuffleList(poissonPositions);
        //
        //Sort list in order of points closest to the center of the distribution area, outwards
        poissonPositions.Sort((v1, v2) => (v1 - center2).sqrMagnitude.CompareTo((v2 - center2).sqrMagnitude));
        /*
        //DEBUGGING
        for (int i = 0; i < poissonPositions.Count; i++)
        {
            Vector3 candidatePosition = new Vector3(poissonPositions[i].x, poisson_y, poissonPositions[i].y);
            Vector3 _shift = new Vector3(candidatePosition.x - center.x, candidatePosition.y, candidatePosition.z - center.z); //need to test and eyeball based on player POV
            Vector3 candidatePosition_shift = ScaleAndShiftVector(candidatePosition, _shift, _scale);
            Debug.Log(i + " -- " + candidatePosition_shift.x + "," + candidatePosition_shift.z);

        }
        */
        GameObject go;//temp gameObject we use
        //GameObject _positions;//temp gameObject we use

        //Points are all random, so we can just use as many of them as we need
        for (int i = 0; i < numberOfCloudsToGenerate; i++)
        {
            if (i >= poissonPositions.Count)
            {
                Debug.Log($"Out of possible positions, found {i - 1} positions out of {numberOfCloudsToGenerate}");
                break;
            }

            Vector3 candidatePosition = new Vector3(poissonPositions[i].x, poisson_y, poissonPositions[i].y);
            Vector3 _shift = new Vector3(candidatePosition.x - center.x, candidatePosition.y, candidatePosition.z - center.z); //need to test and eyeball based on player POV
            Vector3 candidatePosition_shift = ScaleAndShiftVector(candidatePosition, _shift, _scale);

            //convert Poisson Position into Cloud space by translation
            Vector3 cloudPosition = candidatePosition_shift;

            //instantiate the prefab
            go = Instantiate(cloudObjectPrefab, cloudPosition, Quaternion.Euler(0f, 0f, 0f), transform);

            //float sizeScale = UnityEngine.Random.Range(scaleRange.x, scaleRange.y);
            //Vector3 scaleVector = new Vector3(sizeScale, sizeScale, sizeScale);

            //get a random scale for the transform
            //go.transform.localScale = scaleVector; //CM: Need to change to scale the underlying shape - which will unify the fluffiness of the clouds, but differentiate their shapes.
            go.name = $"Cloud {i}";

            var _fadeObject = go.GetComponent<FadeObjectInOut>();
            _fadeObject.fadeDelay = UnityEngine.Random.Range(3, 10);
            _fadeObject.fadeTime = UnityEngine.Random.Range(6, 12);

            generatedCloudObjects.Add(go);
        }


        //Actions.CloudsGenerated();
        Actions.SlowdownClouds();
        //should add some generic shapes here...
        SetCloudsToGenericShapes();
    }



    //Set all clouds to some generic cloud (non target) Shapes
    //Note this is simpler because we do not care about repeats here
    void SetCloudsToGenericShapes()
    {
        //Shuffle clouds shape array
        cloudGenericsArray = ShuffleArray(cloudGenericsArray);

        //loop through all clouds

        for (int i = 0; i < generatedCloudObjects.Count; i++)
        {
            //Get the shape component
            CloudShape cloud = generatedCloudObjects[i].GetComponent<CloudShape>();
            //Tell the cloud to handle the texture
            cloud.SetShape(cloudGenericsArray[i % cloudGenericsArray.Length]);
            cloud.TurnOffCollider(); //TODO: is this neccessary? Could cause problems when re-placing clouds as they scroll offscreen
            cloud.isTarget = false; //mark this cloud as a non-targetable cloud
        }
    }

   

    //Set specified number of clouds to the target shapes
    //Is uncontrolled to an extent, entirely random -- we might want to change that so that we can vary where these appear 
    //Create a comparison list as we go along so we do not repeat shapes set to set
    void SetCloudsToTargetShapes()
    {
        //shuffle possible targets
        cloudTargetsList = ShuffleList(cloudTargetsList);

        //shuffle the generated clouds
        generatedCloudObjects = ShuffleList(generatedCloudObjects);

        //Store the next set of active targets so we compare them later
        List<string> incomingActiveTargets = new List<string>();

        for (int i = 0; i < numberOfTargetsToGenerate; i++)
        {

            int indexOffset = 0;
            if (i >= generatedCloudObjects.Count)
            {
                Debug.Log($"Out of potential clouds to convert to target, reached {i} of {numberOfTargetsToGenerate}.");
                break;
            }
            //Get the shape component
            CloudShape cloud = generatedCloudObjects[i].GetComponent<CloudShape>();

            Texture2D nextShape;

            nextShape = cloudTargetsList[(i + indexOffset) % cloudTargetsList.Count];
     

            //compare current texture with existing selections
            //if it's already been used, then we skip forward in the deck
            //Debug.Log("Cloud CurrentShapeName: " + cloud.CurrentShapeName);

                while (cloudsActiveHistory.Contains(nextShape.name) || cloudsSelectedHistory.Contains(nextShape.name) || nextShape.name == cloud.CurrentShapeName)
                {
                //Debug.Log("shape was previously seen");
                cloudTargetsList.Remove(nextShape);
                //make ending list
                finalCloudTextures.Add(nextShape);

                indexOffset++;

                nextShape = cloudTargetsList[(i + indexOffset) % cloudTargetsList.Count];

                //if we've gone through all the options, break out.
                if (indexOffset >= cloudTargetsList.Count)
                {
                    Debug.Log("Looped through all possible targets but could not find non-duplicate, breaking");
                    break;
                }
            }

            //stop particles
            //StopCloud(cloud);

            //Tell the cloud to handle the texture
            cloud.SetShape(nextShape);
            cloud.isTarget = true; //mark this cloud as a target shape
            StartCoroutine(waitToMakeClickable(cloud));//this gets applied to all clouds - but should only apply to clouds that are "transitioning"
            incomingActiveTargets.Add(nextShape.name);
        }

        //replace cloud shapes in sky with new set of targets
        cloudsActiveHistory = incomingActiveTargets;
    }


    //---------For future implementation, call from Narrator after a shaped cloud has been selected
    void SetSingleCloudToGenericShape(CloudShape c) //for a future action on a single cloud called from narrator
    {
        if (c.ready)
        {
            Debug.Log("SetSingleCloudToGeneric Called on : " + c.name);

            //Shuffle clouds shape array
            cloudGenericsArray = ShuffleArray(cloudGenericsArray);

            //loop through all clouds
            CloudShape cloud = c;
            //pick a random cloudGeneric from the cloudGenericsArray
            int index = UnityEngine.Random.Range(0, cloudGenericsArray.Length);
            //Tell the cloud to handle the texture
            cloud.SetShape(cloudGenericsArray[index]);
            c.isTarget = false; //marks the cloud as a generic shape

            cloud.TurnOffCollider();
        }
    }

    //Set single cloud to a shape
    void SetSingleCloudToShape(CloudShape c)
    {
        if (c.ready && c.name != "" &&  cloudTargetsList.Count > 0 && cloudsActiveHistory.Count <=3)
        {
            Debug.Log("SetSingleCloudToShape Called on : " + c.name);
            CloudShape cloud = c;


            //pick a random cloud shape from the cloudTargetsList
            cloudTargetsList = ShuffleList(cloudTargetsList);
            //int index = UnityEngine.Random.Range(0, cloudTargetsList.Count);
            Texture2D nextShape = cloudTargetsList[0];

            
                int indexOffset = 0;
                //check to make sure that there's no repeats of cloud shapes currently in the sky
                while (cloudsActiveHistory.Contains(nextShape.name)) {
                    indexOffset++;

                    nextShape = cloudTargetsList[(indexOffset) % cloudTargetsList.Count];

                    //if we've gone through all the options, break out.
                    if (indexOffset >= cloudTargetsList.Count)
                    {
                        Debug.Log("Looped through all possible targets but could not find non-duplicate, breaking");
                        break;
                    }

                }

            cloud.SetShape(nextShape); // if it's a List
            c.isTarget = true; //marks the cloud as a target shape
            StartCoroutine(waitToMakeClickable(cloud));//this gets applied to all clouds - but should only apply to clouds that are "transitioning"
            cloudsActiveHistory.Add(nextShape.name);
            //c.ready = false; //use this if we want the clouds that are shapes to stick around for a while
        }
    }

    private void SetEndingClouds(List<string> cloudTexture)
    {

        //Debug.Log("cloudTextures for ending list: " + cloudTexture[0]);
        int indexOffset = 0;

        foreach (GameObject c in generatedCloudObjects)
        {
            Debug.Log("index: " + indexOffset + " c: " + c.name +"generatedCloudObjects List Count: "+ generatedCloudObjects.Count);
            CloudShape cloud = c.GetComponent<CloudShape>();

            //int index = UnityEngine.Random.Range(0, cloudTargetsList.Count);
            if (indexOffset < finalCloudTextures.Count)
            {
                Texture2D nextShape = finalCloudTextures[indexOffset];
                cloud.SetShape(nextShape);
            }
            else {
                cloud.fadeOutParticleSystem();
                cloud.enabled = false;
            }
            indexOffset++;
        }


    }
    private void SeenCloud() //called from "DoneReading" Event in TextBoxController.NextLine()
    {
        if (GameState.Gameloop && clickedCloud) {
            CloudShape c = clickedCloud;
            Debug.Log("Seen this cloud, done reading = " + c.name);
            cloudsActiveHistory.Remove(clickedCloud.currentShape.name);
            c.ready = true; //a boolean on cloud objects to keep clouds from changng before it's time to change...such as when it's being talked about
            SetSingleCloudToGenericShape(c); //turns the most recently discussed cloud to a generic shape
                                             //should change another generic cloud to a shape 
        }
        else {return;}

    }

    //inactive
    IEnumerator waitToMakeClickable(CloudShape c)
    {

        //we need to wait until the cloud has stopped transitioning - which is based on a guess!
        yield return new WaitForSeconds(transitionTime);

        //Start particle system
        //StartCloud(c);
        c.TurnOnCollider();

    }


    public void ReadyCloud(CloudShape c)
    {
        //check to see if cloud is going out of bounds
        //probably should make a seperate function for this, and make a timer that calls it as an action every x seconds from cloud objects that are "Ready"
        Vector3 myPosition = c.transform.position;
        if (myPosition.x < c.cloudVisX)
        {
            //TODO: Make this a call to cloud manager to remomve and reinstantiate (with a nice fade effect) fadeout and move...
            //TODO: location can be tracked by thie cloud object, but fading and moving the cloud to the right should probably be called from the cloudManager so that it can use the poisson function to re-instantiate and then avoid overlapping clouds

            /*
            fadeOutParticleSystem();
            transform.position = new Vector3(myPosition.x*-1, myPosition.y, myPosition.z);
            fadeInParticleSystem();
            */
        Debug.Log(c.name + " x is: " + myPosition.x + " which is out of visibleX: " + c.cloudVisX);

        }

        if (c == clickedCloud)
        {
            //wait until conversation is ended to change shape
            c.ready = false;
        }
        //set this cloud to a new shape --CM 5/31
        //TODO need to check the array of shapes—should not be repeating
        //TODO might want to check on the type of shape the cloud is (generic or target) and then change accordingly
        //TODO adjust the timing
        //TODO decide whether to make this cloud a shape or a generic
        //Debug.Log("cloud shape is: "+c.currentShape);

        if (c.isTarget)
        {
            cloudsActiveHistory.Remove(c.CurrentShapeName);
            SetSingleCloudToGenericShape(c);
            //Debug.Log("Cloud: " + c + "Is ready");
        }
        else
        {
            SetSingleCloudToShape(c);
            //Debug.Log("Cloud: " + c + "Is ready");
        }
    }

    /*
    public void GenerateNewClouds() //called from "setup" event, via storyteller, Start() 
    {
        //GenerateClouds();
        SetNextShapes();
    }
    */
    public void SetNextShapes() //Only called when introDone Event is triggered
    {
        //SetCloudsToGenericShapes(); //should be applied to only the cloud remarked upon from narrator
        SetCloudsToTargetShapes(); // TODO: debug - seems that over time, all clouds end up being shapes! Also will need to make sure there are no repeats
    }


    //////////////////
    //
    //   Event Behavior
    //
    //////////////////////

    //Two versions of the Cloud Actions from the manager level
    //But because events can't take furhter data right now, we would need to add yet another function
    //To wrap around this.


    // TODO:: Set up these manager level functions because they let us fire events when
    // all the cloud actions are **DONE** 
    // That is actually pretty important!!!
    IEnumerable PerformCloudActionOverTime(Action<CloudShape> cloudAction, float totalExecutionTIme)
    {
        float interval = totalExecutionTIme / generatedCloudObjects.Count;

        foreach (GameObject go in generatedCloudObjects)
        {
            CloudShape c = go.GetComponent<CloudShape>();
            cloudAction(c);
            yield return new WaitForSeconds(interval);
        }
    }
    void PerformCloudAction(Action<CloudShape> cloudAction)
    {
        foreach (GameObject go in generatedCloudObjects)
        {
            CloudShape c = go.GetComponent<CloudShape>();
            cloudAction(c);
        }
    }

    //These are added at the manager level in case we need to do any high order scope comparisons
    //Otherwise, the event driven form is fine.


    void SlowDownCloud(CloudShape c)
    {
        c.SlowDownCloud();
    }
    void ClarifyCloud(CloudShape c)
    {
        c.ClarifyCloud();
    }

    void SharpenCloud(CloudShape c)
    {
        c.SharpenCloud();
        Debug.Log("SharpenCloud: " + c);

    }

    void BlurCloud(CloudShape c)
    {
        c.BlurCloud();
        Debug.Log("Blur: " + c);

    }


    void StopCloud(CloudShape c)
    {
        c.StopCloud();
    }

    void StartCloud(CloudShape c)
    {
        c.StartCloud();
    }


    ///////////////////////
    //
    //  Utilities
    //
    ///////////////////////////////

    //Keep Track of Clicked Clouds

    public void GetClickedCloud(GameObject c) //from storyteller
    {
        //write history code
        clickedCloud = c.GetComponent<CloudShape>();
        Debug.Log(c.name+" clicked: " + clickedCloud.CurrentShapeName);
        cloudsSelectedHistory.Add(clickedCloud.CurrentShapeName);
        cloudTargetsList.Remove(clickedCloud.currentShape);
        finalCloudTextures.Add(clickedCloud.currentShape);
    }


    //Generic Shuffler
    List<T> ShuffleList<T>(List<T> list)
    {
        List<T> shuffledResult = list;
        int n = list.Count;
        while (n > 1)
        {
            n--;//simplifies the shuffledResult[n] down below

            int i = UnityEngine.Random.Range(0, n + 1);
            var temp = shuffledResult[i];

            shuffledResult[i] = shuffledResult[n];
            shuffledResult[n] = temp;
        }
        return shuffledResult;

    }


    T[] ShuffleArray<T>(T[] array)
    {
        T[] shuffledResult = array;
        int n = array.Length;
        while (n > 1)
        {
            n--;//simplifies the shuffledResult[n] down below

            int i = UnityEngine.Random.Range(0, n + 1);
            var temp = shuffledResult[i];

            shuffledResult[i] = shuffledResult[n];
            shuffledResult[n] = temp;
        }
        return shuffledResult;

    }


    //TODO: MOVE to UTILITIES
    Vector3 ScaleAndShiftVector(Vector3 v, Vector3 shift, Vector3 scale)
    {
        return Vector3.Scale(v, scale) + shift + regionTranslation;
    }


    private IEnumerator PauseBeforeTalking()
    {

        //EventManager.TriggerEvent("Talk");
        Actions.Talk();
        yield return new WaitForSeconds(pauseBetweenText);

    }

    private void TurnOffCloud()
    {
        //EventManager.TriggerEvent("TurnOffCloud"); //message received on cloud object
        Actions.TurnOffCloud();
    }
    //for prefab instantiation, see: https://docs.unity3d.com/Manual/InstantiatingPrefabs.html

    //Both of these fade methods aren't called from here yet - but could be if we need to choreograph this

    private void FadeInCloud(GameObject c)
    {
        //fadein

        var cloudToFade = c.GetComponent<CloudShape>();
        cloudToFade.fadeInParticleSystem();

    }

    private void FadeOutCloud(GameObject c)
    {
        //fadeout
        var cloudToFade = c.GetComponent<CloudShape>();
        cloudToFade.fadeOutParticleSystem();

    }



}
