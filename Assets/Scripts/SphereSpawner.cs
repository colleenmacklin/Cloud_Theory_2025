using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Synthic
{
    public class SphereSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PolyphonicGenerator generator;
        [SerializeField] private NoteInput           noteInput;
        [SerializeField] private GameObject          spherePrefab;
        [SerializeField] private Camera              playerCamera;

        [Header("Throw Settings")]
        [SerializeField, Range(1f, 30f)]  private float throwForce    = 10f;
        [SerializeField, Range(0f, 1f)]   private float throwArcAngle = 0.8f;
        [SerializeField, Range(0.1f, 2f)] private float sphereRadius  = 0.2f;

        [Header("Sphere Settings")]
        [SerializeField] private float minCollisionVelocity = 0.05f;
        [SerializeField] private float maxCollisionVelocity = 10f;

        [Header("Caps Lock Mode")]
        [SerializeField, Range(0f, 5f)]  private float capsLockAttack  = 0.05f;
        [SerializeField, Range(0f, 1f)]  private float capsLockSustain = 1f;
        [SerializeField, Range(0f, 5f)]  private float capsLockRelease = 0.5f;

        [Header("Rhythmic Bounce")]
        [SerializeField] private bool  rhythmicBounce  = true;
        [SerializeField, Range(1, 16)] private int   beatsPerCycle  = 1;
        [SerializeField, Range(0f, 1f)] private float sphereBounciness = 0.8f;

        [Header("Long-Press Worm")]
        [Tooltip("How long (seconds) a key must be held before worm mode activates")]
        [SerializeField, Range(0.05f, 1f)] private float longPressThreshold = 0.3f;
        [Tooltip("Worm segment prefab (leave empty to reuse spherePrefab)")]
        [SerializeField] private GameObject wormSegmentPrefab;
        [Tooltip("How many tube cross-section sides on the worm mesh")]
        [SerializeField, Range(4, 16)] private int wormTubeSides = 8;
        [Tooltip("Seconds between each new worm segment while key is held")]
        [SerializeField, Range(0.05f, 1f)] private float wormGrowInterval = 0.12f;
        [Tooltip("Max segments a worm can grow to")]
        [SerializeField, Range(2, 40)] private int wormMaxSegments = 20;

        // ── Note key layout ──────────────────────────────────────────────────
        private static readonly Dictionary<Key, Note.Name> NoteKeys =
            new Dictionary<Key, Note.Name>
        {
            { Key.Z, Note.Name.C      },
            { Key.S, Note.Name.CSharp },
            { Key.X, Note.Name.D      },
            { Key.D, Note.Name.DSharp },
            { Key.C, Note.Name.E      },
            { Key.V, Note.Name.F      },
            { Key.G, Note.Name.FSharp },
            { Key.B, Note.Name.G      },
            { Key.H, Note.Name.GSharp },
            { Key.N, Note.Name.A      },
            { Key.J, Note.Name.ASharp },
            { Key.M, Note.Name.B      },
        };

        // ── Runtime state ────────────────────────────────────────────────────
        private int                          _currentOctave     = 3;
        private readonly List<GameObject>    _spawnedSpheres    = new();
        private readonly HashSet<Key>        _heldNoteKeys      = new();
        private readonly Dictionary<Key, float> _heldCapsLockNotes = new();

        // long-press tracking
        // key → time the key was first pressed (in normal mode)
        private readonly Dictionary<Key, float>     _keyPressTime  = new();
        // key → worm that is currently growing for that key
        private readonly Dictionary<Key, SynthWorm> _activeWorms   = new();
        // keys that have already spawned a sphere (so we don't double-spawn)
        private readonly HashSet<Key>               _spawnedSphere = new();

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            SynthSphere.SetPlayerTransform(playerCamera.transform);
            RhythmicSynthSphere.SetPlayerTransform(playerCamera.transform);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            HandleOctaveKeys();
            HandleNoteKeys();
            HandleSphereManagement();
        }

        // ── Octave ────────────────────────────────────────────────────────────

        private void HandleOctaveKeys()
        {
            if (Keyboard.current.commaKey.wasPressedThisFrame)
                _currentOctave = Mathf.Max(0, _currentOctave - 1);
            if (Keyboard.current.periodKey.wasPressedThisFrame)
                _currentOctave = Mathf.Min(8, _currentOctave + 1);
        }

        // ── Note keys ─────────────────────────────────────────────────────────

        private void HandleNoteKeys()
        {
            bool capsLockOn = Keyboard.current.capsLockKey.isPressed;

            foreach (var kvp in NoteKeys)
            {
                Key       inputKey  = kvp.Key;
                Note.Name noteName  = kvp.Value;
                float     frequency = Note.Get(noteName, _currentOctave);

                bool isDown = Keyboard.current[inputKey].wasPressedThisFrame;
                bool isUp   = Keyboard.current[inputKey].wasReleasedThisFrame;
                bool isHeld = Keyboard.current[inputKey].isPressed;

                if (capsLockOn)
                {
                    HandleCapsLockKey(inputKey, frequency, isDown, isUp);
                }
                else
                {
                    HandleNormalKey(inputKey, frequency, isDown, isUp, isHeld);
                }
            }
        }

        // ── Caps-lock (sustained, no sphere) ─────────────────────────────────

        private void HandleCapsLockKey(Key key, float frequency, bool isDown, bool isUp)
        {
            if (isDown && !_heldNoteKeys.Contains(key))
            {
                _heldNoteKeys.Add(key);
                _heldCapsLockNotes[key] = frequency;

                generator.SaveADSR();
                generator.SetAttack(capsLockAttack);
                generator.SetSustain(capsLockSustain);
                generator.SetRelease(capsLockRelease);
                generator.NoteOn(frequency, 0.8f);
            }
            else if (isUp && _heldNoteKeys.Contains(key))
            {
                _heldNoteKeys.Remove(key);
                if (_heldCapsLockNotes.TryGetValue(key, out float heldFreq))
                {
                    generator.NoteOff(heldFreq);
                    _heldCapsLockNotes.Remove(key);
                }

                if (_heldCapsLockNotes.Count == 0)
                    generator.RestoreADSR();
            }
        }

        // ── Normal mode with long-press detection ─────────────────────────────

        private void HandleNormalKey(Key key, float frequency, bool isDown, bool isUp, bool isHeld)
        {
            // release any lingering caps-lock note on this key
            if (_heldNoteKeys.Contains(key) && isUp)
            {
                _heldNoteKeys.Remove(key);
                if (_heldCapsLockNotes.TryGetValue(key, out float heldFreq))
                {
                    generator.NoteOff(heldFreq);
                    _heldCapsLockNotes.Remove(key);
                }
            }

            // ── Key pressed this frame ────────────────────────────────────────
            if (isDown && !_heldNoteKeys.Contains(key))
            {
                _heldNoteKeys.Add(key);
                _keyPressTime[key] = Time.time;
                _spawnedSphere.Remove(key); // reset so we can decide later
            }

            // ── Key held (not yet released) ───────────────────────────────────
            if (isHeld && _heldNoteKeys.Contains(key))
            {
                float heldFor = Time.time - (_keyPressTime.TryGetValue(key, out float pt) ? pt : Time.time);
                bool  isLong  = heldFor >= longPressThreshold;
                bool  hasWorm = _activeWorms.ContainsKey(key);

                if (isLong && !hasWorm && !_spawnedSphere.Contains(key))
                {
                    // worm handles NoteOn internally
                    StartWorm(key, frequency);
                }
                else if (!isLong && !_spawnedSphere.Contains(key))
                {
                    // still within short-press window – do nothing yet (we spawn on release)
                }
            }

            // ── Key released ──────────────────────────────────────────────────
            if (isUp)
            {
                _heldNoteKeys.Remove(key);
                _keyPressTime.Remove(key);

                if (_activeWorms.TryGetValue(key, out SynthWorm worm))
                {
                    // tell worm to begin its death sequence (handles NoteOff itself)
                    worm.StartDying();
                    _activeWorms.Remove(key);
                }
                else if (!_spawnedSphere.Contains(key))
                {
                    // short press – spawn a single sphere now
                    SpawnSphere(frequency);
                }

                _spawnedSphere.Remove(key);
            }
        }

        // ── Worm creation ─────────────────────────────────────────────────────

        private void StartWorm(Key key, float frequency)
        {
            if (spherePrefab == null || playerCamera == null) return;

            Vector3 spawnPos = playerCamera.transform.position
                             + playerCamera.transform.forward * (sphereRadius * 2f + 0.1f);

            GameObject wormRoot = new GameObject($"SynthWorm_{frequency:F0}Hz");
            wormRoot.transform.position = Vector3.zero;

            SynthWorm wormComp = wormRoot.AddComponent<SynthWorm>();

            Vector3 launchDir = playerCamera.transform.forward;

            wormComp.Initialize(
                generator,
                frequency,
                spawnPos,
                launchDir,
                wormSegmentPrefab != null ? wormSegmentPrefab : spherePrefab,
                sphereRadius,
                wormGrowInterval,
                wormMaxSegments,
                wormTubeSides
            );

            _activeWorms[key] = wormComp;
            _spawnedSpheres.Add(wormRoot); // so delete-key can remove worms too
            _spawnedSphere.Add(key);       // prevent double sphere spawn on release
        }

        // ── Normal sphere spawn ────────────────────────────────────────────────

        private void SpawnSphere(float frequency)
        {
            if (spherePrefab == null || playerCamera == null) return;

            Vector3 spawnPosition = playerCamera.transform.position
                                  + playerCamera.transform.forward * (sphereRadius * 2f + 0.1f);

            GameObject sphere = Instantiate(spherePrefab, spawnPosition, Quaternion.identity);
            sphere.transform.localScale = Vector3.one * sphereRadius * 2f;

            if (rhythmicBounce)
            {
                var existing = sphere.GetComponent<SynthSphere>();
                if (existing != null) Destroy(existing);

                var rhythmicSphere = sphere.GetComponent<RhythmicSynthSphere>();
                if (rhythmicSphere == null)
                    rhythmicSphere = sphere.AddComponent<RhythmicSynthSphere>();

                rhythmicSphere.Initialize(generator, frequency, beatsPerCycle,
                                          sphereBounciness, minCollisionVelocity,
                                          maxCollisionVelocity);
            }
            else
            {
                var synthSphere = sphere.GetComponent<SynthSphere>();
                if (synthSphere == null)
                    synthSphere = sphere.AddComponent<SynthSphere>();

                synthSphere.Initialize(generator, frequency);

                var minVelField = typeof(SynthSphere).GetField("minCollisionVelocity",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var maxVelField = typeof(SynthSphere).GetField("maxCollisionVelocity",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                minVelField?.SetValue(synthSphere, minCollisionVelocity);
                maxVelField?.SetValue(synthSphere, maxCollisionVelocity);
            }

            var rb = sphere.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 throwDirection = playerCamera.transform.forward
                                       + playerCamera.transform.up * throwArcAngle;
                rb.linearVelocity = throwDirection.normalized * throwForce;
            }

            _spawnedSpheres.Add(sphere);
        }

        // ── Delete / clear ────────────────────────────────────────────────────

        private void HandleSphereManagement()
        {
            if (Keyboard.current == null) return;

            bool shiftHeld    = Keyboard.current.leftShiftKey.isPressed ||
                                 Keyboard.current.rightShiftKey.isPressed;
            bool deletePressed = Keyboard.current.backspaceKey.wasPressedThisFrame;

            if (deletePressed)
            {
                if (shiftHeld) ClearAllSpheres();
                else           ClearLastSphere();
            }
        }

        private void ClearLastSphere()
        {
            if (_spawnedSpheres.Count == 0) return;
            GameObject last = _spawnedSpheres[_spawnedSpheres.Count - 1];
            _spawnedSpheres.RemoveAt(_spawnedSpheres.Count - 1);

            // if it's a worm, destroy segments cleanly
            var worm = last != null ? last.GetComponent<SynthWorm>() : null;
            if (worm != null) worm.DestroyWorm();
            else if (last != null) Destroy(last);
        }

        public void ClearAllSpheres()
        {
            foreach (var obj in _spawnedSpheres)
            {
                if (obj == null) continue;
                var worm = obj.GetComponent<SynthWorm>();
                if (worm != null) worm.DestroyWorm();
                else Destroy(obj);
            }
            _spawnedSpheres.Clear();
        }
    }
}
