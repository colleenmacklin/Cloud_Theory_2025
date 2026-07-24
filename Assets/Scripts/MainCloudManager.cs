using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AzureSky;

public class MainCloudManager : MonoBehaviour
{
    //The CloudManager:
    // assigns clouds their shapes (both shaped clouds and "regular" clouds)
    // keeps track of the clouds and their positions,  making sure they are all visible to the player
    // tells the clouds when to change shape
    // keeps a history of the shapes that clouds have turned into
    public List<CloudShape> Clouds;
    public List<Texture2D> CloudShapes;
    public List<Texture2D> GenericCloudShapes;
    public List<string> allClouds;
    public float CloudStartScale;
    public CloudShape clickedCloud;
    [SerializeField]
    [Tooltip("How many of the clouds to turn into targets")]
    private int numberOfTargetsToGenerate = 1;
    [SerializeField]
    private List<string> cloudsSelectedHistory;
    [SerializeField]
    private List<string> cloudsActiveHistory;
    [SerializeField]
    private List<Texture2D> finalCloudTextures;

    [Header("Sky Time Progression")]
    [Tooltip("Azure Core System to advance when clouds are selected.")]
    public AzureCoreSystem skySystem;
    [Tooltip("Timeline hour the sky transitions to once all clouds are selected (0-24). 20 = 8pm.")]
    [Range(0f, 24f)] public float sunsetTimeline = 20f;
    [Tooltip("How many cloud selections span the full day arc. Defaults to CloudShapes.Count if left at 0.")]
    public int totalCloudsForDayArc = 0;
    [Tooltip("Duration in seconds of each sky time transition.")]
    public float transitionDuration = 5f;

    private float _startTimeline;
    private bool _climaxEntered = false;

    private Queue<Texture2D> _remainingTargets = new Queue<Texture2D>();

    public GameState gameState;
    private void OnEnable()
    {
      Actions.GetClickedCloud += GetClickedCloud;
      Actions.ConversationEnded += InactivateCloud;
      Actions.Speak += OnNarratorSpeak;
      Actions.CloudIsReady += OnCloudReady;
    }

    private void OnDisable()
    {
        Actions.GetClickedCloud -= GetClickedCloud;
        Actions.ConversationEnded -= InactivateCloud;
        Actions.Speak -= OnNarratorSpeak;
        Actions.CloudIsReady -= OnCloudReady;
    }
    void Start()
    {
        //Actions.SetRandomScale?.Invoke(CloudStartScale);

        //populate the dropdown menu for the debug scene
        //pull from the list of Shapes.names
        foreach (Texture2D shape in CloudShapes)
        {
            allClouds.Add(shape.name);
        }

        if (skySystem != null)
            _startTimeline = skySystem.timeSystem.timeline;

        setUpInitialClouds();
    }

    private void setUpInitialClouds()
    {
        Clouds.Shuffle();
        CloudShapes.Shuffle();
        GenericCloudShapes.Shuffle();

        // Spread clouds across the sky using a 2D grid in XZ, with one cloud per cell.
        // The grid aspect ratio matches the world range so cells are roughly square.
        if (Clouds.Count > 0)
        {
            float leftX  = Clouds[0].leftExitX;
            float rightX = Clouds[0].rightSpawnX;
            float zMinV  = Clouds[0].zMin;
            float zMaxV  = Clouds[0].zMax;
            float xRange = rightX - leftX;
            float zRange = zMaxV - zMinV;

            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(Clouds.Count * xRange / zRange)));
            int rows = Mathf.CeilToInt((float)Clouds.Count / cols);
            float xSlot = xRange / cols;
            float zSlot = zRange / rows;

            // Shuffle cell indices so clouds get random cells, not sequential ones.
            List<int> cells = new List<int>(cols * rows);
            for (int ci = 0; ci < cols * rows; ci++) cells.Add(ci);
            cells.Shuffle();

