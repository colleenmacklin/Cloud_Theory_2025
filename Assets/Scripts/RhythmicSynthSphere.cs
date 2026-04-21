using UnityEngine;

namespace Synthic
{
    [RequireComponent(typeof(Rigidbody))]
    public class RhythmicSynthSphere : MonoBehaviour
    {
        [Header("Synth")]
        [SerializeField] private float minCollisionVelocity = 0.05f;
        [SerializeField] private float maxCollisionVelocity = 10f;
        [SerializeField] private float collisionCooldown    = 0.1f;

        [Header("Distance Attenuation")]
        [SerializeField] private float minDistance   = 1f;
        [SerializeField] private float maxDistance   = 50f;
        [SerializeField, Range(0.1f, 1f)] private float falloffCurve = 0.25f;

        [Header("Rhythm")]
        [SerializeField] private float groundHeight  = 0f;
        [SerializeField, Range(0f, 1f)] private float bounciness = 0.8f;
        [SerializeField] private float maxDropHeight = 10f;

        // set by SphereSpawner
        public int beatsPerCycle = 1;

        private PolyphonicGenerator _generator;
        private float               _frequency;
        private Renderer            _renderer;
        private Rigidbody           _rb;

        private float _lastCollisionTime = -999f;
        private bool  _isSynced          = false;
        private float _dropHeight;

        private static Transform _playerTransform;

        public static void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }

        private static readonly Color[] NoteColors = new Color[]
        {
            new Color(1.0f, 0.2f, 0.2f), // C
            new Color(1.0f, 0.5f, 0.2f), // C#
            new Color(1.0f, 0.8f, 0.2f), // D
            new Color(0.8f, 1.0f, 0.2f), // D#
            new Color(0.4f, 1.0f, 0.2f), // E
            new Color(0.2f, 1.0f, 0.6f), // F
            new Color(0.2f, 0.9f, 1.0f), // F#
            new Color(0.2f, 0.5f, 1.0f), // G
            new Color(0.4f, 0.2f, 1.0f), // G#
            new Color(0.7f, 0.2f, 1.0f), // A
            new Color(1.0f, 0.2f, 0.8f), // A#
            new Color(1.0f, 0.2f, 0.5f), // B
        };

        public void Initialize(PolyphonicGenerator generator, float frequency,
                               int beatsPerCycle, float bounciness,
                               float minCollisionVelocity, float maxCollisionVelocity)
        {
            _generator              = generator;
            _frequency              = frequency;
            _rb                     = GetComponent<Rigidbody>();
            _renderer               = GetComponent<Renderer>();
            this.beatsPerCycle      = beatsPerCycle;
            this.bounciness         = bounciness;
            this.minCollisionVelocity = minCollisionVelocity;
            this.maxCollisionVelocity = maxCollisionVelocity;

            ApplyNoteColor();
            ApplyPhysicsMaterial();
            CalculateDropHeight();
        }
private void Update()
{
    if (_rb == null) return;
    // cap velocity to prevent tunneling
    if (_rb.linearVelocity.magnitude > 20f)
        _rb.linearVelocity = _rb.linearVelocity.normalized * 20f;
}

        private void ApplyNoteColor()
        {
            if (_renderer == null) return;
            int midiNote = Note.FrequencyToMidi(_frequency);
            int semitone = midiNote % 12;
            Color color  = NoteColors[semitone];

            _renderer.material = new Material(_renderer.sharedMaterial);
            _renderer.material.color = color;
            _renderer.material.SetColor("_EmissionColor", color * 0.5f);
            _renderer.material.EnableKeyword("_EMISSION");
        }

        private void ApplyPhysicsMaterial()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            var mat             = new PhysicsMaterial("SphereBounce");
            mat.bounciness      = bounciness;
            mat.dynamicFriction = 0f;
            mat.staticFriction  = 0f;
            mat.bounceCombine   = PhysicsMaterialCombine.Maximum;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            col.material        = mat;
        }

        private void CalculateDropHeight()
        {
            if (RhythmicMasterClock.Instance == null) return;

            float g               = Mathf.Abs(Physics.gravity.y);
            float quarterNote     = 60f / RhythmicMasterClock.Instance.BPM;
            float cycleDuration   = quarterNote * beatsPerCycle;
            float tFall           = cycleDuration / (1f + bounciness);
            _dropHeight           = Mathf.Min((g / 2f) * tFall * tFall, maxDropHeight);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // ignore sphere-sphere collisions for rhythm sync
            bool isGround = IsGroundCollision(collision);

            if (Time.time - _lastCollisionTime < collisionCooldown) return;
            _lastCollisionTime = Time.time;

            // trigger note on any collision
            float impactVelocity = collision.relativeVelocity.magnitude;
            if (impactVelocity >= minCollisionVelocity)
            {
                float velocity = Mathf.Clamp01(impactVelocity / maxCollisionVelocity);
                PlayNote(velocity);
            }

            // sync rhythm on first ground bounce
            if (isGround && !_isSynced)
            {
                _isSynced = true;
                SyncToRhythm();
            }
            else if (isGround && _isSynced)
            {
                // correct each subsequent bounce to stay in tempo
                SyncToRhythm();
            }
        }

        private void SyncToRhythm()
        {
            if (_rb == null) return;

            CalculateDropHeight();

            float g             = Mathf.Abs(Physics.gravity.y);
            float currentY      = transform.position.y;
            float heightNeeded  = (groundHeight + _dropHeight) - currentY;
            float correctedVel  = Mathf.Sqrt(2f * g * Mathf.Max(0f, heightNeeded));

            Vector3 vel = _rb.linearVelocity;
            vel.y = correctedVel * bounciness;
            _rb.linearVelocity = vel;
        }

        private bool IsGroundCollision(Collision collision)
        {
            // not a ground collision if it's another rigidbody sphere
            if (collision.gameObject.GetComponent<Rigidbody>() != null &&
                collision.gameObject.GetComponent<RhythmicSynthSphere>() != null)
                return false;

            foreach (var contact in collision.contacts)
                if (contact.point.y <= groundHeight + 0.15f)
                    return true;

            return false;
        }

        public void PlayNote(float velocity)
        {
            if (_generator == null) return;
            float distanceScale = GetDistanceScale();
            _generator.ForceNoteOn(_frequency, velocity * distanceScale,
                                   gameObject.GetInstanceID());
        }

        private float GetDistanceScale()
        {
            if (_playerTransform == null) return 1f;
            float distance = Vector3.Distance(
                transform.position, _playerTransform.position);
            float t = Mathf.Clamp01(
                (distance - minDistance) / (maxDistance - minDistance));
            return 1f - Mathf.Pow(t, falloffCurve);
        }
    }
}