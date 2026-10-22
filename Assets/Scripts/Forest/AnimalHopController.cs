using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Synthic;

namespace Forest
{
    // Drives one path-side animal through the full behavior described for this feature:
    //   - Wary: sits still until the player approaches, then hops away each time the
    //     player gets measurably closer, playing its note sequence forward. If the
    //     player instead stays still for a full measure, the animal hops closer instead,
    //     playing the same sequence backward.
    //   - Following: once close enough, it stops being wary and just keeps pace with the
    //     player, hopping to catch up and playing the sequence forward (looping).
    //   - Retreating: on an external Retreat() call (from a scene trigger), it hops away
    //     from the player and deactivates.
    // Movement is a procedural arc hop (no animation clips required) — see HopTo().
    // Every hop (flee, return, follow, retreat) uses the same hopDistance; the hop's
    // height scales with that distance, and its airtime with the animal's note value
    // (AnimalNoteSequencer.noteValue), so big animals take long, slow, high leaps on
    // whole notes and small ones take quick little 16th-note skips.
    [RequireComponent(typeof(AnimalNoteSequencer))]
    public class AnimalHopController : MonoBehaviour
    {
        public enum AnimalMood { Wary, Following, Retreating }

        [Header("References")]
        [Tooltip("Leave empty to auto-find the GameObject tagged 'Player'.")]
        public Transform player;
        [Tooltip("Leave empty to use the AnimalNoteSequencer on this GameObject.")]
        public AnimalNoteSequencer sequencer;
        [Tooltip("Where a retreat hops toward. Leave empty to just hop directly away from the player.")]
        public Transform retreatPoint;

        [Header("Flee")]
        [Tooltip("Distance within which the animal reacts to the player at all.")]
        public float fleeTriggerRadius = 6f;
        [Tooltip("Player must close the distance by at least this much (since the last check) to count as 'approaching' and trigger a flee-hop.")]
        public float approachEpsilon = 0.05f;

        [Header("Reward (hop closer)")]
        [Tooltip("How far the player may drift between bars and still count as 'staying still'.")]
        public float stillnessMoveThreshold = 0.15f;

        [Header("Following")]
        [Tooltip("Minimum radius of the circle returning animals form around the player. The circle grows beyond this automatically when there are too many animals to fit.")]
        public float followDistance = 3f;
        [Tooltip("Once within this distance of its spot in the circle the animal stops being wary and starts following; also the slack before a follow-hop triggers, so it doesn't hop on every tiny player movement.")]
        public float followSlack = 0.5f;

        [Header("Circle Around Player")]
        [Tooltip("How much of the circle's circumference this animal takes up. 0 = measure it from the model's renderer bounds at Start.")]
        public float circleFootprint = 0f;
        [Tooltip("Extra space left between this animal and its neighbors in the circle.")]
        public float circleGap = 0.5f;

        [Header("Retreat")]
        public int retreatHopCount = 3;
        [Tooltip("Deactivates the GameObject this long after the last retreat hop finishes.")]
        public float retreatSettleDelay = 0.5f;

        [Header("Hop Motion")]
        [Tooltip("How far every hop moves the animal (flee, return, follow and retreat). Set per animal relative to body size: smaller animals take smaller hops.")]
        [FormerlySerializedAs("retreatHopDistance")]
        public float hopDistance = 3f;
        [Tooltip("Hop height as a fraction of hopDistance, so bigger hops arc higher.")]
        public float hopHeightRatio = 0.8f;
        [Tooltip("Fraction of the animal's note value (AnimalNoteSequencer.noteValue) spent in the air. The rest of the note is the pause on the ground before the next hop.")]
        [Range(0.1f, 1f)] public float hopAirFraction = 0.8f;
        [SerializeField] private AnimationCurve hopArc = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

        public AnimalMood Mood { get; private set; } = AnimalMood.Wary;

        private static readonly List<AnimalHopController> _active = new();
        // Animals that have been spotted and are returning to / following the player.
        // Each one owns a fixed slot (an angle around the player) so they spread out in
        // a ring instead of all converging on the same point in front of the player.
        private static readonly List<AnimalHopController> _circle = new();

