using UnityEngine;

namespace Synthic
{
    public class StylusDetector : MonoBehaviour
    {
        [SerializeField] private float triggerCooldown = 0.05f;

        private float _lastTriggerTime = -999f;

        private void OnTriggerEnter(Collider other)
        {
            // cooldown to prevent double triggers
            if (Time.time - _lastTriggerTime < triggerCooldown) return;
            _lastTriggerTime = Time.time;

            // look for a PlatterObject on the colliding object
            var platterObject = other.GetComponent<PlatterObject>();
            if (platterObject == null) return;

            platterObject.Trigger();
        }
    }
}