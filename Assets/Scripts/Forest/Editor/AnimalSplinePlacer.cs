using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace Forest.Editor
{
    // Editor tool: distributes a set of animals evenly along a spline's length, snaps
    // each one to the terrain height at its new XZ position, and gives each a random
    // Y rotation. Menu: Tools/Forest/Place Animals Along Spline.
    public class AnimalSplinePlacer : EditorWindow
    {
        [SerializeField] private SplineContainer spline;
        [SerializeField] private List<Transform> animals = new();
        [SerializeField] private float minYRotation = 0f;
        [SerializeField] private float maxYRotation = 360f;

        private SerializedObject _so;
        private SerializedProperty _splineProp;
        private SerializedProperty _animalsProp;
        private SerializedProperty _minYRotationProp;
        private SerializedProperty _maxYRotationProp;

        [MenuItem("Tools/Forest/Place Animals Along Spline")]
        private static void Open()
        {
            GetWindow<AnimalSplinePlacer>("Animal Spline Placer");
        }

        private void OnEnable()
        {
            _so = new SerializedObject(this);
            _splineProp = _so.FindProperty(nameof(spline));
            _animalsProp = _so.FindProperty(nameof(animals));
            _minYRotationProp = _so.FindProperty(nameof(minYRotation));
            _maxYRotationProp = _so.FindProperty(nameof(maxYRotation));
        }

        private void OnGUI()
        {
            _so.Update();

            EditorGUILayout.PropertyField(_splineProp);
            EditorGUILayout.PropertyField(_animalsProp, true);

            if (GUILayout.Button("Find All Animals In Scene"))
            {
                animals = Object.FindObjectsByType<AnimalHopController>(FindObjectsSortMode.None)
                    .Select(a => a.transform)
                    .ToList();
                // Resync the cached SerializedProperty snapshot with the field we just
                // mutated directly, otherwise ApplyModifiedProperties() below would
                // overwrite it with the stale pre-button data and discard this.
                _so.Update();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Random Y Rotation Range (degrees)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_minYRotationProp, new GUIContent("Min"));
            EditorGUILayout.PropertyField(_maxYRotationProp, new GUIContent("Max"));

            _so.ApplyModifiedProperties();

            EditorGUILayout.Space();
            var validAnimals = animals.Where(a => a != null).ToList();
            using (new EditorGUI.DisabledScope(spline == null || validAnimals.Count == 0))
            {
                if (GUILayout.Button($"Place {validAnimals.Count} Animal(s) Along Spline"))
                    PlaceAnimals(validAnimals);
            }
        }

        private void PlaceAnimals(List<Transform> validAnimals)
        {
            Spline nativeSpline = spline.Spline;
            float length = nativeSpline.GetLength();

            Undo.SetCurrentGroupName("Place Animals Along Spline");
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = 0; i < validAnimals.Count; i++)
            {
                // Fence-post spacing: first animal at the spline's start, last at its end,
                // evenly spread between. A single animal lands at the midpoint.
                float frac = validAnimals.Count > 1 ? (float)i / (validAnimals.Count - 1) : 0.5f;
                SplineUtility.GetPointAtLinearDistance(nativeSpline, 0f, frac * length, out float t);
                Vector3 worldPos = spline.EvaluatePosition(t);
                worldPos = SnapToTerrain(worldPos);

                Transform animal = validAnimals[i];
                Undo.RecordObject(animal, "Place Animal");
                animal.position = worldPos;
                animal.rotation = Quaternion.Euler(0f, Random.Range(minYRotation, maxYRotation), 0f);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // Mirrors AnimalHopController.TerrainAt / forest_player_controller.TerrainAt:
        // Terrain.activeTerrain is unreliable in this project since multiple Terrain
        // tiles (Cloud/Rave/Forest) are bundled active in the same prefab — find the
        // tile whose bounds actually cover this XZ position instead.
        private static Vector3 SnapToTerrain(Vector3 pos)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 local = pos - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x >= 0f && local.x <= size.x && local.z >= 0f && local.z <= size.z)
                {
                    pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
                    return pos;
                }
            }
            return pos;
        }
    }
}
