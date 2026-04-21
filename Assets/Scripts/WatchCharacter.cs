using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WatchCharacter : MonoBehaviour
{
    public Transform Ferret;

    // Update is called once per frame
    void Update()
    {
        // Rotate the camera every frame so it keeps looking at the target
        transform.LookAt(Ferret);

        // Same as above, but setting the worldUp parameter to Vector3.left in this example turns the camera on its side
        transform.LookAt(Ferret, Vector3.left);        
    }
}
