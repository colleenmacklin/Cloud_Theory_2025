using UnityEditor;
using UnityEngine;

/// <summary>
/// Bulk-remaps every embedded material inside all FBX/model files in a folder
/// (and its subfolders) to a single target material.
///
/// Why this exists: Unity's multi-select "Materials" remap tab in the Inspector
/// only lets you remap materials one imported model at a time, or asks you to
/// remap each identically-named material separately per file. This script walks
/// every model asset in a folder, finds every material sub-asset embedded in it,
/// and points it at your chosen material in one pass.
///
/// Setup:
///   1. Put this file in a folder named "Editor" anywhere under Assets
///      (e.g. Assets/Editor/BulkMaterialRemapWindow.cs). Unity requires editor
///      scripts to live in an "Editor" folder.
///   2. In Unity: Tools > Bulk Remap FBX Materials.
///   3. Set "Folder" to the folder containing your imported animal FBX files
///      (e.g. Assets/exported_fbx).
///   4. Drag your VertexColorURP material into "Target Material".
///   5. Click "Remap All FBX Materials In Folder".
///
/// This remaps ALL embedded materials it finds (Mat_Standard, Mat_Swim, etc.)
/// to the same target material. If you want to keep some materials distinct
/// (e.g. leave Mat_Transp alone), use the "Skip names containing" field to
/// exclude them by substring.
/// </summary>
public class BulkMaterialRemapWindow : EditorWindow
{
    private string folderPath = "Assets";
    private Material targetMaterial;
    private string skipSubstring = "";

    [MenuItem("Tools/Bulk Remap FBX Materials")]
    public static void ShowWindow()
    {
        GetWindow<BulkMaterialRemapWindow>("Bulk Material Remap");
    }

    private void OnGUI()
    {
        GUILayout.Label("Remap embedded FBX materials to a single material", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Folder", GUILayout.Width(80));
        folderPath = EditorGUILayout.TextField(folderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string picked = EditorUtility.OpenFolderPanel("Choose folder", "Assets", "");
            if (!string.IsNullOrEmpty(picked))
            {
                if (picked.StartsWith(Application.dataPath))
                {
                    folderPath = "Assets" + picked.Substring(Application.dataPath.Length);
                }
                else
                {
                    EditorUtility.DisplayDialog("Invalid folder",
                        "Please choose a folder inside this project's Assets folder.", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        targetMaterial = (Material)EditorGUILayout.ObjectField(
            "Target Material", targetMaterial, typeof(Material), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Skip materials whose name contains (optional):");
        skipSubstring = EditorGUILayout.TextField(skipSubstring);

        EditorGUILayout.Space();

        if (GUILayout.Button("Remap All FBX Materials In Folder"))
        {
            if (targetMaterial == null)
            {
                EditorUtility.DisplayDialog("Missing material",
                    "Please assign a target material first.", "OK");
                return;
            }
            RemapAll();
        }
    }

    private void RemapAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
        int modelsTouched = 0;
        int remapsMade = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            if (guids.Length > 0)
            {
                EditorUtility.DisplayProgressBar("Remapping materials", path, (float)i / guids.Length);
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            bool changedThisModel = false;

            foreach (Object asset in subAssets)
            {
                if (asset is Material mat)
                {
                    if (!string.IsNullOrEmpty(skipSubstring) &&
                        mat.name.IndexOf(skipSubstring, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    var identifier = new AssetImporter.SourceAssetIdentifier(mat);
                    importer.AddRemap(identifier, targetMaterial);
                    changedThisModel = true;
                    remapsMade++;
                }
            }

            if (changedThisModel)
            {
                importer.SaveAndReimport();
                modelsTouched++;
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log($"Bulk Material Remap complete: {remapsMade} material reference(s) remapped across {modelsTouched} model file(s).");
    }
}
