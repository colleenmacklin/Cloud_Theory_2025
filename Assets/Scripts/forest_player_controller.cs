using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5.0f;
    [Tooltip("Mouse sensitivity. The new Input System reports pixel deltas, so this is much smaller than the old Input.GetAxis equivalent — start around 0.1.")]
    public float lookSensitivity = 0.1f;
    public float gravity = -9.81f;

    private CharacterController _cc;
    private Transform _cam;
    private float _verticalRot;
    private float _verticalVel;

    void Start()
    {
        _cc  = GetComponent<CharacterController>();
        _cam = GetComponentInChildren<Camera>().transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        Vector2 delta = Mouse.current.delta.ReadValue();
        float mouseX  = delta.x * lookSensitivity;
        float mouseY  = delta.y * lookSensitivity;

        transform.Rotate(Vector3.up * mouseX);
        _verticalRot = Mathf.Clamp(_verticalRot - mouseY, -90f, 90f);
        _cam.localRotation = Quaternion.Euler(_verticalRot, 0f, 0f);
    }

    private void HandleMovement()
    {
        Keyboard kb = Keyboard.current;
        float v = (kb.wKey.isPressed || kb.upArrowKey.isPressed    ? 1f : 0f)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed   ? 1f : 0f);
        float h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed  ? 1f : 0f)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed    ? 1f : 0f);

        Vector3 move = transform.forward * v + transform.right * h;
        move = Vector3.ClampMagnitude(move, 1f) * moveSpeed;

        if (SnapToTerrain())
        {
            // Y is handled by the terrain snap — no vertical velocity needed.
            _verticalVel = 0f;
        }
        else
        {
            // No terrain below (gap, edge, etc.) — apply gravity normally.
            _verticalVel = _cc.isGrounded ? -2f : _verticalVel + gravity * Time.deltaTime;
        }

        _cc.Move((move + Vector3.up * _verticalVel) * Time.deltaTime);
    }

    // Reads the Unity Terrain height at the player's XZ position and
    // repositions the CharacterController directly onto the surface.
    // Returns true if the player is on (or very close to) the terrain.
    private bool SnapToTerrain()
    {
        Terrain terrain = TerrainAt(transform.position);
        if (terrain == null) return false;

        float groundY  = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
        float targetY  = groundY + _cc.height * 0.5f;

        // Don't snap if we're more than 0.5 units above the surface — let gravity handle the fall.
        if (transform.position.y > targetY + 0.5f) return false;

        Vector3 pos = transform.position;
        pos.y = targetY;
        transform.position = pos;
        return true;
    }

    // Terrain.activeTerrain is unreliable when multiple Terrain tiles are active at once
    // (this project bundles Cloud/Rave/Forest tiles in one shared prefab, all active
    // together in the Forest scene). Picking a tile by bounds alone isn't enough either —
    // these tiles are only 1000 units apart but each is wider than that, so their bounds
    // genuinely overlap, and a bounds check can match more than one tile for the same XZ
    // position, silently sampling CloudTerrain's or RaveTerrain's height instead of the
    // ground actually under the player. This controller only ever runs in the Forest
    // scene, so go straight for the tile named "ForestTerrain" instead of guessing; fall
    // back to the old bounds heuristic only if that tile can't be found (e.g. renamed).
    private static Terrain _forestTerrain;

    private static Terrain TerrainAt(Vector3 pos)
    {
        if (_forestTerrain == null)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                if (t.name == "ForestTerrain")
                {
                    _forestTerrain = t;
                    break;
                }
            }
        }
        if (_forestTerrain != null) return _forestTerrain;

        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 local = pos - t.transform.position;
            Vector3 size = t.terrainData.size;
            if (local.x >= 0f && local.x <= size.x && local.z >= 0f && local.z <= size.z)
                return t;
        }
        return Terrain.activeTerrain;
    }
}
