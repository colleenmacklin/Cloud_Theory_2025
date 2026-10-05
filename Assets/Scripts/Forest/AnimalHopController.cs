using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        [Tooltip("How far each flee-hop moves the animal away from the player.")]
        public float fleeHopDistance = 2.5f;
        [Tooltip("Player must close the distance by at least this much (since the last check) to count as 'approaching' and trigger a flee-hop.")]
        public float approachEpsilon = 0.05f;

        [Header("Reward (hop closer)")]
        [Tooltip("How far the player may drift between bars and still count as 'staying still'.")]
        public float stillnessMoveThreshold = 0.15f;
        [Tooltip("How far each reward hop moves the animal toward the player.")]
        public float rewardHopDistance = 1.5f;

        [Header("Following")]
        [Tooltip("Once within this distance the animal stops being wary and starts following.")]
        public float followDistance = 3f;
        [Tooltip("Extra slack before a follow-hop triggers, so it doesn't hop on every tiny player movement.")]
        public float followSlack = 0.5f;

        [Header("Retreat")]
        public int retreatHopCount = 3;
        public float retreatHopDistance = 3f;
        [Tooltip("Deactivates the GameObject this long after the last retreat hop finishes.")]
        public float retreatSettleDelay = 0.5f;

        [Header("Hop Motion")]
        public float hopDuration = 0.35f;
        public float hopHeight = 0.6f;
        [SerializeField] private AnimationCurve hopArc = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

        public AnimalMood Mood { get; private set; } = AnimalMood.Wary;

        private static readonly List<AnimalHopController> _active = new();

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

            float distance = Vector3.Distance(transform.position, player.position);

            switch (Mood)
            {
                case AnimalMood.Wary:
                    UpdateWary(distance);
                    break;
                case AnimalMood.Following:
                    UpdateFollowing(distance);
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
                StartCoroutine(FleeToRetreatPoint());
                _lastPlayerDistance = distance;
            }
        }

        // Fires the first time the player is spotted (measurably closer, within
        // fleeTriggerRadius) while Wary — covers the ground to retreatPoint in one go,
        // in the same bouncing multi-hop style as RetreatSequence(), rather than the
        // single small fleeHopDistance step used elsewhere. Stays Wary throughout (no
        // mood change, no deactivation) so the stillness/follow mechanics still apply
        // once the animal has settled at the retreat point. Falls back to a single
        // fleeHopDistance hop directly away from the player when no retreatPoint is set.
        private IEnumerator FleeToRetreatPoint()
        {
            if (retreatPoint == null)
            {
                Vector3 away = transform.position - player.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = -transform.forward; // player is right on top of us; pick any direction
                Vector3 target = transform.position + away.normalized * fleeHopDistance;

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

                Vector3 target = transform.position + toPoint.normalized * Mathf.Min(retreatHopDistance, remaining);

                sequencer.PlayNextForward();
                yield return HopTo(target);
            }
        }

        private void UpdateFollowing(float distance)
        {
            if (distance > followDistance + followSlack && sequencer.IsReady)
            {
                Vector3 toward = (player.position - transform.position);
                toward.y = 0f;
                if (toward.sqrMagnitude < 0.0001f) return;

                float step = Mathf.Min(rewardHopDistance, distance - followDistance);
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
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);

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
                    Vector3 toward = (player.position - transform.position);
                    toward.y = 0f;
                    if (toward.sqrMagnitude > 0.0001f)
                    {
                        float step = Mathf.Min(rewardHopDistance, distanceToPlayer - followDistance);
                        if (step > 0f)
                        {
                            Vector3 target = transform.position + toward.normalized * step;
                            sequencer.PlayNextReverse();
                            StartCoroutine(HopTo(target, afterHop: OnRewardHopLanded));
                        }
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
            if (Vector3.Distance(transform.position, player.position) <= followDistance + followSlack)
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
                Vector3 target = transform.position + direction.normalized * retreatHopDistance;

                yield return HopTo(target);
            }

            yield return new WaitForSeconds(retreatSettleDelay);
            gameObject.SetActive(false);
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

            float elapsed = 0f;
            while (elapsed < hopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hopDuration);
                Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
                pos.y += hopArc.Evaluate(t) * hopHeight;
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
        // scene, and it can resolve to a tile that doesn't cover this XZ position at all —
        // sampling the wrong tile's height, which is what made hops land mid-air. Find the
        // tile whose bounds actually contain pos instead.
        private static Terrain TerrainAt(Vector3 pos)
        {
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
