using UnityEngine;
using System;

namespace Synthic
{
    /// <summary>
    /// Pong ball. Lives in a 3D scene but moves on the XY plane only
    /// (Rigidbody Z-locked, no gravity). Sound fires only on paddle and
    /// wall collisions (tagged "PongPaddle" and "PongWall"). Destroyed when
    /// it exits through a goal trigger (tagged "PongGoal").
    ///
    /// Carries a launchedByLeft flag so the sequencer knows who scores.
    /// Fires OnScored(bool launchedByLeft) when it exits through a goal.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PongBall : MonoBehaviour
    {
        // ── Runtime state ────────────────────────────────────────────────────
        private PolyphonicGenerator _generator;
        private float               _frequency;
        private Renderer            _renderer;
        private Rigidbody           _rb;
        private bool                _dead;
        private bool                _launchedByLeft;

        [Header("Collision Sound")]
        [SerializeField] private float minCollisionVelocity = 0.5f;
        [SerializeField] private float maxCollisionVelocity = 20f;
        [SerializeField] private float collisionCooldown    = 0.05f;

        private float _lastCollisionTime = -999f;

        /// <summary>Fired when the ball exits through a goal. bool = launchedByLeft.</summary>
        public event Action<bool> OnScored;

        // ── Note colours ─────────────────────────────────────────────────────
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

        /// <summary>
        /// launchedByLeft: true = left paddle launched this ball.
        /// </summary>
        public void Initialize(PolyphonicGenerator generator, float frequency,
                               Vector3 velocity, bool launchedByLeft)
        {
            _generator      = generator;
            _frequency      = frequency;
            _launchedByLeft = launchedByLeft;
            _renderer       = GetComponent<Renderer>();
            _rb             = GetComponent<Rigidbody>();

            // 2D-style physics: no gravity, locked to XY plane
            _rb.useGravity  = false;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY
                            | RigidbodyConstraints.FreezeRotationZ;

            // perfect bounce — no energy loss
            var col = GetComponent<Collider>();
            if (col != null)
            {
                var pm = new PhysicsMaterial("PongBounce");
                pm.bounciness      = 1f;
                pm.dynamicFriction = 0f;
                pm.staticFriction  = 0f;
                pm.bounceCombine   = PhysicsMaterialCombine.Maximum;
                pm.frictionCombine = PhysicsMaterialCombine.Minimum;
                col.material       = pm;
            }

            ApplyNoteColor();
            _rb.linearVelocity = velocity;

            // fire the note immediately on launch
            _generator?.ForceNoteOn(_frequency, 0.8f);
        }

        /// <summary>Silences the note and destroys the ball immediately.</summary>
        public void Kill()
        {
            if (_dead) return;
            _dead = true;
            _generator?.NoteOff(_frequency);
            Destroy(gameObject);
            Destroy(_renderer.material);

        }

        // ─────────────────────────────────────────────────────────────────────
        //  Collision → sound
        // ─────────────────────────────────────────────────────────────────────

        private void OnCollisionEnter(Collision col)
        {
            if (_dead) return;

            string tag      = col.gameObject.tag;
            bool isPaddle   = tag == "PongPaddle";
            bool isWall     = tag == "PongWall";

            if (!isPaddle && !isWall) return;
            if (Time.time - _lastCollisionTime < collisionCooldown) return;
            _lastCollisionTime = Time.time;

            float impact   = col.relativeVelocity.magnitude;
            if (impact < minCollisionVelocity) return;

            float velocity = Mathf.Clamp01(impact / maxCollisionVelocity);

            // ForceNoteOn handles voice stealing cleanly — calling NoteOff first
            // caused a hard amplitude discontinuity (the crackle)
            _generator?.ForceNoteOn(_frequency, velocity);

            // notify paddle timbre for filter modulation
            if (isPaddle)
            {
                var timbre = col.gameObject.GetComponentInParent<PongBallTimbre>();
                timbre?.OnBallHit(velocity);
            }

            // flash emission
            if (_renderer != null)
            {
                int semitone = Note.FrequencyToMidi(_frequency) % 12;
                _renderer.material.SetColor("_EmissionColor",
                    NoteColors[semitone] * velocity * 2.5f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Goal trigger
        // ─────────────────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("PongGoal")) return;
            if (_dead) return;
            _dead = true;

            OnScored?.Invoke(_launchedByLeft);
            _generator?.NoteOff(_frequency);
            Destroy(gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyNoteColor()
        {
            if (_renderer == null) return;
            int semitone = Note.FrequencyToMidi(_frequency) % 12;
            Color c = NoteColors[semitone];

            _renderer.material = new Material(_renderer.sharedMaterial);
            _renderer.material.color = c;
            _renderer.material.SetColor("_EmissionColor", c * 0.5f);
            _renderer.material.EnableKeyword("_EMISSION");
        }
    }
}
