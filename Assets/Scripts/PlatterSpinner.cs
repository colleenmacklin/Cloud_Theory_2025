using UnityEngine;

namespace Synthic
{
    public class PlatterSpinner : MonoBehaviour
    {
        [Header("BPM Settings")]
        [SerializeField, Range(60f, 180f)] private float bpm = 120f;
        [SerializeField] private int beatsPerRevolution = 4;

        [Header("Rotation")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private bool clockwise = true;

        [Header("Sync")]
[SerializeField] private float interactionRange = 15f;

        private bool  _isPlaying        = false;
        private bool  _waitingForSync   = false;
        private float _savedRotation    = 0f;

        public float BPM             => bpm;
        public bool  IsPlaying       => _isPlaying;
        public bool  WaitingForSync  => _waitingForSync;
        public float DegreesPerSecond =>
            360f / (beatsPerRevolution * (60f / bpm));

        public float InteractionRange => interactionRange;
public void SetInRange(bool inRange)
{
    // optional - add visual feedback on the platter itself if needed
}

public void SetBPM(float newBpm)
{
    bpm = Mathf.Clamp(newBpm, 60f, 180f);
}
private void Start()
{
    if (RhythmicMasterClock.Instance != null)
        RhythmicMasterClock.Instance.RegisterPlatter(this);
    else
        Debug.LogWarning($"PlatterSpinner {gameObject.name}: RhythmicMasterClock instance not found");
}
        private void OnDestroy()
        {
            if (RhythmicMasterClock.Instance != null)
                RhythmicMasterClock.Instance.UnregisterPlatter(this);
        }

        private void Update()
        {
            if (!_isPlaying) return;

            float direction       = clockwise ? -1f : 1f;
            float degreesPerFrame = DegreesPerSecond * Time.deltaTime * direction;
            transform.Rotate(rotationAxis, degreesPerFrame, Space.Self);
        }

        public void Toggle()
        {
            if (_isPlaying)
                Stop();
            else
                QueueStart();
        }

        public void QueueStart()
        {
            if (_isPlaying || _waitingForSync) return;
            _waitingForSync = true;
            // master clock will call StartNow() on the next quarter note
        }

        public void StartNow()
        {
            _waitingForSync = false;
            _isPlaying      = true;

            // snap rotation to the nearest 16th note position
            // so objects on the platter align to the grid
            SnapRotationToGrid();
        }

        public void Stop()
        {
            _isPlaying      = false;
            _waitingForSync = false;

            // save current rotation so we know where we stopped
            _savedRotation = transform.localEulerAngles.y;
        }

        private void SnapRotationToGrid()
        {
            // snap Y rotation to nearest 22.5 degrees (16th note position)
            float currentY  = transform.localEulerAngles.y;
            float snapped   = Mathf.Round(currentY / 22.5f) * 22.5f;
            Vector3 euler   = transform.localEulerAngles;
            euler.y         = snapped;
            transform.localEulerAngles = euler;
        }

        public float RevolutionProgress =>
            (transform.localEulerAngles.y % 360f) / 360f;

        public int Current16thStep =>
            Mathf.FloorToInt(RevolutionProgress * 16f) % 16;
    }
}