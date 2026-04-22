using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Synthic
{
    /// <summary>
    /// A worm that floats and writhes through the air.
    ///
    /// Lifecycle:
    ///   1. Key held  → NoteOn sustained, worm grows, swims with sine-wave motion.
    ///   2. Key released OR collision → BeginDieSequence():
    ///      - Tail segments destroyed one-by-one over dissolveDuration (3 s).
    ///      - Generator velocity fades from 0.9 → 0 in parallel.
    ///      - NoteOff + Destroy when all segments gone.
    /// </summary>
    public class SynthWorm : MonoBehaviour
    {
        // ── Tuning (set via Initialize or Inspector) ──────────────────────────
        [Header("Shape")]
        public float segmentRadius  = 0.18f;
        public float segmentSpacing = 0.36f;
        [Range(4, 16)] public int tubeSides = 8;

        [Header("Motion")]
        public float swimSpeed     = 2.5f;
        public float waveAmplitude = 0.55f;
        public float waveFrequency = 1.8f;

        [Header("Growth")]
        public float growInterval = 0.10f;
        public int   maxSegments  = 24;

        [Header("Death")]
        public float dissolveDuration = 3f;

        // ── Private state ─────────────────────────────────────────────────────
        private PolyphonicGenerator _generator;
        private float               _frequency;
        private GameObject          _segmentPrefab;
        private Color               _noteColor;

        // spine – purely procedural, no Rigidbody on segments
        private readonly List<Transform> _segTransforms = new();
        private readonly List<Vector3>   _segPositions  = new();
        private Vector3[]                _segSmoothVel  = new Vector3[0];

        // head state
        private Vector3 _headPos;
        private Vector3 _headDir;
        private float   _wavePhase;

        // tube mesh
        private MeshFilter   _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh         _tubeMesh;

        // state flags
        private bool      _growing = true;
        private bool      _dying   = false;
        private Coroutine _growCo;

        // ── Note colours (matches SynthSphere) ───────────────────────────────
        private static readonly Color[] NoteColors =
        {
            new Color(1.0f, 0.2f, 0.2f), new Color(1.0f, 0.5f, 0.2f),
            new Color(1.0f, 0.8f, 0.2f), new Color(0.8f, 1.0f, 0.2f),
            new Color(0.4f, 1.0f, 0.2f), new Color(0.2f, 1.0f, 0.6f),
            new Color(0.2f, 0.9f, 1.0f), new Color(0.2f, 0.5f, 1.0f),
            new Color(0.4f, 0.2f, 1.0f), new Color(0.7f, 0.2f, 1.0f),
            new Color(1.0f, 0.2f, 0.8f), new Color(1.0f, 0.2f, 0.5f),
        };

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Initialize(
            PolyphonicGenerator generator,
            float               frequency,
            Vector3             spawnPosition,
            Vector3             launchDirection,
            GameObject          segmentPrefab,
            float               segRadius,
            float               growInterval,
            int                 maxSegments,
            int                 tubeSides)
        {
            _generator     = generator;
            _frequency     = frequency;
            _segmentPrefab = segmentPrefab;
            segmentRadius  = segRadius;
            segmentSpacing = segRadius * 2f + 0.06f;
            this.growInterval = growInterval;
            this.maxSegments  = maxSegments;
            this.tubeSides    = tubeSides;

            int semitone = Note.FrequencyToMidi(frequency) % 12;
            _noteColor   = NoteColors[semitone];

            // head position and initial direction (horizontal – worm floats level)
            _headPos = spawnPosition;
            _headDir = new Vector3(launchDirection.x, 0f, launchDirection.z).normalized;
            if (_headDir == Vector3.zero) _headDir = Vector3.forward;

            // set up tube mesh on this root object
            _tubeMesh             = new Mesh { name = "WormTube" };
            _meshFilter           = gameObject.AddComponent<MeshFilter>();
            _meshRenderer         = gameObject.AddComponent<MeshRenderer>();
            _meshFilter.mesh      = _tubeMesh;

            // clone the material from the segment prefab so we automatically
            // use whatever shader/pipeline the project is set up with (URP, HDRP, Built-in)
            var sourceMat = segmentPrefab.GetComponent<Renderer>()?.sharedMaterial;
            var mat = sourceMat != null
                ? new Material(sourceMat)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));

            mat.color = _noteColor;
            mat.SetColor("_EmissionColor", _noteColor * 0.5f);
            mat.EnableKeyword("_EMISSION");
            _meshRenderer.material = mat;

            // first segment at spawn point
            AddSegment(_headPos);

            // save current ADSR so we can restore it on cleanup, then set
            // sustain to full so the note holds cleanly without retriggering
            _generator.SaveADSR();
            _generator.SetSustain(1f);
            _generator.NoteOn(_frequency, 0.9f);

            // begin growing
            _growCo = StartCoroutine(GrowRoutine());
        }

        /// <summary>Called by SphereSpawner when the key is released.</summary>
        public void StartDying() { if (!_dying) BeginDieSequence(); }

        /// <summary>Called by WormCollisionReporter when a segment hits something.</summary>
        public void OnSegmentCollision() { if (!_dying) BeginDieSequence(); }

        // ─────────────────────────────────────────────────────────────────────
        //  Per-frame motion
        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_dying || _segTransforms.Count == 0) return;

            _wavePhase += waveFrequency * Time.deltaTime;

            // lateral sine wave steers the head
            Vector3 perp   = Vector3.Cross(_headDir, Vector3.up);
            Vector3 offset = perp * (Mathf.Sin(_wavePhase) * waveAmplitude * Time.deltaTime * 4f);
            _headDir = (_headDir + offset).normalized;

            // advance head
            _headPos += _headDir * swimSpeed * Time.deltaTime;
            _segPositions[0] = _headPos;

            // each segment follows the one ahead, maintaining spacing
            for (int i = 1; i < _segPositions.Count; i++)
            {
                Vector3 toTarget = _segPositions[i - 1]
                    + (_segPositions[i] - _segPositions[i - 1]).normalized * segmentSpacing;
                // smooth damp toward the target
                _segPositions[i] = Vector3.SmoothDamp(
                    _segPositions[i], toTarget,
                    ref _segSmoothVel[i], 0.08f, 100f, Time.deltaTime);
            }

            // sync transforms
            for (int i = 0; i < _segTransforms.Count; i++)
                if (_segTransforms[i] != null)
                    _segTransforms[i].position = _segPositions[i];

            RebuildTubeMesh();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Growth
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator GrowRoutine()
        {
            while (_growing && _segTransforms.Count < maxSegments)
            {
                yield return new WaitForSeconds(growInterval);
                if (_dying || !_growing) yield break;

                Vector3 tail = _segPositions[_segPositions.Count - 1];
                Vector3 prev = _segPositions.Count > 1
                    ? _segPositions[_segPositions.Count - 2]
                    : tail + _headDir;
                Vector3 newPos = tail + (tail - prev).normalized * segmentSpacing;
                AddSegment(newPos);
            }
        }

        private void AddSegment(Vector3 worldPos)
        {
            GameObject seg = Instantiate(_segmentPrefab, worldPos, Quaternion.identity);
            seg.name = $"WormSeg_{_segTransforms.Count}";
            seg.transform.localScale = Vector3.one * segmentRadius * 2f;

            // kinematic – no physics simulation on segments
            var rb = seg.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

            // hide sphere renderer; tube mesh does the skinning
            var rend = seg.GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;

            // attach collision reporter on the segment collider
            var reporter = seg.AddComponent<WormCollisionReporter>();
            reporter.SetWorm(this);

            _segTransforms.Add(seg.transform);
            _segPositions.Add(worldPos);

            // grow the smooth-damp velocity array
            System.Array.Resize(ref _segSmoothVel, _segTransforms.Count);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Die sequence
        // ─────────────────────────────────────────────────────────────────────

        private void BeginDieSequence()
        {
            _dying   = true;
            _growing = false;
            if (_growCo != null) StopCoroutine(_growCo);
            StartCoroutine(DieRoutine());
        }

        private IEnumerator DieRoutine()
        {
            int   startCount   = _segTransforms.Count;
            float elapsed      = 0f;
            float startSustain = _generator != null ? _generator.Sustain : 1f;

            while (elapsed < dissolveDuration && _segTransforms.Count > 0)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dissolveDuration);

                // fade sustain smoothly from current value → 0
                // no retriggering – just lets the held note get quieter
                _generator?.SetSustain(Mathf.Lerp(startSustain, 0f, t));

                // how many segments should remain right now
                int targetCount = Mathf.RoundToInt(Mathf.Lerp(startCount, 0, t));

                // destroy from tail inward until we reach target count
                while (_segTransforms.Count > targetCount)
                {
                    int last = _segTransforms.Count - 1;
                    if (_segTransforms[last] != null)
                        Destroy(_segTransforms[last].gameObject);
                    _segTransforms.RemoveAt(last);
                    _segPositions.RemoveAt(last);
                }

                if (_segTransforms.Count >= 2)
                    RebuildTubeMesh();
                else
                    _meshRenderer.enabled = false;

                yield return null;
            }

            // release note and restore whatever ADSR was set before the worm started
            _generator?.NoteOff(_frequency);
            _generator?.RestoreADSR();

            foreach (var t in _segTransforms)
                if (t != null) Destroy(t.gameObject);

            Destroy(gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Immediate destroy (delete-key from SphereSpawner)
        // ─────────────────────────────────────────────────────────────────────

        public void DestroyWorm()
        {
            StopAllCoroutines();
            _generator?.NoteOff(_frequency);
            _generator?.RestoreADSR();
            foreach (var t in _segTransforms)
                if (t != null) Destroy(t.gameObject);
            _segTransforms.Clear();
            Destroy(gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Tube mesh builder
        // ─────────────────────────────────────────────────────────────────────

        private void RebuildTubeMesh()
        {
            int ringCount = _segPositions.Count;
            if (ringCount < 2) { _tubeMesh.Clear(); return; }

            int sides        = tubeSides;
            int vertsPerRing = sides + 1;
            int vertCount    = ringCount * vertsPerRing + 2;

            Vector3[] verts = new Vector3[vertCount];
            Vector3[] norms = new Vector3[vertCount];
            Vector2[] uvs   = new Vector2[vertCount];
            float r = segmentRadius;

            for (int ring = 0; ring < ringCount; ring++)
            {
                Vector3 centre = _segPositions[ring];
                Vector3 fwd;

                if      (ring == 0)             fwd = (ringCount > 1 ? _segPositions[1] - centre : Vector3.forward).normalized;
                else if (ring == ringCount - 1) fwd = (centre - _segPositions[ring - 1]).normalized;
                else                            fwd = (_segPositions[ring + 1] - _segPositions[ring - 1]).normalized;

                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;

                Vector3 up    = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) < 0.98f ? Vector3.up : Vector3.right;
                Vector3 right = Vector3.Cross(up, fwd).normalized;
                up            = Vector3.Cross(fwd, right);

                for (int s = 0; s <= sides; s++)
                {
                    float   a   = s * Mathf.PI * 2f / sides;
                    Vector3 dir = Mathf.Cos(a) * right + Mathf.Sin(a) * up;
                    int     vi  = ring * vertsPerRing + s;
                    verts[vi] = transform.InverseTransformPoint(centre + dir * r);
                    norms[vi] = transform.InverseTransformDirection(dir);
                    uvs[vi]   = new Vector2((float)s / sides, (float)ring / (ringCount - 1));
                }
            }

            int headCap = ringCount * vertsPerRing;
            int tailCap = headCap + 1;
            verts[headCap] = transform.InverseTransformPoint(_segPositions[0]);
            verts[tailCap] = transform.InverseTransformPoint(_segPositions[ringCount - 1]);
            norms[headCap] = -transform.InverseTransformDirection(
                (ringCount > 1 ? _segPositions[1] - _segPositions[0] : Vector3.forward).normalized);
            norms[tailCap] = transform.InverseTransformDirection(
                (ringCount > 1 ? _segPositions[ringCount - 1] - _segPositions[ringCount - 2] : Vector3.forward).normalized);

            int tubeTriCount = (ringCount - 1) * sides * 6;
            int capTriCount  = sides * 3 * 2;
            int[] tris = new int[tubeTriCount + capTriCount];
            int ti = 0;

            for (int ring = 0; ring < ringCount - 1; ring++)
                for (int s = 0; s < sides; s++)
                {
                    int a = ring * vertsPerRing + s;
                    int b = (ring + 1) * vertsPerRing + s;
                    tris[ti++] = a;   tris[ti++] = b;   tris[ti++] = a + 1;
                    tris[ti++] = b;   tris[ti++] = b + 1; tris[ti++] = a + 1;
                }

            for (int s = 0; s < sides; s++)
            { tris[ti++] = headCap; tris[ti++] = s + 1; tris[ti++] = s; }

            int tailStart = (ringCount - 1) * vertsPerRing;
            for (int s = 0; s < sides; s++)
            { tris[ti++] = tailCap; tris[ti++] = tailStart + s; tris[ti++] = tailStart + s + 1; }

            _tubeMesh.Clear();
            _tubeMesh.vertices  = verts;
            _tubeMesh.normals   = norms;
            _tubeMesh.uv        = uvs;
            _tubeMesh.triangles = tris;
            _tubeMesh.RecalculateBounds();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tiny per-segment component – reports first external collision to worm
    // ─────────────────────────────────────────────────────────────────────────

    public class WormCollisionReporter : MonoBehaviour
    {
        private SynthWorm _worm;
        private bool      _reported;

        public void SetWorm(SynthWorm worm) => _worm = worm;

        private void OnCollisionEnter(Collision col)
        {
            if (_reported) return;
            // ignore worm-segment ↔ worm-segment contacts
            if (col.gameObject.GetComponent<WormCollisionReporter>() != null) return;
            _reported = true;
            _worm?.OnSegmentCollision();
        }
    }
}
