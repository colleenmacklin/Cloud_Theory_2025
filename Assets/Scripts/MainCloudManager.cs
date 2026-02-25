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
    public List<Texture2D> Shapes;
    public List<Texture2D> CloudShapes;
    public List<string> allClouds;
    public float CloudStartScale = 5f;
    public CloudShape clickedCloud;
    [SerializeField]
    private List<string> cloudsSelectedHistory;
    [SerializeField]
    private List<string> cloudsActiveHistory;
    [SerializeField]
    private List<Texture2D> finalCloudTextures;
    [SerializeField]
    private List<Texture2D> cloudTargetsList; 

    public GameState gameState;
    private void OnEnable()
    {
      Actions.ChooseCloud += changeCloudShape;  
      Actions.GetClickedCloud += GetClickedCloud;
      Actions.ConversationEnded += InactivateCloud;
    }

    private void OnDisable()
    {
        Actions.ChooseCloud -= changeCloudShape;
        Actions.GetClickedCloud -= GetClickedCloud;
        Actions.ConversationEnded -= InactivateCloud;

    }
    void Start()
    {
        //Actions.SetRandomScale?.Invoke(CloudStartScale);

        //populate the dropdown menu
        //pull from the list of Shapes.names
        foreach (Texture2D shape in Shapes)
        {
            allClouds.Add(shape.name);
        }
    }


    void changeCloudShape(string s)
    {
        //Debug.Log("changing shape to: "+s);
        foreach (Texture2D shape in Shapes)
        {
            if (shape.name == s)
            {
                Debug.Log("changing shape to: "+s);
                Actions.ChangeCloudShape?.Invoke(shape);
            }
        }

    }

    public void GetClickedCloud(GameObject c) //from Raycaster
    {
        //write history code
        clickedCloud = c.GetComponent<CloudShape>();
        Debug.Log(c.name+" clicked: " + clickedCloud.CurrentShapeName);
        Actions.ChooseCloud?.Invoke(c.name);
        cloudsSelectedHistory.Add(clickedCloud.CurrentShapeName);
        cloudTargetsList.Remove(clickedCloud.currentShape);
        finalCloudTextures.Add(clickedCloud.currentShape);
    }

    private void InactivateCloud()
    {
        Actions.InactivateCloud?.Invoke(clickedCloud);
    }

}