        private float _slotAngle;

        private bool _isHopping;
        private float _lastPlayerDistance = Mathf.Infinity;
        private Vector3 _playerPosAtLastBar;
        private bool _havePlayerPosAtLastBar;
        // Set the first time the player is spotted (see UpdateWary). The stand-still
        // reward-hop is gated on this rather than distance (see OnBar) so the animal
        // stays put at spawn instead of creeping toward a player who hasn't approached
        // it yet — it only starts reacting once it's actually been startled once.
        private bool _hasBeenSpotted;

        private void OnEnable()
        {
            _active.Add(this);
        }

        private void OnDisable()
        {
            _active.Remove(this);
            LeaveCircle();
            if (RhythmicMasterClock.Instance != null)
                RhythmicMasterClock.Instance.OnBar -= OnBar;
        }

        // Subscribes in Start (not OnEnable) because RhythmicMasterClock.Instance is set
        // in its own Awake, and Unity doesn't guarantee Awake order across objects —
        // Start is guaranteed to run after every object's Awake (same pattern
        // FMODChordPlayer uses for the same reason).
        private void Start()
        {
            if (sequencer == null) sequencer = GetComponent<AnimalNoteSequencer>();
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }

            if (RhythmicMasterClock.Instance != null)
                RhythmicMasterClock.Instance.OnBar += OnBar;

            if (circleFootprint <= 0f)
                circleFootprint = MeasureFootprint();
        }

