using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;




public class FallingObject : MonoBehaviour
{
   [SerializeField]
   private ObjectType _objectType;

    private bool _hasLanded = false;

    public event Action<ObjectType, FallingObject> OnObjectLanded;

    public string objectName;

    public AudioClip SpokenName; //object name spoken out loud for audio sequencer

   public ObjectType GetObjectType
    {
        get { return _objectType; }
    }

    //Added as for prompt-crafting via DialogueManager
    //TODO: check this list against the names we use in google colab for content generation

 public string GetObjectName(FallingObject f)
    {
        switch (_objectType)
        {
            case ObjectType.BabyPenguin:
                objectName = "baby penguin";
                return objectName;
            case ObjectType.Baguette:
                objectName = "baguette";
                return objectName;
            case ObjectType.Banana:
                objectName = "banana";
                return objectName;
            case ObjectType.BlackWidowSpider:
                objectName = "Black Widow spider";
                return objectName;
            case ObjectType.Bleachers:
                objectName = "bleachers";
                return objectName;
            case ObjectType.BrokenBrain:
                objectName = "broken brain";
                return objectName;
            case ObjectType.Butterfly:
                objectName = "butterfly";
                return objectName;
            case ObjectType.Candle:
                objectName = "candle";
                return objectName;
            case ObjectType.Cat:
                objectName = "cat";
                return objectName;
            case ObjectType.ChickenDrumstick:
                objectName = "chicken drumstick";
                return objectName;
            case ObjectType.CigaretteButts:
                objectName = "cigarette butts";
                return objectName;
            case ObjectType.ConvenienceStore:
                objectName = "convenience store";
                return objectName;
            case ObjectType.Crocodile:
                objectName = "crocodile";
                return objectName;
            case ObjectType.CupOfCoffee:
                objectName = "cup of coffee";
                return objectName;
            case ObjectType.Donut:
                objectName = "donut";
                return objectName;
            case ObjectType.FriedEgg:
                objectName = "fried egg";
                return objectName;
            case ObjectType.Giraffe:
                objectName = "giraffe";
                return objectName;
            case ObjectType.GlassOfWine:
                objectName = "glass of wine";
                return objectName;
            case ObjectType.LabradorPuppy:
                objectName = "Labrador puppy";
                return objectName;
            case ObjectType.Leek:
                objectName = "leek";
                return objectName;
            case ObjectType.Lightbulb:
                objectName = "lightbulb";
                return objectName;
            case ObjectType.Molar:
                objectName = "molar";
                return objectName;
            case ObjectType.PayPhone:
                objectName = "pay phone";
                return objectName;
            case ObjectType.Peanut:
                objectName = "peanut";
                return objectName;
            case ObjectType.PekinDuck:
                objectName = "duck";
                return objectName;
            case ObjectType.PizzaSlice:
                objectName = "pizza slice";
                return objectName;
            case ObjectType.Poodle:
                objectName = "poodle";
                return objectName;
            case ObjectType.PrayingMantis:
                objectName = "Praying Mantis";
                return objectName;
            case ObjectType.Pyramid:
                objectName = "pyramid";
                return objectName;
            case ObjectType.RedOnion:
                objectName = "red onion";
                return objectName;
            case ObjectType.Rock:
                objectName = "rock";
                return objectName;
            case ObjectType.Sink:
                objectName = "sink";
                return objectName;
            case ObjectType.SalmonSushi:
                objectName = "salmon sushi";
                return objectName;
            case ObjectType.Snail:
                objectName = "snail";
                return objectName;
            case ObjectType.Snowflake:
                objectName = "snowflake";
                return objectName;
            case ObjectType.SodaCan:
                objectName = "soda can";
                return objectName;
            case ObjectType.Squirrel:
                objectName = "squirrel";
                return objectName;
            case ObjectType.Swan:
                objectName = "swan";
                return objectName;
            case ObjectType.Teapot:
                objectName = "teapot";
                return objectName;
            case ObjectType.Tortoise:
                objectName = "tortoise";
                return objectName;
            case ObjectType.TrafficCone:
                objectName = "traffic cone";
                return objectName;
            case ObjectType.Trophy:
                objectName = "trophy";
                return objectName;
            case ObjectType.TurtleOnATurtle:
                objectName = "turtle on a turtle";
                return objectName;
            case ObjectType.Unicorn:
                objectName = "unicorn";
                return objectName;
            case ObjectType.WaterFountain:
                objectName = "water fountain";
                return objectName;
            case ObjectType.Walrus:
                objectName = "walrus";
                return objectName;
            case ObjectType.XXXBottle:
                objectName = "XXX bottle";
                return objectName;
            default:
                objectName = "stuff";
                return objectName;
        }

    }

    private void OnCollisionEnter(Collision collision)
    {
       // Debug.Log(_objectType + "has collided");

        //flag to detect collision only once - perhaps need to unsubscribe from the event??
        if (!_hasLanded)
        {
              OnObjectLanded?.Invoke(_objectType, this);
            _hasLanded = true;
        }
    }
}



