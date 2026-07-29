using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AzureSky;
using FMODUnity;

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

    [Header("Sky Dome Layout")]
    [Tooltip("The player's fixed viewing position (e.g. the lying-down camera or body). Cloud placement angles are measured from this point, so clouds read as evenly spaced in the player's field of view rather than in flat world XZ.")]
    public Transform playerViewpoint;
    [Tooltip("Drag four empty GameObjects to mark the top, bottom, left, and right edges of the sky patch that's clear of trees, as seen from playerViewpoint. Position them visually in the Scene view — clouds are placed within the region these four points define, so this replaces tuning elevation/azimuth by hand.")]
    public Transform topMarker;
    public Transform bottomMarker;
    public Transform leftMarker;
    public Transform rightMarker;
    [Tooltip("How much of a cloud's own visual radius its neighbors are allowed to overlap into. 0 = clouds may only just touch (no overlap); 1 = clouds may fully overlap. Required separation is computed from each cloud's actual size (CloudShape.EstimateVisualRadius), not a flat angle, since a fixed angle means nothing across clouds of very different apparent size.")]
    [Range(0f, 1f)] public float allowedOverlapFraction = 0.35f;
    [Tooltip("How many candidate positions to try before settling for the least-overlapping one found.")]
    public int placementAttempts = 30;
    [Tooltip("Fraction of the total azimuth range, measured in from rightMarker's edge, that respawning clouds are placed within — mimicking a conveyor belt where clouds drift left and exit, then re-enter from the right. Small values (e.g. 0.25) keep respawns close to the right edge; 1 allows anywhere in the region.")]
    [Range(0.05f, 1f)] public float respawnAzimuthZoneFraction = 0.3f;

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
    private bool _cricketsCued = false;

    [Header("Ambient Sound")]
    [Tooltip("FMOD event for the nighttime cricket ambiance.")]
    public EventReference cricketsEvent;
    [Tooltip("How far through the day arc (0–1) before the birds cross-fade to crickets. 0.5 = halfway.")]
    [Range(0f, 1f)] public float cricketsCrossoverPoint = 0.5f;

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

        // Place clouds by sampling elevation/azimuth angles around the player's fixed
        // viewpoint rather than a flat XZ grid. A flat grid looks uneven from a fixed
        // viewpoint below it — cells near the zenith cover a wide angular swing while
        // cells near the horizon barely change angle at all.
        bool canDomePlace = playerViewpoint != null && topMarker != null && bottomMarker != null
                            && leftMarker != null && rightMarker != null;
        if (Clouds.Count > 0 && canDomePlace)
        {
            // Grid the region in BOTH elevation and azimuth (not just elevation), and give
            // each cloud its own shuffled cell. Elevation-only banding guarantees even
            // top-to-bottom coverage, but leaves azimuth to plain random sampling — with
            // only a handful of clouds, that can easily leave the far left/right edges (or
            // any specific direction) sparse or empty purely by chance. Gridding both axes
            // fixes that the same way the original flat-grid system did, just correctly in
            // angle-space instead of world XZ.
            GetSkyRegionBounds(out float minElevation, out float maxElevation, out float azimuthCenter, out float azimuthRange);
            ComputeGridDimensions(out int rows, out int cols);

            List<int> cellOrder = new List<int>(rows * cols);
            for (int c = 0; c < rows * cols; c++) cellOrder.Add(c);
            cellOrder.Shuffle();

            Debug.Log($"[MainCloudManager] Sky region: elevation {minElevation:F1}° to {maxElevation:F1}°, azimuth {azimuthCenter:F1}° ±{azimuthRange * 0.5f:F1}°, grid {rows}x{cols}");

            var placed = new List<(Vector3 pos, float radius)>(Clouds.Count);
            for (int i = 0; i < Clouds.Count; i++)
            {
                int cell = cellOrder[i];
                Clouds[i].elevationBand = cell / cols;
                Clouds[i].azimuthBand = cell % cols;

                Vector3 chosen = PickDomePositionInCell(Clouds[i], rows, cols, placed);
                Clouds[i].transform.position = chosen;
                placed.Add((chosen, Clouds[i].EstimateVisualRadius()));

                Debug.Log($"[MainCloudManager] {Clouds[i].name}: cell=({Clouds[i].elevationBand},{Clouds[i].azimuthBand}) elevation={ElevationOfPosition(chosen):F1}°");
            }
        }
        else if (Clouds.Count > 0)
        {
            Debug.LogWarning("[MainCloudManager] playerViewpoint and/or the four sky-region markers (topMarker/bottomMarker/leftMarker/rightMarker) are not fully assigned — clouds keep their authored positions instead of being dome-placed.");
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

        // Queue the remaining shapes up to totalCloudsForDayArc.
        // These are cycled in one-at-a-time as each target cloud is narrated about,
        // so there are always exactly numberOfTargetsToGenerate targets in the sky.
        int required = totalCloudsForDayArc > 0 ? totalCloudsForDayArc : CloudShapes.Count;
        _remainingTargets.Clear();
        for (int i = targetsToAssign; i < Mathf.Min(required, CloudShapes.Count); i++)
            _remainingTargets.Enqueue(CloudShapes[i]);
    }

    // Sizes an (elevation x azimuth) grid to roughly Clouds.Count cells. Deliberately
    // balanced against cloud count alone rather than matched to the region's raw
    // degree-aspect-ratio: azimuth range is typically much wider in degrees than elevation
    // range, and matching that ratio directly (as the original flat-XZ grid did in world
    // space) starves elevation down to very few rows — e.g. 2 rows for 13 clouds — leaving
    // large gaps near the top/bottom regardless of how well azimuth is covered.
    private void ComputeGridDimensions(out int rows, out int cols)
    {
        cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Clouds.Count)));
        rows = Mathf.CeilToInt((float)Clouds.Count / cols);
    }

    // Picks an elevation/azimuth pair within this cloud's assigned grid cell (see
    // CloudShape.elevationBand/azimuthBand), rejection-sampled against `others` for
    // enough angular clearance (see MinAngularClearanceDeg) to avoid a major overlap.
    // Falls back to the least-overlapping candidate found if no fully-clear position
    // turns up within placementAttempts. Confining each cloud to its own cell (rather
    // than letting all clouds compete for the full region) is what guarantees even
    // coverage in both directions — see the comment in setUpInitialClouds.
    private Vector3 PickDomePositionInCell(CloudShape cloud, int rows, int cols, List<(Vector3 pos, float radius)> others)
    {
        GetSkyRegionBounds(out float minElevation, out float maxElevation, out float azimuthCenter, out float azimuthRange);

        float rowHeight = (maxElevation - minElevation) / rows;
        int row = Mathf.Clamp(cloud.elevationBand, 0, rows - 1);
        float bandMinElev = minElevation + row * rowHeight;
        float bandMaxElev = bandMinElev + rowHeight;

        float colWidth = azimuthRange / cols;
        int col = Mathf.Clamp(cloud.azimuthBand, 0, cols - 1);
        float azimuthMinEdge = azimuthCenter - azimuthRange * 0.5f;
        float bandMinAz = azimuthMinEdge + col * colWidth;
        float bandMaxAz = bandMinAz + colWidth;

        float candidateRadius = cloud.EstimateVisualRadius();

        Vector3 best = playerViewpoint.position + Vector3.forward; // safe fallback, overwritten below
        float bestClearance = float.MinValue;

        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            float elevation = Random.Range(bandMinElev, bandMaxElev);
            float azimuth = Random.Range(bandMinAz, bandMaxAz);
            Vector3 candidate = DomeToWorldPosition(elevation, azimuth, cloud.transform.position.y);

            float clearance = MinAngularClearanceDeg(candidate, candidateRadius, others);
            if (clearance >= 0f) return candidate;
            if (clearance > bestClearance) { bestClearance = clearance; best = candidate; }
        }

        return best;
    }

    // Derives the elevation/azimuth region clouds may occupy from the four marker
    // transforms, as seen from playerViewpoint. Azimuth uses Mathf.DeltaAngle so the
    // left/right span is always measured the short way around, regardless of which
    // side of the 180°/-180° wraparound the markers happen to sit on.
    private void GetSkyRegionBounds(out float minElevation, out float maxElevation, out float azimuthCenter, out float azimuthRange)
    {
        minElevation = ElevationOf(bottomMarker);
        maxElevation = ElevationOf(topMarker);

        float azLeft = AzimuthOf(leftMarker);
        float azRight = AzimuthOf(rightMarker);
        float delta = Mathf.DeltaAngle(azLeft, azRight);
        azimuthCenter = azLeft + delta * 0.5f;
        azimuthRange = Mathf.Abs(delta);
    }

    // Elevation angle (degrees above horizon) of a marker as seen from playerViewpoint.
    private float ElevationOf(Transform marker) => ElevationOfPosition(marker.position);

    // Elevation angle (degrees) of a world position as seen from playerViewpoint,
    // for a LYING-DOWN player whose camera points up (+Y).
    // X = screen left/right, Z = screen up/down, Y = altitude (depth into sky).
    // "Elevation" here = screen-vertical angle: how far above/below screen centre.
    private float ElevationOfPosition(Vector3 worldPos)
    {
        Vector3 offset = worldPos - playerViewpoint.position;
        // Y is altitude (camera forward). XY-plane distance is the "horizontal" reference.
        float horizontalDist = new Vector2(offset.x, offset.y).magnitude;
        // Z deviation from the XY plane = screen up/down angle.
        return Mathf.Atan2(offset.z, horizontalDist) * Mathf.Rad2Deg;
    }

    // Screen-horizontal angle (degrees) of a marker as seen from playerViewpoint.
    // X = screen left/right, Y = altitude depth → azimuth = angle from +Y toward ±X.
    private float AzimuthOf(Transform marker)
    {
        Vector3 offset = marker.position - playerViewpoint.position;
        return Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
    }

    // Converts an elevation/azimuth pair, as seen from playerViewpoint, into a world
    // position at the given Y altitude. Coordinate convention: X = screen left/right,
    // Z = screen up/down, Y = altitude (camera looks up along +Y).
    private Vector3 DomeToWorldPosition(float elevationDeg, float azimuthDeg, float worldY)
    {
        // Clamp to avoid tan() singularity; allow negative elevation for screen-bottom clouds.
        float elevationRad = Mathf.Clamp(elevationDeg, -88f, 88f) * Mathf.Deg2Rad;
        float azimuthRad   = Mathf.Clamp(azimuthDeg,   -88f, 88f) * Mathf.Deg2Rad;

        // Y-depth: how far above the player the cloud sits.
        float yOffset = worldY - playerViewpoint.position.y;

        // Project angles onto the XZ plane at altitude worldY.
        // tan(azimuth)  = X / Y  →  X = Y * tan(azimuth)
        // tan(elevation) = Z / Y  →  Z = Y * tan(elevation)
        float xOffset = yOffset * Mathf.Tan(azimuthRad);
        float zOffset = yOffset * Mathf.Tan(elevationRad);

        return new Vector3(
            playerViewpoint.position.x + xOffset,
            worldY,
            playerViewpoint.position.z + zOffset
        );
    }

    // Angular radius (degrees), as seen from playerViewpoint, that a cloud of the given
    // world-unit radius at the given world position appears to subtend.
    private float AngularRadiusDeg(Vector3 worldPos, float radius)
    {
        float distance = Vector3.Distance(worldPos, playerViewpoint.position);
        if (distance < 0.01f) return 90f;
        return Mathf.Atan2(radius, distance) * Mathf.Rad2Deg;
    }

    // How much angular room (degrees) is left between a candidate cloud and its closest
    // neighbor in `others`, after each cloud's own angular radius is accounted for.
    // Positive means clear of a "major" overlap (by allowedOverlapFraction); negative
    // means the two clouds are overlapping into each other's visual extent. Returns a
    // large positive value if there's nothing to compare against.
    private float MinAngularClearanceDeg(Vector3 candidateWorldPos, float candidateRadius, List<(Vector3 pos, float radius)> others)
    {
        if (others.Count == 0) return float.MaxValue;

        float minClearance = float.MaxValue;
        Vector3 dirA = (candidateWorldPos - playerViewpoint.position).normalized;
        float candidateAngularRadius = AngularRadiusDeg(candidateWorldPos, candidateRadius);

        foreach (var (pos, radius) in others)
        {
            Vector3 dirB = (pos - playerViewpoint.position).normalized;
            float gap = Vector3.Angle(dirA, dirB);
            float otherAngularRadius = AngularRadiusDeg(pos, radius);
            float requiredGap = (candidateAngularRadius + otherAngularRadius) * (1f - allowedOverlapFraction);

            float clearance = gap - requiredGap;
            if (clearance < minClearance) minClearance = clearance;
        }
        return minClearance;
    }

    // Called by CloudShape when respawning after a wind-wrap. Wind only ever drags
    // clouds toward -X (left) regardless of a cloud's assigned azimuthBand, so a cloud
    // whose band happens to sit on the left of the region would keep reappearing on the
    // left forever if respawn reused PickDomePositionInCell (its original band) — leaving
    // the right side empty and clumping clouds back near where they'd just disappeared.
    // Respawn instead reappears near rightMarker's edge, like a scrolling conveyor belt.
    // Elevation still uses the cloud's original elevationBand row, so vertical
    // stratification holds over time; only azimuth is re-picked. Falls back to the
    // cloud's current position if the sky region isn't fully configured or it was never
    // assigned a band.
    public Vector3 PickDomeRespawnPosition(CloudShape self)
    {
        if (playerViewpoint == null || topMarker == null || bottomMarker == null
            || leftMarker == null || rightMarker == null || self.elevationBand < 0)
            return self.transform.position;

        ComputeGridDimensions(out int rows, out _);

        var others = new List<(Vector3 pos, float radius)>(Clouds.Count);
        foreach (var cloud in Clouds)
            if (cloud != null && cloud != self)
                others.Add((cloud.transform.position, cloud.EstimateVisualRadius()));

        return PickRightEdgeRespawnPosition(self, rows, others);
    }

    // Picks an elevation/azimuth pair for a respawning cloud: elevation confined to the
    // cloud's own elevationBand row (as at initial layout), azimuth confined to the
    // rightmost respawnAzimuthZoneFraction slice of the region (measured in from
    // rightMarker's actual azimuth — not azimuthCenter + azimuthRange/2, which edge that
    // is depends on which side of the wraparound the markers happen to sit on).
    // Rejection-sampled against `others` for angular clearance, same as initial layout.
    private Vector3 PickRightEdgeRespawnPosition(CloudShape cloud, int rows, List<(Vector3 pos, float radius)> others)
    {
        GetSkyRegionBounds(out float minElevation, out float maxElevation, out float azimuthCenter, out float azimuthRange);

        float rowHeight = (maxElevation - minElevation) / rows;
        int row = Mathf.Clamp(cloud.elevationBand, 0, rows - 1);
        float bandMinElev = minElevation + row * rowHeight;
        float bandMaxElev = bandMinElev + rowHeight;

        float azRightEdge = AzimuthOf(rightMarker);
        float inward = Mathf.Sign(Mathf.DeltaAngle(azRightEdge, azimuthCenter));
        float zoneWidth = azimuthRange * respawnAzimuthZoneFraction;
        float azimuthA = azRightEdge;
        float azimuthB = azRightEdge + inward * zoneWidth;
        float bandMinAz = Mathf.Min(azimuthA, azimuthB);
        float bandMaxAz = Mathf.Max(azimuthA, azimuthB);

        float candidateRadius = cloud.EstimateVisualRadius();

        Vector3 best = playerViewpoint.position + Vector3.forward;
        float bestClearance = float.MinValue;

        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            float elevation = Random.Range(bandMinElev, bandMaxElev);
            float azimuth = Random.Range(bandMinAz, bandMaxAz);
            Vector3 candidate = DomeToWorldPosition(elevation, azimuth, cloud.transform.position.y);

            float clearance = MinAngularClearanceDeg(candidate, candidateRadius, others);
            if (clearance >= 0f) return candidate;
            if (clearance > bestClearance) { bestClearance = clearance; best = candidate; }
        }

        return best;
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

        if (!_cricketsCued && t >= cricketsCrossoverPoint && !cricketsEvent.IsNull && AudioManager.instance != null)
        {
            _cricketsCued = true;
            AudioManager.instance.swapTrack(cricketsEvent);
        }
    }

    private void OnNarratorSpeak()
    {
        if (clickedCloud != null)
            clickedCloud.GlowCloud(clickedCloud.gameObject);
    }

    private void OnCloudReady(CloudShape cloud)
    {
        // Only give a generic shape on wind-cycle respawn; never overwrite an active target.
        if (!cloud.isTarget)
            cloud.SetGenericShape(GenericCloudShapes[Random.Range(0, GenericCloudShapes.Count)]);
    }

    private void PromoteNextTarget()
    {
        if (_remainingTargets.Count == 0) return;

        // Pick a random cloud that is not currently a target and is not the one
        // that was just used — so the new shape always appears somewhere fresh.
        var candidates = Clouds.FindAll(c => !c.isTarget && c != clickedCloud);
        if (candidates.Count == 0) return;

        CloudShape next = candidates[Random.Range(0, candidates.Count)];
        next.SetShape(_remainingTargets.Dequeue());
    }

    private void InactivateCloud()
    {
        Actions.InactivateCloud?.Invoke(clickedCloud);

        // Immediately promote a different generic cloud to the next target shape,
        // keeping exactly numberOfTargetsToGenerate targets visible at all times.
        if (!_climaxEntered)
            PromoteNextTarget();

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

    // Draws a ray from playerViewpoint to each assigned marker, so the sky region can be
    // positioned by eye in the Scene view against the tree canopy.
    private void OnDrawGizmosSelected()
    {
        if (playerViewpoint == null) return;

        Gizmos.color = Color.cyan;
        if (topMarker != null) Gizmos.DrawLine(playerViewpoint.position, topMarker.position);
        if (bottomMarker != null) Gizmos.DrawLine(playerViewpoint.position, bottomMarker.position);
        if (leftMarker != null) Gizmos.DrawLine(playerViewpoint.position, leftMarker.position);
        if (rightMarker != null) Gizmos.DrawLine(playerViewpoint.position, rightMarker.position);
    }
}
