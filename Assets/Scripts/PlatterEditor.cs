using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse-based interaction for placing and removing PlatterObjects on a PlatterLayout.
///
/// Controls:
///   Hover platter     — highlights nearest slot; shows a ghost preview of the selected config
///   Scroll wheel      — cycle through the platter's palette (ghost updates immediately)
///   Left click        — place selected config at hovered slot
///   Right click       — remove object at hovered slot
/// </summary>
public class PlatterEditor : MonoBehaviour
{
    [SerializeField] private Camera    playerCamera;
    [SerializeField] private LayerMask platterLayer;

    [Header("Ghost Preview")]
    [Tooltip("Optional: assign a semi-transparent URP material for the placement ghost. " +
             "If left empty a default cyan ghost material is created at runtime.")]
    [SerializeField] private Material ghostMaterial;

    [Header("UI (optional)")]
    [Tooltip("Assign a TMPro label to show the selected config name while hovering.")]
    [SerializeField] private TMPro.TextMeshProUGUI paletteLabel;

    private PlatterLayout _hovered;
    private int           _paletteIndex;

    // ghost state
    private GameObject    _ghost;
    private PlatterConfig _ghostConfig;
    private int           _ghostRing = -1;
    private Material      _ghostMat;

    private void Update()
    {
        UpdateHover();
        HandleInput();
    }

    private void OnDestroy() => DestroyGhost();

    // ── Hover ─────────────────────────────────────────────────────────────

    private void UpdateHover()
    {
        Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, platterLayer))
        {
            ClearHover();
            return;
        }

        var layout = hit.collider.GetComponentInParent<PlatterLayout>();
        if (layout == null)
        {
            ClearHover();
            return;
        }

        if (layout != _hovered)
        {
            _hovered?.ClearHover();
            DestroyGhost();
            _hovered = layout;
            _paletteIndex = Mathf.Clamp(_paletteIndex, 0, Mathf.Max(0, layout.palette.Count - 1));
            UpdateLabel();
        }

        if (!layout.WorldPointToRingAndSlot(hit.point, out int ring, out int slot)) return;

        layout.SetHoveredRingSlot(ring, slot);
        UpdateGhost(layout, ring, slot);
    }

    private void ClearHover()
    {
        _hovered?.ClearHover();
        _hovered = null;
        DestroyGhost();
    }

    // ── Input ─────────────────────────────────────────────────────────────

    private void HandleInput()
    {
        if (_hovered == null || _hovered.palette.Count == 0) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll != 0f)
        {
            int dir = scroll > 0f ? 1 : -1;
            _paletteIndex = (_paletteIndex + dir + _hovered.palette.Count) % _hovered.palette.Count;
            UpdateLabel();
            // rebuild ghost for new config
            DestroyGhost();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && GetHoveredRingSlot(out int pr, out int ps))
            _hovered.Place(pr, ps, _hovered.palette[_paletteIndex]);

        if (Mouse.current.rightButton.wasPressedThisFrame && GetHoveredRingSlot(out int rr, out int rs))
            _hovered.Remove(rr, rs);
    }

    private bool GetHoveredRingSlot(out int ring, out int slot)
    {
        ring = -1; slot = -1;
        if (_hovered == null) return false;
        Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, platterLayer)) return false;
        return _hovered.WorldPointToRingAndSlot(hit.point, out ring, out slot);
    }

    // ── Ghost preview ─────────────────────────────────────────────────────

    private void UpdateGhost(PlatterLayout layout, int ring, int slot)
    {
        if (_paletteIndex >= layout.palette.Count) return;
        var config = layout.palette[_paletteIndex];
        var prefab = layout.rings[ring].platterObjectPrefab;

        // rebuild when the config or ring changes
        if (_ghost == null || config != _ghostConfig || ring != _ghostRing)
        {
            DestroyGhost();
            if (config == null || prefab == null) return;

            _ghost = Instantiate(prefab, layout.transform);
            _ghost.name = "PlacementGhost";

            // apply mesh + auto-scale via Apply() so the ghost matches the placed object exactly
            var ghostPO = _ghost.GetComponent<PlatterObject>();
            if (ghostPO != null) { ghostPO.config = config; ghostPO.Apply(); }

            // ghost should be purely visual — disable colliders and PlatterObject immediately
            // (Destroy is deferred and can cause ghost to trigger StylusDetector before end of frame)
            foreach (var c in _ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var p in _ghost.GetComponentsInChildren<PlatterObject>()) p.enabled = false;

            // apply semi-transparent material to every renderer
            Material mat = GetGhostMaterial();
            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;

            _ghostConfig = config;
            _ghostRing   = ring;
        }

        // snap to slot position (local space — moves with spinning disc)
        float slotAngle = 360f / layout.rings[ring].slotCount;

        _ghost.transform.SetLocalPositionAndRotation(
            layout.SlotLocalPosition(ring, slot),
            Quaternion.Euler(0f, slot * slotAngle, 0f));
    }

    private void DestroyGhost()
    {
        if (_ghost != null) Destroy(_ghost);
        _ghost       = null;
        _ghostConfig = null;
        _ghostRing   = -1;
    }

    private Material GetGhostMaterial()
    {
        // use the assigned material if provided
        if (ghostMaterial != null) return ghostMaterial;

        // otherwise create a default cyan semi-transparent material once
        if (_ghostMat != null) return _ghostMat;

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");
        _ghostMat = new Material(shader)
        {
            color = new Color(0.4f, 0.9f, 1f, 0.35f)
        };
        // URP transparency keywords
        _ghostMat.SetFloat("_Surface", 1f);
        _ghostMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _ghostMat.renderQueue = 3000;
        return _ghostMat;
    }

    // ── Label ─────────────────────────────────────────────────────────────

    private void UpdateLabel()
    {
        if (paletteLabel == null || _hovered == null || _hovered.palette.Count == 0) return;
        var config = _hovered.palette[_paletteIndex];
        paletteLabel.text = config != null ? config.name : "—";
    }
}
