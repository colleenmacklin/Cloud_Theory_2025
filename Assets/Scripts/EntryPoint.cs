using UnityEngine;

/// <summary>
/// Place one of these in each scene at the position/rotation the player
/// should have when arriving in that scene. SceneFlowController looks this
/// up by scanning the scene's root objects after it loads.
/// </summary>
public class EntryPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}
