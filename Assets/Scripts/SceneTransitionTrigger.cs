using UnityEngine;

/// <summary>
/// Attach to a trigger collider (BoxCollider with "Is Trigger" checked) placed
/// across the walkable path at a scene's exit. Two of these per boundary:
///   - one earlier ("Is Preload Trigger" checked) to start streaming the next
///     scene in the background while the player is still walking toward it
///   - one at the actual edge (unchecked) that performs the transition
///
/// Both just need to know which scene index they belong to.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Tooltip("Index of the CURRENT scene (the one the player is leaving) in SceneFlowController's sceneOrder list.")]
    [SerializeField] private int sceneIndex;

    [Tooltip("If true, this trigger only starts background loading of the next scene and does not move the player.")]
    [SerializeField] private bool isPreloadTrigger = false;

    [Tooltip("Only objects with this tag will fire the transition (default: Player).")]
    [SerializeField] private string playerTag = "Player";

    private bool fired = false;

    private void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        if (!other.CompareTag(playerTag)) return;

        fired = true;

        if (isPreloadTrigger)
        {
            SceneFlowController.Instance.BeginPreload(sceneIndex);
        }
        else
        {
            SceneFlowController.Instance.RequestTransition(sceneIndex);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isPreloadTrigger ? new Color(1f, 0.6f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        Collider col = GetComponent<Collider>();
        if (col != null) Gizmos.DrawCube(transform.position, col.bounds.size);
    }
}
