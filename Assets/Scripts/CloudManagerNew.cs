using System.Collections.Generic;
using UnityEngine;

public class CloudManagerNew : MonoBehaviour
{
    //The CloudManager: 
    // assigns clouds their shapes (both shaped clouds and "regular" clouds)
    // keeps track of the clouds and their positions,  making sure they are all visible to the player
    // tells the clouds when to change shape
    // keeps a history of the shapes that clouds have turned into
    public List<CloudShape> Clouds;
    public List<Sprite> Shapes;
    public List<Sprite> CloudShapes;
    public GameState gameState;


}
