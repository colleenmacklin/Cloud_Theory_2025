using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    public class AudioFilterLFO : MonoBehaviour
    {
        public enum FilterType { LowPass, HighPass }

        [SerializeField] private FilterType filterType = FilterType.LowPass;
        [SerializeField] private List<LFO> lfos = new();

        // cutoff range — Unity's filters go from 10Hz to 22000Hz
        [SerializeField, Range(10f, 22000f)] private float minCutoff = 200f;
        [SerializeField, Range(10f, 22000f)] private float maxCutoff = 8000f;

        private AudioLowPassFilter  _lowPass;
        private AudioHighPassFilter _highPass;

        private void Awake()
        {
            _lowPass  = GetComponent<AudioLowPassFilter>();
            _highPass = GetComponent<AudioHighPassFilter>();
        }

        private void Update()
        {
            float mod = 0f;
            int count = 0;

            foreach (var lfo in lfos)
            {
                mod += lfo.Update(Time.deltaTime);
                count++;
            }

            if (count == 0) return;

            // mod is 0-1, map to cutoff range
            float normalizedMod = (mod / count);
            float cutoff = Mathf.Lerp(minCutoff, maxCutoff, normalizedMod);

            switch (filterType)
            {
                case FilterType.LowPass:
                    if (_lowPass != null)
                        _lowPass.cutoffFrequency = cutoff;
                    break;
                case FilterType.HighPass:
                    if (_highPass != null)
                        _highPass.cutoffFrequency = cutoff;
                    break;
            }
        }
    }
}