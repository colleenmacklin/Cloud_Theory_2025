using UnityEngine;

namespace Synthic
{
    public class RhythmicBounceSphere : MonoBehaviour
    {
[Header("Rhythm")]
[SerializeField] private float bpm = 120f;
[SerializeField, Range(1, 16)] private int beatsPerCycle = 4; // in QUARTER notes
[SerializeField, Range(0, 15)] private int beatOffset    = 0; // in QUARTER notes


        [Header("Physics")]
        [SerializeField] private float groundHeight  = 0f;
        [SerializeField, Range(0f, 1f)] private float bounciness = 0.8f;
        [SerializeField] private float maxDropHeight = 10f;

        [Header("Collision Override")]
        [SerializeField] private float externalHitRecoveryTime = 2f;

        private Rigidbody _rb;
        private float _cycleDuration;
        private float _timer;
        private float _dropHeight;
        private float _lastExternalHitTime = -999f;
        private bool  _wasExternallyHit    = false;
        private bool  _hasBouncedThisCycle = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // set physics material programmatically
            var col = GetComponent<Collider>();
            if (col != null)
            {
                var mat             = new PhysicsMaterial("RhythmBounce");
                mat.bounciness      = bounciness;
                mat.dynamicFriction = 0f;
                mat.staticFriction  = 0f;
                mat.bounceCombine   = PhysicsMaterialCombine.Maximum;
                mat.frictionCombine = PhysicsMaterialCombine.Minimum;
                col.material        = mat;
            }

            RecalculatePhysics();

            // apply beat offset - start timer so this sphere hits on its assigned beat
            float offsetFraction = beatOffset / (float)beatsPerCycle;
            _timer = offsetFraction * _cycleDuration;

            // place sphere at calculated drop height
            Vector3 pos = transform.position;
            pos.y = groundHeight + _dropHeight;
            transform.position = pos;

            _rb.linearVelocity = Vector3.zero;
        }

private float _lastBpm;
private int   _lastBeatsPerCycle;
private float _lastBounciness;

private void RecalculatePhysics()
{
    if (Mathf.Approximately(_lastBpm, bpm) &&
        _lastBeatsPerCycle == beatsPerCycle &&
        Mathf.Approximately(_lastBounciness, bounciness))
        return;

    _lastBpm           = bpm;
    _lastBeatsPerCycle = beatsPerCycle;
    _lastBounciness    = bounciness;

    float g = Mathf.Abs(Physics.gravity.y);

    // quarter note duration - what musicians mean by "beat"
    float quarterNoteDuration = 60f / bpm;
    _cycleDuration = quarterNoteDuration * beatsPerCycle;

    float tFall = _cycleDuration / (1f + bounciness);
    _dropHeight  = Mathf.Min((g / 2f) * tFall * tFall, maxDropHeight);

    Vector3 pos = transform.position;
    pos.y = groundHeight + _dropHeight;
    transform.position = pos;

    if (_rb != null)
        _rb.linearVelocity = Vector3.zero;

    Debug.Log($"{gameObject.name}: cycleDuration={_cycleDuration:F3}s dropHeight={_dropHeight:F3}m");
}


private void Update()
{
    RecalculatePhysics();

    if (_wasExternallyHit)
    {
        if (Time.time - _lastExternalHitTime > externalHitRecoveryTime)
        {
            _wasExternallyHit    = false;
            _hasBouncedThisCycle = false;
        }
        else
            return;
    }

    // use master clock timer if available, otherwise use local timer
    float masterTimer = RhythmicMasterClock.Instance != null
        ? RhythmicMasterClock.Instance.MasterTimer
        : _timer;

    _timer += Time.deltaTime;

    if (_timer >= _cycleDuration)
    {
        _timer -= _cycleDuration;
        _hasBouncedThisCycle = false;
    }
}

private void OnCollisionEnter(Collision collision)
{
    // detect external hits from non-rhythmic rigidbodies
    if (collision.gameObject.GetComponent<Rigidbody>() != null
        && collision.gameObject.GetComponent<RhythmicBounceSphere>() == null)
    {
        _wasExternallyHit    = true;
        _lastExternalHitTime = Time.time;
        return;
    }

    // only resync on ground collisions
    if (_hasBouncedThisCycle || !IsGroundCollision(collision)) return;
    _hasBouncedThisCycle = true;

    // correct bounce velocity at impact so next peak is exactly at drop height
    float g             = Mathf.Abs(Physics.gravity.y);
    float targetPeak    = groundHeight + _dropHeight;
    float currentY      = transform.position.y;
    float heightNeeded  = targetPeak - currentY;

    // v = sqrt(2gh) gives exact upward velocity to reach target height
    float correctedVelocity = Mathf.Sqrt(2f * g * Mathf.Max(0f, heightNeeded));

    Vector3 vel = _rb.linearVelocity;
    vel.y = correctedVelocity * bounciness;
    _rb.linearVelocity = vel;

    // resync timer to master clock at moment of impact
    if (RhythmicMasterClock.Instance != null)
        _timer = RhythmicMasterClock.Instance.MasterTimer % _cycleDuration;
    else
        _timer = 0f;
}
        private bool IsGroundCollision(Collision collision)
        {
            foreach (var contact in collision.contacts)
            {
                if (contact.point.y <= groundHeight + 0.1f)
                    return true;
            }
            return false;
        }

        public void SetBPM(float newBpm)
        {
            bpm = newBpm;
            RecalculatePhysics();
        }

public void Resync(float masterTimer)
{
    float quarterNote    = 60f / bpm;
    float offsetTime     = beatOffset * quarterNote;
    float newTimer       = (masterTimer + offsetTime) % _cycleDuration;

    float drift = Mathf.Abs(newTimer - _timer);
    if (drift > 0.02f)
        _timer = newTimer;
}
        private void OnDrawGizmosSelected()
        {
            float g             = Mathf.Abs(Physics.gravity.y);
            float cycleDuration = (60f / bpm) * beatsPerCycle;
            float tFall         = cycleDuration / (1f + bounciness);
            float height        = Mathf.Min((g / 2f) * tFall * tFall, maxDropHeight);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                new Vector3(transform.position.x, groundHeight + height, transform.position.z),
                0.1f);
            Gizmos.DrawLine(
                new Vector3(transform.position.x, groundHeight,          transform.position.z),
                new Vector3(transform.position.x, groundHeight + height, transform.position.z));
        }
    }
}