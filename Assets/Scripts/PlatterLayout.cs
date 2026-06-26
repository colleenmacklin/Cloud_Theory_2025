using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlatterRing
{
    [Tooltip("Distance from the platter centre.")]
    public float radius = 1f;

    [Tooltip("Number of evenly-spaced slots in this ring.")]
    [Min(1)]
    public int slotCount = 16;

    [Tooltip("Prefab placed at each slot. Needs PlatterObject + Collider.")]
    public GameObject platterObjectPrefab;
}

/// <summary>
/// Manages one or more rings of slots around a spinning platter disc.
/// Attach to the same GameObject as PlatterSpinner (the part that rotates).
/// </summary>
public class PlatterLayout : MonoBehaviour
{
    [Header("Rings")]
    [Tooltip("Add one entry per ring. Each ring has its own radius, slot count, and prefab.")]
    public List<PlatterRing> rings = new();

    [Header("Palette")]
    [Tooltip("PlatterConfig assets the player can choose from when placing objects.")]
    public List<PlatterConfig> palette = new();

    // per-ring slot state — jagged arrays indexed by [ring][slot]
    private PlatterConfig[][] _configs;
    private GameObject[][]   _objects;
    private GameObject[][]   _markers;

    private static readonly Color ColOccupied = new Color(0.2f, 0.8f, 0.8f);
    private static readonly Color ColHovered  = Color.white;

    private Color[] _ringColors;
    private int _hoveredRing = -1;
    private int _hoveredSlot = -1;

    private void Awake()
    {
        InitArrays();
        SpawnAllMarkers();
    }

    private void InitArrays()
    {
        _configs    = new PlatterConfig[rings.Count][];
        _objects    = new GameObject[rings.Count][];
        _markers    = new GameObject[rings.Count][];
        _ringColors = new Color[rings.Count];
        for (int r = 0; r < rings.Count; r++)
        {
            int n = rings[r].slotCount;
            _configs[r] = new PlatterConfig[n];
            _objects[r] = new GameObject[n];
            _markers[r] = new GameObject[n];

            float hue = rings.Count > 1 ? (float)r / (rings.Count - 1) * 0.5f : 0.5f;
            _ringColors[r] = Color.HSVToRGB(hue, 0.7f, 0.6f);
        }
    }

    // ── Slot geometry ─────────────────────────────────────────────────────

