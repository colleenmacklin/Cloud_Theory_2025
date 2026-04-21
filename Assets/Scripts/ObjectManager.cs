using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Random = UnityEngine.Random;

public class ObjectManager : MonoBehaviour
{
    [Tooltip("Number of objects that will be spawned during playthrough")]
    public int NumberOfObjectsToSpawn;
    public float TimeBetweenObejcts = 120f;

    [Tooltip("All the objects we have to choose from")]
    public List<GameObject> Objects = new List<GameObject>();

    //List of objects in the order that they will be spawned
    private List<GameObject> _shuffledObjects = new List<GameObject>();


    private int _nextObjectIndex = 0;


    private List<ObjectSpawnPoint> _objectSpawnPoints = new List<ObjectSpawnPoint>();

    private float _timer = 0;

    private Vector3[] _positions;
    [SerializeField]
    private float _radius;
    private bool _allObjectsHaveBeenSpawned = false;

    //TODO move audio stuff via game manager???
    [SerializeField]
    private AudioClip _landingSound;

    [SerializeField]
    private AudioClip _fallingSound;

    [SerializeField]
    private SequencerManager _audioManager;

    //[SerializeField]
    //private Transform _roofTarget;

    //for sfx
    private AudioSource _audioSource;

    public event Action<FallingObject> OnObjectFallen; //todo look and see if we need this cause it currently happens at the same time as it lands

    public event Action<FallingObject>OnObjectLanded;

    FallingObject _currentFallingObject;

    public event Action OnGameLoopFinished;
    public event Action<List<GameObject>> OnFallingObjectsOrdered;
    public event Action<FallingObject> OnNextObjectToFall;

    private bool _movingObjectsToRoof = false;
    private bool _hoveringObjects = false;

    FallingObject[] _fallenObjects;
    Vector3[] _roofTargets;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _positions = new Vector3[NumberOfObjectsToSpawn];

        foreach (ObjectSpawnPoint osp in GetComponentsInChildren<ObjectSpawnPoint>())
        {
            _objectSpawnPoints.Add(osp);
        }
    }

    private void Start()
    {
        SetPositions();
        OrderObjects();
    }

    //set the array of positions for objects to spawn, to set positions move or add an object spawn point under objects and move it physically in scene to where object should be spawned. 
    //object spawn points must be a child of the  object manager prefab in the scene
    private void SetPositions()
    {
        for (int i = 0; i < NumberOfObjectsToSpawn; i++)
        {
            if (_objectSpawnPoints.Count <= 0)
            {
                foreach (ObjectSpawnPoint osp in GetComponentsInChildren<ObjectSpawnPoint>())
                {
                    _objectSpawnPoints.Add(osp);
                }
            }
            //add to position array, a random position of object spawn points
            int index = Random.Range(0, _objectSpawnPoints.Count);
            _positions[i] = _objectSpawnPoints[index].SpawnPoint;

            _objectSpawnPoints.RemoveAt(index);
            // Debug.Log(_positions[i]);
        }
    }

    //set the order in which objects are going to appear
    private void OrderObjects()
    {
        int index = 0;


        if (Objects.Count > 0)
        {
            while (_shuffledObjects.Count < NumberOfObjectsToSpawn)
            {
                index = Random.Range(0, Objects.Count);

                //check if it contains the object already, if not add it
                if (!_shuffledObjects.Contains(Objects[index]))
                {
                    _shuffledObjects.Add(Objects[index]);
                    //Debug.Log(Objects[index].name);
                }

            }

            Debug.Log(_shuffledObjects.Count);
            OnFallingObjectsOrdered?.Invoke(_shuffledObjects); //to GameManager -> Dialogue Manager SetUpDialogues
        }
        else
        {
            Debug.LogWarning("There are no objects referenced in the objects list");
        }
    }

    private void OnObjectLand(ObjectType objType, FallingObject fallingObject)
    {
        Debug.Log(objType.ToString() + " has landed");

        PlayObjectSFX(_landingSound);

        //TODO: this should be moved to a "prepare object to fall phase.
        OnObjectFallen?.Invoke(fallingObject); //GameManager tells DialogueManager fetches remarks for next object to fall

        OnObjectLanded?.Invoke(fallingObject); //for audio sequencers

        _currentFallingObject.OnObjectLanded -= OnObjectLand;
    }

    private void PlayObjectSFX(AudioClip audioClip)
    {
        _audioSource.clip = audioClip;
        _audioSource.Play();
    }

    //TODO: This should migrate to a coRoutine called by GameManager so that we can preload the live dialogues before starting the timer and dropping an object...

    private void Update()
    {
        //test key
        if (Input.GetKeyDown(KeyCode.O))
        {
            DropObject();
        }


       // if (_movingObjectsToRoof)
        //{
            //MoveObjectsToRoof();
        //}


        if (GameManager.CurrentGameState == State.GameLoop)
        {
            //Debug.Log("ObjectManager Ready to drop...");
            if (_timer > 0)
            {
                _timer -= Time.deltaTime;
            }
            else
            {
                if (!_allObjectsHaveBeenSpawned)
                {
                    DropObject();
                    //Get live dialogue for NEXT object to fall

                    GameObject nextGO = _shuffledObjects[_nextObjectIndex];
                    FallingObject nextFO = nextGO.GetComponent<FallingObject>();
                    OnNextObjectToFall?.Invoke(nextFO);
                }
                else
                {
                    OnGameLoopFinished?.Invoke();
                }
                _timer = TimeBetweenObejcts;
            }
        }

    }


