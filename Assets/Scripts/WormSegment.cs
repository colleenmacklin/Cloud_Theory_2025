using UnityEngine;

namespace Synthic
{
    /// <summary>
    /// Attached to every sphere segment inside a SynthWorm.
    /// Fires ForceNoteOn on collision, with distance attenuation,
    /// exactly like SynthSphere does.
    /// </summary>
    public class WormSegment : MonoBehaviour
    {
        [Header("Collision Thresholds")]
        [SerializeField] private float minCollisionVelocity = 0.05f;
        [SerializeField] private float maxCollisionVelocity = 10f;
        [SerializeField] private float collisionCooldown    = 0.08f;

        [Header("Distance Attenuation")]
        [SerializeField] private float minDistance   = 1f;
        [SerializeField] private float maxDistance   = 50f;
        [SerializeField, Range(0.1f, 1f)] private float falloffCurve = 0.25f;

        private PolyphonicGenerator _generator;
        private float               _frequency;
        private float               _lastCollisionTime = -999f;

        private static Transform _playerTransform;

        public static void SetPlayerTransform(Transform t) => _playerTransform = t;

        public void Initialize(PolyphonicGenerator generator, float frequency,
                               float minColVel, float maxColVel)
        {
            _generator            = generator;
            _frequency            = frequency;
            minCollisionVelocity  = minColVel;
            maxCollisionVelocity  = maxColVel;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time - _lastCollisionTime < collisionCooldown) return;
            _lastCollisionTime = Time.time;

            float impact = collision.relativeVelocity.magnitude;
            if (impact < minCollisionVelocity) return;

            float velocity     = Mathf.Clamp01(impact / maxCollisionVelocity);
            float distScale    = GetDistanceScale();
            _generator?.ForceNoteOn(_frequency, velocity * distScale);
        }

        private float GetDistanceScale()
        {
            if (_playerTransform == null) return 1f;
            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            float t        = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
            return 1f - Mathf.Pow(t, falloffCurve);
        }
    }
}
