using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;

public class NewNarrator : MonoBehaviour
{
    public TMP_Dropdown allcloudNames;
    public TMP_Text Narration;
    public List<string> cloudHistory;
    public List<string> targetClouds;
    public List<string> allClouds;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnEnable()
    {
      Actions.RespondToShape += respond_to_shape;  
    }

        private void OnDisable()
    {
      Actions.RespondToShape -= respond_to_shape;  
    }

    void Start()
    {
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

public void respond_to_shape(string s)
    {
        cloudHistory.Add(s);
        Debug.Log("cloudhistory: "+cloudHistory.Last());

        var shape = parseName(s);
        Debug.Log("respond to: "+shape);
        //some more code here
    }
    // Utilities
public string parseName(string s)
    {
        string name = s.Replace("_", " ");
        return name;
    }

}
