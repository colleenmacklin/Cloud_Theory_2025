using UnityEngine;

namespace Synthic
{
    public class SynthSphere : MonoBehaviour
    {
        [SerializeField] private float minCollisionVelocity = 0.5f;
        [SerializeField] private float maxCollisionVelocity = 10f;

[Header("Distance Attenuation")]
[SerializeField] private float minDistance   = 1f;
[SerializeField] private float maxDistance   = 100f;
[SerializeField, Range(0.1f, 1f)] private float falloffCurve = 0.25f; // lower = gentler

        private PolyphonicGenerator _generator;
        private float _frequency;
        private Renderer _renderer;
        private float _lastCollisionTime = -1f;
        [SerializeField] private float collisionCooldown = 0.05f;

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

        public void Initialize(PolyphonicGenerator generator, float frequency)
        {
            _generator = generator;
            _frequency = frequency;
            _renderer  = GetComponent<Renderer>();
            ApplyNoteColor();
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

        public void PlayNote(float velocity)
        {
            if (_generator == null) return;
            float distanceScale = GetDistanceScale();
            _generator.ForceNoteOn(_frequency, velocity * distanceScale);
        }
/*
private float GetDistanceScale()
{
    if (_playerTransform == null) return 1f;

    float distance = Vector3.Distance(transform.position, _playerTransform.position);

    // linear falloff - simple and predictable
    float t = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
    return 1f - t;
}
*/

private float GetDistanceScale()
{
    if (_playerTransform == null) return 1f;

    float distance = Vector3.Distance(transform.position, _playerTransform.position);
    float t = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
    return 1f - Mathf.Pow(t, falloffCurve);
}
        private void OnCollisionEnter(Collision collision)
        {
            float impactVelocity = collision.relativeVelocity.magnitude;
            if (impactVelocity < minCollisionVelocity) return;

            if (Time.time - _lastCollisionTime < collisionCooldown) return;
            _lastCollisionTime = Time.time;

            float velocity = Mathf.Clamp01(impactVelocity / maxCollisionVelocity);
            PlayNote(velocity);

            if (_renderer != null)
            {
                int midiNote = Note.FrequencyToMidi(_frequency);
                int semitone = midiNote % 12;
                Color color  = NoteColors[semitone];
                _renderer.material.SetColor("_EmissionColor", color * velocity * 2f);
            }
        }
    }
}