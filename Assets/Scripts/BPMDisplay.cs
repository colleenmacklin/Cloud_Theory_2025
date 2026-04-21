using UnityEngine;
using TMPro;

namespace Synthic
{
    public class BPMDisplay : MonoBehaviour
    {
        [SerializeField] private RhythmicMasterClock masterClock;
        [SerializeField] private TextMeshPro          bpmLabel;

        private void Update()
        {
            if (masterClock == null || bpmLabel == null) return;
            bpmLabel.text = $"{Mathf.RoundToInt(masterClock.BPM)} BPM";
        }
    }
}