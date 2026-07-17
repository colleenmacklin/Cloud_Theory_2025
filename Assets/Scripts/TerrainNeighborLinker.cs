using UnityEngine;

/// <summary>
/// Attach to any GameObject in the scene and click "Link Neighbors" in the Inspector.
/// Finds all active Terrain components and automatically sets their neighbors based
/// on world position, so Unity stitches LOD seams at the edges.
/// </summary>
[ExecuteAlways]
public class TerrainNeighborLinker : MonoBehaviour
{
    [Tooltip("Click this in Play or Edit mode to link all terrains in the scene.")]
    public bool LinkNow = false;

    private void OnValidate()
    {
        if (!LinkNow) return;
        LinkNow = false;
        LinkAllTerrains();
    }

    [ContextMenu("Link Terrain Neighbors")]
    public void LinkAllTerrains()
    {
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);

        int linked = 0;
        foreach (Terrain t in terrains)
        {
            Terrain left   = FindNeighbor(t, terrains, -1,  0);
            Terrain right  = FindNeighbor(t, terrains,  1,  0);
            Terrain top    = FindNeighbor(t, terrains,  0,  1);
            Terrain bottom = FindNeighbor(t, terrains,  0, -1);

            t.SetNeighbors(left, top, right, bottom);
            linked++;
        }

        Debug.Log($"[TerrainNeighborLinker] Linked neighbors for {linked} terrain(s).");
    }

    private static Terrain FindNeighbor(Terrain source, Terrain[] all, int dx, int dz)
    {
        Vector3 size = source.terrainData.size;
        Vector3 expectedPos = source.transform.position
                            + new Vector3(dx * size.x, 0f, dz * size.z);

        foreach (Terrain t in all)
        {
            if (t == source) continue;
            Vector3 delta = t.transform.position - expectedPos;
            // Allow a small tolerance in case of floating-point imprecision
            if (Mathf.Abs(delta.x) < 1f && Mathf.Abs(delta.z) < 1f)
                return t;
        }
        return null;
    }
}
