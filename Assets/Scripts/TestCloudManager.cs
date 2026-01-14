using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class TestCloudManager : MonoBehaviour
{
    //testUI
    public TMP_Dropdown allcloudNames;
    private List<string> allClouds;
    public List<CloudShape> Clouds;
    public List<Texture2D> Shapes;
    public List<Texture2D> CloudShapes;



    void Start()
    {
        //populate the dropdown menu
        //TODO: Make this pull from the list of Sprites.names
        allClouds.Add("a_person_watering_a_fish");
        allClouds.Add("a_jumping_cat");
        allClouds.Add("Abraham_Lincoln");
        allClouds.Add("a_person_playing_saxophone");
        allClouds.Add("a_businesswoman");
        allClouds.Add("a_bucket");
        allClouds.Add("a_walrus_with_a_tophat");
        allClouds.Add("a_poodle");
        allClouds.Add("a_cigarette_butt");
        allClouds.Add("a_telephone");
        allClouds.Add("a_unicorn");
        allClouds.Add("a_cloud");
        allcloudNames.AddOptions(allClouds);
    }
}
