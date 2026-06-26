using System.Collections.Generic;
using UnityEngine;

namespace Synthic
{
    public class StylusDetector : MonoBehaviour
    {
        [SerializeField] private float triggerCooldown = 0.05f;

        // cooldown tracked per object so simultaneous hits on different rings both fire
        private readonly Dictionary<PlatterObject, float> _lastTriggerTimes = new();

        private void OnTriggerEnter(Collider other)
        {
            var platterObject = other.GetComponent<PlatterObject>();
            if (platterObject == null) return;

            if (_lastTriggerTimes.TryGetValue(platterObject, out float last) &&
                Time.time - last < triggerCooldown) return;

            _lastTriggerTimes[platterObject] = Time.time;
            platterObject.Trigger();
        }
    }
}
