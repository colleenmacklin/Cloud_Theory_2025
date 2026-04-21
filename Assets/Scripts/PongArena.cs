using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    /// <summary>
    /// Manages the Pong arena.
    ///
    /// Setup required in the scene:
    ///   - Two thin box colliders tagged "PongWall" for top/bottom boundaries.
    ///   - Two trigger colliders tagged "PongGoal" for left/right exit zones —
    ///     these can be invisible box triggers just beyond the paddle positions.
    ///   - All paddles and walls should use "PongPaddle" and "PongWall" tags
    ///     so PongBall knows which contacts to respond to sonically.
    ///
    /// This component also provides a global kill-all (Backspace) and tracks
    /// all live balls so they can be cleared if needed.
    /// </summary>
    public class PongArena : MonoBehaviour
    {
        [Header("Arena Bounds (auto-creates walls/goals if null)")]
        [Tooltip("Half-width of the arena (X axis)")]
        [SerializeField] private float arenaHalfWidth  = 8f;
        [Tooltip("Half-height of the arena (Y axis)")]
        [SerializeField] private float arenaHalfHeight = 5f;
        [Tooltip("Thickness of wall colliders")]
        [SerializeField] private float wallThickness   = 0.3f;

        [Header("Auto-Build")]
        [Tooltip("If true, walls and goal triggers are created at runtime. "
               + "Disable if you prefer to build them manually in the scene.")]
        [SerializeField] private bool autoBuildArena = true;

        // ── Live ball registry ────────────────────────────────────────────────
        private readonly List<PongBall> _liveBalls = new();

        public void RegisterBall(PongBall ball)   => _liveBalls.Add(ball);
        public void UnregisterBall(PongBall ball) => _liveBalls.Remove(ball);

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (autoBuildArena)
                BuildArena();
        }

        private void Update()
        {
            // Backspace = kill all balls
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                KillAllBalls();
            }
        }

        // ── Kill all ──────────────────────────────────────────────────────────

        public void KillAllBalls()
        {
            // iterate a copy since Kill() will modify the list via OnDestroy
            var copy = new List<PongBall>(_liveBalls);
            foreach (var b in copy)
                if (b != null) b.Kill();
            _liveBalls.Clear();
        }

        // ── Arena construction ────────────────────────────────────────────────

        private void BuildArena()
        {
            // Z depth large enough to always catch the ball regardless of Z drift
            float zDepth = 4f;

            // Top wall
            CreateWall("WallTop",
                new Vector3(0f, arenaHalfHeight + wallThickness * 0.5f, 0f),
                new Vector3(arenaHalfWidth * 2f, wallThickness, zDepth));

            // Bottom wall
            CreateWall("WallBottom",
                new Vector3(0f, -arenaHalfHeight - wallThickness * 0.5f, 0f),
                new Vector3(arenaHalfWidth * 2f, wallThickness, zDepth));

            // Left goal trigger
            CreateGoal("GoalLeft",
                new Vector3(-arenaHalfWidth - wallThickness, 0f, 0f),
                new Vector3(wallThickness * 2f, arenaHalfHeight * 2f, zDepth));

            // Right goal trigger
            CreateGoal("GoalRight",
                new Vector3(arenaHalfWidth + wallThickness, 0f, 0f),
                new Vector3(wallThickness * 2f, arenaHalfHeight * 2f, zDepth));
        }

        private void CreateWall(string objName, Vector3 position, Vector3 size)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.tag = "PongWall";

            var col = go.AddComponent<BoxCollider>();
            col.size = size;

            // visual (thin flat quad)
            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = CreateQuadMesh(size.x, size.y);
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = CreateArenaMaterial(new Color(0.8f, 0.8f, 0.8f));
        }

        private void CreateGoal(string objName, Vector3 position, Vector3 size)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.tag = "PongGoal";

            var col    = go.AddComponent<BoxCollider>();
            col.size   = size;
            col.isTrigger = true;
        }

        // ── Mesh / material helpers ───────────────────────────────────────────

        private static Mesh CreateQuadMesh(float width, float height)
        {
            float hw = width  * 0.5f;
            float hh = height * 0.5f;
            var mesh = new Mesh();
            mesh.vertices  = new Vector3[] {
                new(-hw, -hh, 0), new(hw, -hh, 0),
                new(-hw,  hh, 0), new(hw,  hh, 0) };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Material CreateArenaMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                   ?? Shader.Find("Standard"));
            mat.color = color;
            return mat;
        }
    }
}