        // Horizontal size of the model, used as this animal's share of the circle.
        private float MeasureFootprint()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 1f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Max(b.size.x, b.size.z);
        }

        private void JoinCircle()
        {
            if (_circle.Contains(this)) return;
            _circle.Add(this);
            AssignSlots();
        }

        private void LeaveCircle()
        {
            if (_circle.Remove(this)) AssignSlots();
        }

        // Re-deals the slots whenever someone joins or leaves. Members are ordered by
        // their current bearing from the player so each animal gets the slot on its own
        // side and they don't cross paths to reach the ring. Each slot's arc is weighted
        // by footprint, so a bear gets more of the ring than a squirrel.
        private static void AssignSlots()
        {
            if (_circle.Count == 0) return;
            Transform p = _circle[0].player;
            if (p == null) return;

            _circle.Sort((a, b) => Bearing(a, p).CompareTo(Bearing(b, p)));

            float total = 0f;
            foreach (var a in _circle) total += a.circleFootprint + a.circleGap;

            float start = Bearing(_circle[0], p);
            float acc = 0f;
            foreach (var a in _circle)
            {
                float share = (a.circleFootprint + a.circleGap) / total * 360f;
                a._slotAngle = start + acc + share * 0.5f;
                acc += share;
            }
        }

        private static float Bearing(AnimalHopController a, Transform p)
        {
            Vector3 d = a.transform.position - p.position;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // Ring radius is shared by every member: at least the largest followDistance,
        // grown so the total of everyone's footprints fits around the circumference.
        private static float CircleRadius()
        {
            float total = 0f, minRadius = 0f;
            foreach (var a in _circle)
            {
                total += a.circleFootprint + a.circleGap;
                minRadius = Mathf.Max(minRadius, a.followDistance);
            }
            return Mathf.Max(minRadius, total / (2f * Mathf.PI));
        }

        private Vector3 SlotPosition()
        {
            float rad = _slotAngle * Mathf.Deg2Rad;
            return player.position + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * CircleRadius();
        }

        private float FlatDistanceToSlot()
        {
            Vector3 d = SlotPosition() - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // Called by a scene trigger (see AnimalRetreatTrigger) to send every active animal
        // back into the forest at once.
        public static void RetreatAll()
        {
            foreach (var animal in _active)
                if (animal != null)
                    animal.Retreat();
        }

        public void Retreat()
        {
            if (Mood == AnimalMood.Retreating) return;
            SetMood(AnimalMood.Retreating);
            LeaveCircle();
            StopAllCoroutines();
            StartCoroutine(RetreatSequence());
        }

        private void SetMood(AnimalMood newMood)
        {
            if (Mood == newMood) return;
            Debug.Log($"[AnimalHopController] {name}: {Mood} -> {newMood}");
            Mood = newMood;
        }

        private void Update()
        {
            if (player == null || sequencer == null || _isHopping || Mood == AnimalMood.Retreating)
                return;

            switch (Mood)
            {
                case AnimalMood.Wary:
                    UpdateWary(Vector3.Distance(transform.position, player.position));
                    break;
                case AnimalMood.Following:
                    UpdateFollowing();
                    break;
            }
        }

        // Compares distance against a baseline that only rises when the player backs off
        // (or resets right after a flee-hop) rather than every frame, so "measurably
        // closer" accumulates across frames instead of requiring a single-frame delta
        // bigger than approachEpsilon — at high frame rates a normal walk speed moves
        // less than approachEpsilon per frame, which silently never triggered a flee.
        private void UpdateWary(float distance)
        {
            if (distance > _lastPlayerDistance)
                _lastPlayerDistance = distance;

            if (distance <= fleeTriggerRadius
                && distance < _lastPlayerDistance - approachEpsilon
                && sequencer.IsReady)
            {
                _hasBeenSpotted = true;
                JoinCircle();
                StartCoroutine(FleeToRetreatPoint());
                _lastPlayerDistance = distance;
            }
        }

        // Fires the first time the player is spotted (measurably closer, within
        // fleeTriggerRadius) while Wary — covers the ground to retreatPoint in one go,
        // in the same bouncing multi-hop style as RetreatSequence(). Stays Wary
        // throughout (no mood change, no deactivation) so the stillness/follow mechanics
        // still apply once the animal has settled at the retreat point. Falls back to a
        // single hop directly away from the player when no retreatPoint is set.
        private IEnumerator FleeToRetreatPoint()
        {
            if (retreatPoint == null)
            {
                Vector3 away = transform.position - player.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = -transform.forward; // player is right on top of us; pick any direction
                Vector3 target = transform.position + away.normalized * hopDistance;

                sequencer.PlayNextForward();
                yield return HopTo(target);
                yield break;
            }

            while (true)
            {
                Vector3 toPoint = retreatPoint.position - transform.position;
                toPoint.y = 0f;
                float remaining = toPoint.magnitude;
                if (remaining < 0.05f) yield break; // arrived

                Vector3 target = transform.position + toPoint.normalized * Mathf.Min(hopDistance, remaining);

                // Wait out the previous note so the hops land on the animal's rhythm.
                while (!sequencer.IsReady) yield return null;
                sequencer.PlayNextForward();
                yield return HopTo(target);
            }
        }

        // Keeps the animal on its slot in the ring as the player moves.
        private void UpdateFollowing()
        {
            float distance = FlatDistanceToSlot();
            if (distance > followSlack && sequencer.IsReady)
            {
                Vector3 toward = SlotPosition() - transform.position;
                toward.y = 0f;
                if (toward.sqrMagnitude < 0.0001f) return;

                float step = Mathf.Min(hopDistance, distance);
                Vector3 target = transform.position + toward.normalized * step;

                sequencer.PlayNextForward();
                StartCoroutine(HopTo(target));
            }
        }

        // Fires once per bar (measure) from RhythmicMasterClock — the "did the player stay
        // still for a full measure" check that rewards the animal hopping closer.
        private void OnBar()
        {
            if (player == null || Mood != AnimalMood.Wary)
            {
                if (player != null) { _playerPosAtLastBar = player.position; _havePlayerPosAtLastBar = true; }
                return;
            }

            if (_havePlayerPosAtLastBar)
            {
                float moved = Vector3.Distance(player.position, _playerPosAtLastBar);

                // No distance gate here on purpose: FleeToRetreatPoint() can send the
                // animal far outside fleeTriggerRadius (all the way to retreatPoint), and
                // "stand still to lure it back" needs to work no matter how far away that
                // left it — otherwise it can strand itself out of reward range entirely.
                // Gated on _hasBeenSpotted instead, so the animal stays put at spawn
                // until the player has actually approached it once.
                if (_hasBeenSpotted
                    && moved <= stillnessMoveThreshold
                    && !_isHopping
                    && sequencer.IsReady)
                {
                    // Hops toward its own slot in the ring rather than straight at the
                    // player, so returning animals spread out instead of piling up.
                    Vector3 toward = SlotPosition() - transform.position;
                    toward.y = 0f;
                    if (toward.sqrMagnitude > 0.0001f)
                    {
                        float step = Mathf.Min(hopDistance, toward.magnitude);
                        if (step > 0.05f)
                        {
                            Vector3 target = transform.position + toward.normalized * step;
                            sequencer.PlayNextReverse();
                            StartCoroutine(HopTo(target, afterHop: OnRewardHopLanded));
                        }
                        else CheckStartedFollowing(); // already on its slot
                    }
                }
            }

            _playerPosAtLastBar = player.position;
            _havePlayerPosAtLastBar = true;
        }

        // Runs right after a reward-hop (hop closer) lands. Resyncs the flee baseline to
        // the animal's own new position first — otherwise the distance drop caused by the
        // animal's own hop looks identical to "the player got closer" and the very next
        // Update() spuriously fires a flee-hop, undoing the reward.
        private void OnRewardHopLanded()
        {
            if (player != null)
                _lastPlayerDistance = Vector3.Distance(transform.position, player.position);
            CheckStartedFollowing();
        }

        private void CheckStartedFollowing()
        {
            if (player == null) return;
            if (FlatDistanceToSlot() <= followSlack)
                SetMood(AnimalMood.Following);
        }

        private IEnumerator RetreatSequence()
        {
            for (int i = 0; i < retreatHopCount; i++)
            {
                Vector3 direction;
                if (retreatPoint != null)
                {
                    direction = retreatPoint.position - transform.position;
                    direction.y = 0f;
                }
                else
                {
                    direction = transform.position - (player != null ? player.position : transform.position - transform.forward);
                    direction.y = 0f;
                }

                if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
                Vector3 target = transform.position + direction.normalized * hopDistance;

                yield return HopTo(target);
            }

            yield return new WaitForSeconds(retreatSettleDelay);
            gameObject.SetActive(false);
        }

        // Hop airtime in seconds: a fraction of the animal's note value at the master
        // clock's current tempo. Read fresh per-hop (not cached) so a tempo change
        // mid-scene takes effect on the very next hop.
        private float CurrentHopSeconds()
        {
            return sequencer.NoteSeconds * hopAirFraction;
        }

        private IEnumerator HopTo(Vector3 targetPos, System.Action afterHop = null)
        {
            _isHopping = true;
            targetPos = SnapToGround(targetPos);

            Vector3 startPos = transform.position;
            Vector3 flatDir = targetPos - startPos;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);

            float duration = CurrentHopSeconds();
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
                pos.y += hopArc.Evaluate(t) * hopDistance * hopHeightRatio;
                transform.position = pos;
                yield return null;
            }

            transform.position = targetPos;
            _isHopping = false;
            afterHop?.Invoke();
        }

        private static Vector3 SnapToGround(Vector3 pos)
        {
            Terrain terrain = TerrainAt(pos);
            if (terrain != null)
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            return pos;
        }

        // Terrain.activeTerrain is unreliable here: this project bundles multiple Terrain
        // tiles (Cloud/Rave/Forest) in one shared prefab, all active at once in the Forest
        // scene. Picking a tile by bounds alone isn't enough either — these tiles are only
        // 1000 units apart but each is wider than that, so their bounds genuinely overlap,
        // and a bounds check can match more than one tile for the same XZ position. That's
        // what made hops land mid-air/underground: it was silently sampling CloudTerrain's
        // or RaveTerrain's height instead of the ground actually under the animal. This
        // controller only ever runs in the Forest scene, so go straight for the tile named
        // "ForestTerrain" instead of guessing; fall back to the old bounds heuristic only if
        // that tile can't be found (e.g. it gets renamed).
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
}
