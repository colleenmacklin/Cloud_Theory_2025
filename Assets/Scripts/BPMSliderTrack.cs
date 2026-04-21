using UnityEngine;
using TMPro;

namespace Synthic
{
    public class BPMSliderTrack : MonoBehaviour
    {
        [SerializeField] private BPMSlider    slider;
        [SerializeField] private TextMeshPro  bpmLabel; // 3D text showing current BPM
        [SerializeField] private Transform[]  tickMarks; // optional tick mark transforms

private void Start()
{
    if (slider != null && bpmLabel != null)
        bpmLabel.text = $"{Mathf.RoundToInt(slider.CurrentBPM)} BPM";
}

private void Update()
{
    if (slider == null)
    {
        //Debug.Log("BPMSliderTrack: slider is null");
        return;
    }

    if (bpmLabel == null)
    {
        //Debug.Log("BPMSliderTrack: bpmLabel is null");
        return;
    }

    //Debug.Log($"BPM: {slider.CurrentBPM}");
    bpmLabel.text = $"{Mathf.RoundToInt(slider.CurrentBPM)} BPM";
}
    }
}