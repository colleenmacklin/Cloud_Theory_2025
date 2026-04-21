using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class ObjectSpawnPoint : MonoBehaviour
{
    public Vector3 SpawnPoint
    {
        get { return transform.position; }
    }
}
