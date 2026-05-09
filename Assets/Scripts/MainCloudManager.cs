using UnityEngine;
using System.Collections.Generic;

public class MainCloudManager : MonoBehaviour
{
    //The CloudManager: 
    // assigns clouds their shapes (both shaped clouds and "regular" clouds)
    // keeps track of the clouds and their positions,  making sure they are all visible to the player
    // tells the clouds when to change shape
    // keeps a history of the shapes that clouds have turned into
    public List<CloudShape> Clouds;
    public List<Texture2D> CloudShapes;
    public List<Texture2D> GenericCloudShapes;
    public List<string> allClouds;
    public float CloudStartScale;
    public CloudShape clickedCloud;
    [SerializeField]
    [Tooltip("How many of the clouds to turn into targets")]
    private int numberOfTargetsToGenerate = 1;
    [SerializeField]
    private List<string> cloudsSelectedHistory;
    [SerializeField]
    private List<string> cloudsActiveHistory;
    [SerializeField]
    private List<Texture2D> finalCloudTextures;

    private Queue<Texture2D> _remainingTargets = new Queue<Texture2D>();

    public State gameState;
    private void OnEnable()
    {
      Actions.GetClickedCloud += GetClickedCloud;
      Actions.ConversationEnded += InactivateCloud;
      Actions.Speak += OnNarratorSpeak;
      Actions.CloudIsReady += OnCloudReady;
    }

    private void OnDisable()
    {
        Actions.GetClickedCloud -= GetClickedCloud;
        Actions.ConversationEnded -= InactivateCloud;
        Actions.Speak -= OnNarratorSpeak;
        Actions.CloudIsReady -= OnCloudReady;
    }
    void Start()
    {
        //Actions.SetRandomScale?.Invoke(CloudStartScale);

        //populate the dropdown menu for the debug scene
        //pull from the list of Shapes.names
        foreach (Texture2D shape in CloudShapes)
        {
            allClouds.Add(shape.name);
        }
        ///
        /// 
        setUpInitialClouds();
    }

    private void setUpInitialClouds()
    {
        Clouds.Shuffle();
        CloudShapes.Shuffle();
        GenericCloudShapes.Shuffle();

        for (int i = 0; i < Clouds.Count; i++)
        {
            Clouds[i].SetGenericShape(GenericCloudShapes[i % GenericCloudShapes.Count]);
            Clouds[i].TurnOffCollider();
        }

        for (int i = 0; i < numberOfTargetsToGenerate; i++)
        {
            Clouds[i].SetShape(CloudShapes[i]);
        }

        // Queue up the remaining target shapes for later reassignment
        _remainingTargets.Clear();
        for (int i = numberOfTargetsToGenerate; i < CloudShapes.Count; i++)
            _remainingTargets.Enqueue(CloudShapes[i]);
    }

    public void GetClickedCloud(GameObject c) //from Raycaster
    {
        clickedCloud = c.GetComponent<CloudShape>();
        Debug.Log(c.name+" clicked: " + clickedCloud.CurrentShapeName);
        Actions.ChooseCloud?.Invoke(clickedCloud.CurrentShapeName); // sends name to narrator

        cloudsSelectedHistory.Add(clickedCloud.CurrentShapeName);
        finalCloudTextures.Add(clickedCloud.currentShape);
    }

    private void OnNarratorSpeak()
    {
        if (clickedCloud != null)
            clickedCloud.GlowCloud(clickedCloud.gameObject);
    }

    private void OnCloudReady(CloudShape cloud)
    {
        if (_remainingTargets.Count > 0)
            cloud.SetShape(_remainingTargets.Dequeue());
        else
            cloud.SetGenericShape(GenericCloudShapes[Random.Range(0, GenericCloudShapes.Count)]);
    }

    private void InactivateCloud()
    {
        Actions.InactivateCloud?.Invoke(clickedCloud);
    }

}
