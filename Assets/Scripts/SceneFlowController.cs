using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lives in the persistent (always-loaded) scene. Manages walking the player
/// through a fixed, ordered sequence of additively-loaded scenes.
///
/// Setup:
/// 1. Put this on a GameObject in your persistent/bootstrap scene.
/// 2. Fill in `sceneOrder` with your scene names in walk order, e.g.
///    ["Scene1_Mechanic", "Scene2_Mechanic", "Scene3_Mechanic"].
/// 3. In each of those scenes, add one EntryPoint component marking where
///    the player should appear when arriving FROM the previous scene.
/// 4. In each scene, add a SceneTransitionTrigger near the exit edge that
///    calls RequestTransition(currentSceneIndex) when the player enters it.
/// 5. Load sceneOrder[0] additively at game start (see Start() below).
/// </summary>
public class SceneFlowController : MonoBehaviour
{
    public static SceneFlowController Instance { get; private set; }

    [Header("Ordered list of scene names to walk through")]
    [SerializeField] private string[] sceneOrder;

    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Preload behavior")]
    [Tooltip("Start loading the next scene this many meters before the player reaches the trigger's own transition point, if the trigger supports early preload.")]
    [SerializeField] private float preloadLeadDistance = 5f;

    [Header("Fade (optional, assign a CanvasGroup with a black image, leave empty to skip)")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.35f;

    private int currentSceneIndex = -1;
    private bool isTransitioning = false;
    private AsyncOperation pendingLoad;
    private int pendingLoadIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private IEnumerator Start()
    {
        // Load the first scene in the sequence additively at game start.
        if (sceneOrder != null && sceneOrder.Length > 0)
        {
            yield return SceneManager.LoadSceneAsync(sceneOrder[0], LoadSceneMode.Additive);
            currentSceneIndex = 0;
            PlacePlayerAtEntry(0, fromIndex: -1);
        }
    }

    /// <summary>
    /// Call this early (e.g. from a "preload zone" trigger a few meters before
    /// the actual exit) to start streaming the next scene in the background
    /// without moving the player yet.
    /// </summary>
    public void BeginPreload(int fromSceneIndex)
    {
        int nextIndex = fromSceneIndex + 1;
        if (nextIndex >= sceneOrder.Length) return;
        if (pendingLoad != null && pendingLoadIndex == nextIndex) return; // already preloading this one

        pendingLoadIndex = nextIndex;
        pendingLoad = SceneManager.LoadSceneAsync(sceneOrder[nextIndex], LoadSceneMode.Additive);
        pendingLoad.allowSceneActivation = true; // additive loads are cheap; activate as soon as ready
    }

    /// <summary>
    /// Call this when the player physically crosses the exit boundary of
    /// fromSceneIndex. Handles: finishing/starting the load of the next
    /// scene, moving the player, then unloading the previous scene.
    /// </summary>
    public void RequestTransition(int fromSceneIndex)
    {
        if (isTransitioning) return;
        int nextIndex = fromSceneIndex + 1;
        if (nextIndex >= sceneOrder.Length)
        {
            Debug.Log("SceneFlowController: reached the end of the scene sequence.");
            return;
        }
        StartCoroutine(TransitionRoutine(fromSceneIndex, nextIndex));
    }

    private IEnumerator TransitionRoutine(int fromIndex, int toIndex)
    {
        isTransitioning = true;

        yield return Fade(1f); // fade to black (skips instantly if no CanvasGroup assigned)

        // Make sure the next scene is loaded (uses the preloaded one if BeginPreload was already called).
        if (pendingLoad == null || pendingLoadIndex != toIndex)
        {
            var load = SceneManager.LoadSceneAsync(sceneOrder[toIndex], LoadSceneMode.Additive);
            yield return load;
        }
        else
        {
            while (!pendingLoad.isDone) yield return null;
        }
        pendingLoad = null;
        pendingLoadIndex = -1;

        PlacePlayerAtEntry(toIndex, fromIndex);

        yield return Fade(0f); // fade back in

        // Unload the scene we just left, now that the player and camera have moved out of it.
        Scene sceneToUnload = SceneManager.GetSceneByName(sceneOrder[fromIndex]);
        if (sceneToUnload.IsValid() && sceneToUnload.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(sceneToUnload);
        }

        currentSceneIndex = toIndex;
        isTransitioning = false;
    }

    private void PlacePlayerAtEntry(int sceneIndex, int fromIndex)
    {
        Scene scene = SceneManager.GetSceneByName(sceneOrder[sceneIndex]);
        EntryPoint entry = FindEntryPointInScene(scene);

        if (entry == null)
        {
            Debug.LogWarning($"SceneFlowController: no EntryPoint found in '{sceneOrder[sceneIndex]}'. Player position unchanged.");
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("SceneFlowController: player reference not assigned.");
            return;
        }

        // Disable a CharacterController briefly if present, since it fights direct position sets.
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.SetPositionAndRotation(entry.transform.position, entry.transform.rotation);

        if (cc != null) cc.enabled = true;
    }

    private EntryPoint FindEntryPointInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            EntryPoint found = root.GetComponentInChildren<EntryPoint>(true);
            if (found != null) return found;
        }
        return null;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeCanvasGroup == null) yield break;

        float start = fadeCanvasGroup.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = targetAlpha;
    }

    public int CurrentSceneIndex => currentSceneIndex;
}