/*
    public void SetRoofTargetPositions()
    {
        _fallenObjects = GetComponentsInChildren<FallingObject>();

        float radius = 3 * _fallenObjects.Length; //radius of circle
        float angleStep = 2 * Mathf.PI / _fallenObjects.Length;

        _roofTargets = new Vector3[_fallenObjects.Length];

        for (int i = 0; i < _fallenObjects.Length; i++)
        {
            //might actually just remove the rb completely, but for now set to kinematic so they don't fall down
            _fallenObjects[i].GetComponent<Rigidbody>().isKinematic = true;

            float angle = i * angleStep;

            float xPos = _roofTarget.position.x + radius * Mathf.Cos(angle);
            float yPos = _roofTarget.position.y + 50;
            float zPos = _roofTarget.position.z + radius * Mathf.Sin(angle);

            Vector3 targetPos = new Vector3(xPos, yPos, zPos);
            _roofTargets[i] = targetPos;
        }



    }
*/
/*
    public void SendObjectsToRoof()
    {
        SetRoofTargetPositions();
        // AddHoverObjectComponents();
        _movingObjectsToRoof = true;
    }
    */
    /*
    private void MoveObjectsToRoof()
    {
        //TODO add random speed to each one? maybe not necessary
        for (int i = 0; i < _fallenObjects.Length; i++)
        {
            //Debug.Log("moving");
            if (Vector3.Distance(_fallenObjects[i].transform.position, _roofTargets[i]) > 4f)
            {
                _fallenObjects[i].transform.position = Vector3.Lerp(_fallenObjects[i].transform.position, _roofTargets[i], 0.05f * Time.deltaTime);
            }
            else
            {
                _movingObjectsToRoof = false;
                AddHoverObjectComponents();
                Debug.Log("hover?");
            }

        }
    }
            */
/*
    private void AddHoverObjectComponents()
    {
        foreach (FallingObject obj in _fallenObjects)
        {
            obj.AddComponent<ObjectHover>();
        }
    }

*/
    private void DropObject()
    {
        if ((_nextObjectIndex + 1) < NumberOfObjectsToSpawn) //TODO: test out of range error
        {
            //instatiates the next object on the shuffled object list, at the next position, zero rotation, object manager parent
            Debug.Log("number of objects that are to be spawned: " + NumberOfObjectsToSpawn +" next object index: "+_nextObjectIndex);
            FallingObject newFallingObject = Instantiate(_shuffledObjects[_nextObjectIndex], new Vector3(_positions[_nextObjectIndex].x, 300, _positions[_nextObjectIndex].z), Quaternion.identity, this.transform).GetComponent<FallingObject>();

            newFallingObject.OnObjectLanded += OnObjectLand;

            _currentFallingObject = newFallingObject;

            //PlayObjectSFX(_fallingSound); took this out cause it was confusing sounding

            _nextObjectIndex++;
        }

        else
        {
            _allObjectsHaveBeenSpawned = true;
            Debug.Log("all objects have been spawned");
        }
    }
}
