using UnityEngine;
using UnityEditor;

// Reduces tree density on a selected Terrain by randomly removing a percentage of tree instances.
// Usage: select the Terrain in the Hierarchy, then go to Tools → Reduce Tree Density.

public class TreeDensityReducer : EditorWindow
{
    [Range(0f, 1f)]
    private float removePercent = 0.4f;
    private int prototypeFilter = -1; // -1 = all prototypes
    private string[] prototypeNames;
    private Terrain targetTerrain;

    [MenuItem("Tools/Reduce Tree Density")]
    public static void ShowWindow()
    {
        GetWindow<TreeDensityReducer>("Tree Density Reducer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Tree Density Reducer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Auto-detect terrain from selection
        if (Selection.activeGameObject != null)
            targetTerrain = Selection.activeGameObject.GetComponent<Terrain>();

        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain", targetTerrain, typeof(Terrain), true);

        if (targetTerrain == null)
        {
            EditorGUILayout.HelpBox("Select a Terrain object in the Hierarchy or assign one above.", MessageType.Info);
            return;
        }

        // Build prototype name list for the filter dropdown
        var prototypes = targetTerrain.terrainData.treePrototypes;
        prototypeNames = new string[prototypes.Length + 1];
        prototypeNames[0] = "All tree types";
        for (int i = 0; i < prototypes.Length; i++)
            prototypeNames[i + 1] = prototypes[i].prefab != null ? prototypes[i].prefab.name : $"Prototype {i}";

        EditorGUILayout.Space();
        prototypeFilter = EditorGUILayout.Popup("Apply to", prototypeFilter + 1, prototypeNames) - 1;

        EditorGUILayout.Space();
        removePercent = EditorGUILayout.Slider("Remove %", removePercent, 0f, 1f);

        int currentCount = targetTerrain.terrainData.treeInstances.Length;
        int filtered = prototypeFilter < 0
            ? currentCount
            : System.Array.FindAll(targetTerrain.terrainData.treeInstances, t => t.prototypeIndex == prototypeFilter).Length;
        int willRemove = Mathf.RoundToInt(filtered * removePercent);
        int willRemain = filtered - willRemove;

        EditorGUILayout.HelpBox(
            $"Total trees: {currentCount}\n" +
            $"Affected trees: {filtered}\n" +
            $"Will remove: {willRemove}  |  Will remain: {willRemain}",
            MessageType.None);

        EditorGUILayout.Space();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.4f);
        if (GUILayout.Button("Remove Trees (Undoable)"))
            ReduceTrees();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This operation is undoable with Ctrl/Cmd+Z.\n" +
            "Save the scene after you're happy with the result.",
            MessageType.Info);
    }

    private void ReduceTrees()
    {
        if (targetTerrain == null) return;

        TerrainData data = targetTerrain.terrainData;
        TreeInstance[] existing = data.treeInstances;

        Undo.RegisterCompleteObjectUndo(data, "Reduce Tree Density");

        var kept = new System.Collections.Generic.List<TreeInstance>(existing.Length);
        foreach (var tree in existing)
        {
            bool affected = prototypeFilter < 0 || tree.prototypeIndex == prototypeFilter;
            if (affected && Random.value < removePercent)
                continue; // remove this one
            kept.Add(tree);
        }

        data.treeInstances = kept.ToArray();
        EditorUtility.SetDirty(data);

        Debug.Log($"[TreeDensityReducer] Removed {existing.Length - kept.Count} trees. " +
                  $"{kept.Count} remain.");
    }
}
