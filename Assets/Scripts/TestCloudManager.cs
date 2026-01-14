using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class TestCloudManager : MonoBehaviour
{
    //testUI
    public TMP_Dropdown allcloudNames;
    public List<string> allClouds;
    public List<CloudShape> Clouds;
    public List<Texture2D> Shapes;
    public List<Texture2D> CloudShapes;

    private void OnEnable()
    {
      Actions.ChooseCloud += changeCloudShape;  
    }

    private void OnDisable()
    {
      Actions.ChooseCloud -= changeCloudShape;  
    }



    void Start()
    {
        //populate the dropdown menu
        //pull from the list of Shapes.names
        foreach (Texture2D shape in Shapes)
        {
            allClouds.Add(shape.name);
        }
        allcloudNames.AddOptions(allClouds);
    }

    void changeCloudShape(string s)
    {
        //Debug.Log("changing shape to: "+s);
        foreach (Texture2D shape in Shapes)
        {
            if (shape.name == s)
            {
                Debug.Log("changing shape to: "+s);
                Actions.ChangeCloudShape(shape);
            }
        }

    }
}