            for (int i = 0; i < Clouds.Count; i++)
            {
                int col = cells[i] % cols;
                int row = cells[i] / cols;
                float x = leftX + (col + 0.5f) * xSlot + Random.Range(-xSlot * 0.3f, xSlot * 0.3f);
                float z = zMinV + (row + 0.5f) * zSlot + Random.Range(-zSlot * 0.3f, zSlot * 0.3f);
                Vector3 pos = Clouds[i].transform.position;
                pos.x = x;
                pos.z = z;
                Clouds[i].transform.position = pos;
            }
        }

        // All clouds start as generic (no collider, not selectable).
        for (int i = 0; i < Clouds.Count; i++)
        {
            Clouds[i].SetGenericShape(GenericCloudShapes[i % GenericCloudShapes.Count]);
            Clouds[i].TurnOffCollider();
        }

        // Promote exactly numberOfTargetsToGenerate clouds to target shapes (with collider).
        // Clamped so it never exceeds the available clouds or available shapes.
        int targetsToAssign = Mathf.Clamp(numberOfTargetsToGenerate, 0, Mathf.Min(Clouds.Count, CloudShapes.Count));
        for (int i = 0; i < targetsToAssign; i++)
            Clouds[i].SetShape(CloudShapes[i]);

        // Queue remaining target shapes for reassignment as clouds become ready.
        _remainingTargets.Clear();
        for (int i = targetsToAssign; i < CloudShapes.Count; i++)
            _remainingTargets.Enqueue(CloudShapes[i]);
    }

    public void GetClickedCloud(GameObject c) //from Raycaster
    {
        // BoxCollider may be on a child; search the hit object then its parents.
        clickedCloud = c.GetComponent<CloudShape>() ?? c.GetComponentInParent<CloudShape>();
        if (clickedCloud == null)
        {
            Debug.LogWarning($"[MainCloudManager] GetClickedCloud: no CloudShape found on '{c.name}' or its parents.");
            return;
        }
        if (!clickedCloud.isTarget) return;
        Debug.Log(c.name + " clicked: " + clickedCloud.CurrentShapeName);
        Actions.ChooseCloud?.Invoke(clickedCloud.CurrentShapeName); // sends name to narrator

        cloudsSelectedHistory.Add(clickedCloud.CurrentShapeName);
        finalCloudTextures.Add(clickedCloud.currentShape);

        int required = totalCloudsForDayArc > 0 ? totalCloudsForDayArc : CloudShapes.Count;
        AdvanceSkyTime(cloudsSelectedHistory.Count);

        if (!_climaxEntered && cloudsSelectedHistory.Count >= required)
        {
            _climaxEntered = true;
            EnterClimaxState();
        }
    }

    [Tooltip("Duration of the all-clouds fade-out before the player stands up.")]
    public float endingFadeDuration = 5f;

    private void EnterClimaxState()
    {
        if (gameState != null) { gameState.Gameloop = false; gameState.Climax = true; }
        Actions.Climax?.Invoke();
        Debug.Log("[MainCloudManager] Climax entered — waiting for last cloud narration to finish.");

        // Reassign the first N clouds to the shapes the player identified, in order selected.
        int count = Mathf.Min(finalCloudTextures.Count, Clouds.Count);
        for (int i = 0; i < count; i++)
        {
            if (finalCloudTextures[i] != null)
                Clouds[i].SetShape(finalCloudTextures[i]);
        }

        // Do NOT start the climax narration yet — the narrator is still speaking about
        // the last cloud. InactivateCloud() will fire when that narration ends and will
        // detect _climaxEntered to kick off the closing monologue.
    }

    private void OnClimaxNarrationEnded()
    {
        Actions.ConversationEnded -= OnClimaxNarrationEnded;
        StartCoroutine(FadeAllCloudsAndStandUp());
    }

    private IEnumerator FadeAllCloudsAndStandUp()
    {
        foreach (var cloud in Clouds)
            if (cloud != null && cloud.gameObject.activeInHierarchy)
                StartCoroutine(cloud.FadeOutAndDeactivate(endingFadeDuration));

        yield return new WaitForSeconds(endingFadeDuration);
        Actions.StandUp?.Invoke();
    }

    private void AdvanceSkyTime(int cloudsSelected)
    {
        if (skySystem == null) return;

        int total = totalCloudsForDayArc > 0 ? totalCloudsForDayArc : CloudShapes.Count;
        if (total <= 0) return;

        float t = Mathf.Clamp01((float)cloudsSelected / total);
        float targetTimeline = Mathf.Lerp(_startTimeline, sunsetTimeline, t);

        skySystem.timeSystem.StartTimelineTransition(targetTimeline, transitionDuration);
    }

    private void OnNarratorSpeak()
    {
        if (clickedCloud != null)
            clickedCloud.GlowCloud(clickedCloud.gameObject);
    }

    private void OnCloudReady(CloudShape cloud)
    {
        if (_remainingTargets.Count > 0)
            cloud.SetShape(_remainingTargets.Dequeue());
        else
            cloud.SetGenericShape(GenericCloudShapes[Random.Range(0, GenericCloudShapes.Count)]);
    }

    private void InactivateCloud()
    {
        Actions.InactivateCloud?.Invoke(clickedCloud);

        if (_climaxEntered)
        {
            // The last cloud narration just finished. Now start the climax monologue.
            // Swap our ConversationEnded handler so the climax narration ending
            // triggers the cloud fade-out and stand-up instead of another inactivation.
            Actions.ConversationEnded -= InactivateCloud;
            Actions.ConversationEnded += OnClimaxNarrationEnded;
            Debug.Log("[MainCloudManager] Starting climax narration.");
            Actions.StartClimaxNarration?.Invoke();
        }
    }

}
