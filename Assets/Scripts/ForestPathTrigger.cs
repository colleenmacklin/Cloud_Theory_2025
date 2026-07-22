using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to a trigger collider at the forest path entrance.
// Tag the player GameObject as "Player" so this can identify it.
public class ForestPathTrigger : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load (must be in Build Settings).")]
    public string nextSceneName;

    [Tooltip("Seconds to wait before loading — use this to overlap a fade-out if you add one later.")]
    public float transitionDelay = 1f;

    private bool _triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;
        _triggered = true;
        StartCoroutine(LoadNextScene());
    }

    private IEnumerator LoadNextScene()
    {
        yield return new WaitForSeconds(transitionDelay);
        SceneManager.LoadScene(nextSceneName);
    }
}
