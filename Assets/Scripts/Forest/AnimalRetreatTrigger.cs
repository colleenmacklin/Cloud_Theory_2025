using UnityEngine;

namespace Forest
{
    // Attach to a trigger collider at the point along the path where the animals should
    // give up following and hop back into the forest. Tag the player GameObject as
    // "Player" (same convention as ForestPathTrigger) so this can identify it.
    public class AnimalRetreatTrigger : MonoBehaviour
    {
        private bool _triggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;
            _triggered = true;
            AnimalHopController.RetreatAll();
        }
    }
}
