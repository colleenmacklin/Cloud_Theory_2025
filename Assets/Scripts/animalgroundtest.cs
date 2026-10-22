using UnityEngine;

public class animalgroundtest : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
void Start() {
    var r = GetComponentInChildren<Renderer>();
    Debug.Log($"Feet offset: {r.bounds.min.y - transform.position.y}");
}

}