    public Vector3 SlotLocalPosition(int ring, int slot)
    {
        float slotAngle = 360f / rings[ring].slotCount;
        float rad = slot * slotAngle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad) * rings[ring].radius, 0.5f, Mathf.Cos(rad) * rings[ring].radius);
    }

    /// <summary>
    /// Given a world-space point on the platter surface, returns the nearest ring and slot.
    /// </summary>
    public bool WorldPointToRingAndSlot(Vector3 worldPoint, out int ringIndex, out int slotIndex)
    {
        ringIndex = -1;
        slotIndex = -1;
        if (rings.Count == 0) return false;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        float dist = new Vector2(local.x, local.z).magnitude;

        // nearest ring by radial distance
        float best = float.MaxValue;
        for (int r = 0; r < rings.Count; r++)
        {
            float delta = Mathf.Abs(dist - rings[r].radius);
            if (delta < best) { best = delta; ringIndex = r; }
        }

        // nearest slot in that ring
        float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        float slotAngle = 360f / rings[ringIndex].slotCount;
        slotIndex = Mathf.RoundToInt(angle / slotAngle) % rings[ringIndex].slotCount;
        return true;
    }

    // ── Placement API ─────────────────────────────────────────────────────

    public bool IsOccupied(int ring, int slot) => _objects[ring][slot] != null;

    public bool Place(int ring, int slot, PlatterConfig config)
    {
        if (ring < 0 || ring >= rings.Count) return false;
        if (IsOccupied(ring, slot) || config == null) return false;

        var prefab = rings[ring].platterObjectPrefab;
        if (prefab == null) { Debug.LogWarning($"[PlatterLayout] Ring {ring} has no prefab assigned."); return false; }

        var go = Instantiate(prefab, transform);
        go.transform.SetLocalPositionAndRotation(
            SlotLocalPosition(ring, slot),
            Quaternion.Euler(0f, slot * (360f / rings[ring].slotCount), 0f));

        var po = go.GetComponent<PlatterObject>();
        po.config = config;
        po.Apply(); // Awake ran before config was set, so apply it now

        _configs[ring][slot] = config;
        _objects[ring][slot] = go;
        SetMarkerColor(ring, slot, ColOccupied);
        return true;
    }

    public void Remove(int ring, int slot)
    {
        if (ring < 0 || ring >= rings.Count || !IsOccupied(ring, slot)) return;
        Destroy(_objects[ring][slot]);
        _objects[ring][slot] = null;
        _configs[ring][slot] = null;
        bool isHovered = ring == _hoveredRing && slot == _hoveredSlot;
        SetMarkerColor(ring, slot, isHovered ? ColHovered : _ringColors[ring]);
    }

    // ── Hover highlight ───────────────────────────────────────────────────

    public void SetHoveredRingSlot(int ring, int slot)
    {
        if (_hoveredRing == ring && _hoveredSlot == slot) return;

        if (_hoveredRing >= 0 && _hoveredSlot >= 0)
            SetMarkerColor(_hoveredRing, _hoveredSlot,
                IsOccupied(_hoveredRing, _hoveredSlot) ? ColOccupied : _ringColors[_hoveredRing]);

        _hoveredRing = ring;
        _hoveredSlot = slot;

        if (_hoveredRing >= 0 && _hoveredSlot >= 0)
            SetMarkerColor(_hoveredRing, _hoveredSlot, ColHovered);
    }

    public void ClearHover()
    {
        if (_hoveredRing < 0) return;
        SetMarkerColor(_hoveredRing, _hoveredSlot,
            IsOccupied(_hoveredRing, _hoveredSlot) ? ColOccupied : _ringColors[_hoveredRing]);
        _hoveredRing = -1;
        _hoveredSlot = -1;
    }

    // ── Marker visuals ────────────────────────────────────────────────────

    private void SpawnAllMarkers()
    {
        for (int r = 0; r < rings.Count; r++)
        {
            // marker size scales with slot spacing so they're never too crowded or too tiny
            float spacing = 2f * Mathf.PI * rings[r].radius / rings[r].slotCount;
            float size = Mathf.Clamp(spacing * 0.35f, 0.02f, 0.2f);

            for (int i = 0; i < rings[r].slotCount; i++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                m.name = $"Ring{r}_Marker_{i}";
                m.transform.SetParent(transform);
                m.transform.localPosition = SlotLocalPosition(r, i);
                m.transform.localScale    = Vector3.one * size;
                Destroy(m.GetComponent<SphereCollider>());
                _markers[r][i] = m;
                SetMarkerColor(r, i, _ringColors[r]);
            }
        }
    }

    private void SetMarkerColor(int ring, int slot, Color color)
    {
        var m = _markers[ring][slot];
        if (m == null) return;
        m.GetComponent<Renderer>().material.color = color;
    }

    // ── Scene view preview ────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        for (int r = 0; r < rings.Count; r++)
        {
            // each ring gets a distinct hue
            float hue = rings.Count > 1 ? (float)r / (rings.Count - 1) * 0.5f : 0.5f;
            Gizmos.color = Color.HSVToRGB(hue, 0.8f, 1f);

            float rad = rings[r].radius;
            int   seg = Mathf.Max(rings[r].slotCount * 2, 64);
            Vector3 prev = transform.TransformPoint(new Vector3(0f, 0f, rad));
            for (int i = 1; i <= seg; i++)
            {
                float a    = i / (float)seg * Mathf.PI * 2f;
                Vector3 next = transform.TransformPoint(new Vector3(Mathf.Sin(a) * rad, 0f, Mathf.Cos(a) * rad));
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

            for (int i = 0; i < rings[r].slotCount; i++)
                Gizmos.DrawSphere(transform.TransformPoint(SlotLocalPosition(r, i)), 0.04f);
        }
    }
}
