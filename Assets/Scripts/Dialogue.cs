using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dialogue
{
    public int sequence; //stores the order in which dialogues appear
    public FallingObject fallingObject { get; set; }
    public string fallingObjectName { get; set; }
    public string prompt { get; set; }
    public List<Character> speakers { get; set; } = new List<Character>();
    public List<string> remarks { get; set; } = new List<string>();//all of the things said about ONE object by various characters
}