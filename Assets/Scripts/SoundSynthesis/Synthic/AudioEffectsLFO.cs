using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    public class AudioEffectsLFO : MonoBehaviour
    {
        [System.Serializable]
        public class FilterLFOBinding
        {
            public enum TargetFilter
            {
                LowPassCutoff,
                LowPassResonance,
                HighPassCutoff,
                HighPassResonance,
                ReverbDryLevel,
                ReverbRoom,
                ReverbRoomHF,
                ReverbDecayTime,
                ReverbReflectionsLevel,
                ReverbReverbLevel,
                DistortionLevel,
                ChorusDepth,
                ChorusRate,
                ChorusDryMix,
                ChorusWetMix1,
                ChorusWetMix2,
                ChorusWetMix3,
                EchoDelay,
                EchoDecayRatio,
                EchoDryMix,
                EchoWetMix,
            }

            public TargetFilter target;
            [Range(0f, 1f)] public float minValue = 0f;
            [Range(0f, 1f)] public float maxValue = 1f;
            public List<LFO> lfos = new();
        }

        [SerializeField] private List<FilterLFOBinding> bindings = new();

        // cache all filter components
        private AudioLowPassFilter   _lowPass;
        private AudioHighPassFilter  _highPass;
        private AudioReverbFilter    _reverb;
        private AudioDistortionFilter _distortion;
        private AudioChorusFilter    _chorus;
        private AudioEchoFilter      _echo;

        private void Awake()
        {
            _lowPass    = GetComponent<AudioLowPassFilter>();
            _highPass   = GetComponent<AudioHighPassFilter>();
            _reverb     = GetComponent<AudioReverbFilter>();
            _distortion = GetComponent<AudioDistortionFilter>();
            _chorus     = GetComponent<AudioChorusFilter>();
            _echo       = GetComponent<AudioEchoFilter>();
        }

        private void Update()
        {
            foreach (var binding in bindings)
            {
                float mod   = 0f;
                int   count = 0;

                foreach (var lfo in binding.lfos)
                {
                    mod += lfo.Update(Time.deltaTime);
                    count++;
                }

                if (count == 0) continue;

                // average LFOs and map to target range
                float normalized = mod / count;
                float value      = Mathf.Lerp(binding.minValue, binding.maxValue, normalized);

                ApplyToFilter(binding.target, value);
            }
        }

        private void ApplyToFilter(FilterLFOBinding.TargetFilter target, float normalizedValue)
        {
            switch (target)
            {
                // low pass
                case FilterLFOBinding.TargetFilter.LowPassCutoff:
                    if (_lowPass != null)
                        _lowPass.cutoffFrequency = Mathf.Lerp(10f, 22000f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.LowPassResonance:
                    if (_lowPass != null)
                        _lowPass.lowpassResonanceQ = Mathf.Lerp(1f, 10f, normalizedValue);
                    break;

                // high pass
                case FilterLFOBinding.TargetFilter.HighPassCutoff:
                    if (_highPass != null)
                        _highPass.cutoffFrequency = Mathf.Lerp(10f, 22000f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.HighPassResonance:
                    if (_highPass != null)
                        _highPass.highpassResonanceQ = Mathf.Lerp(1f, 10f, normalizedValue);
                    break;

                // reverb
                case FilterLFOBinding.TargetFilter.ReverbDryLevel:
                    if (_reverb != null)
                        _reverb.dryLevel = Mathf.Lerp(-10000f, 0f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.ReverbRoom:
                    if (_reverb != null)
                        _reverb.room = Mathf.Lerp(-10000f, 0f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.ReverbRoomHF:
                    if (_reverb != null)
                        _reverb.roomHF = Mathf.Lerp(-10000f, 0f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.ReverbDecayTime:
                    if (_reverb != null)
                        _reverb.decayTime = Mathf.Lerp(0.1f, 20f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.ReverbReflectionsLevel:
                    if (_reverb != null)
                        _reverb.reflectionsLevel = Mathf.Lerp(-10000f, 1000f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.ReverbReverbLevel:
                    if (_reverb != null)
                        _reverb.reverbLevel = Mathf.Lerp(-10000f, 2000f, normalizedValue);
                    break;

                // distortion
                case FilterLFOBinding.TargetFilter.DistortionLevel:
                    if (_distortion != null)
                        _distortion.distortionLevel = normalizedValue;
                    break;

                // chorus
                case FilterLFOBinding.TargetFilter.ChorusDepth:
                    if (_chorus != null)
                        _chorus.depth = normalizedValue;
                    break;
                case FilterLFOBinding.TargetFilter.ChorusRate:
                    if (_chorus != null)
                        _chorus.rate = Mathf.Lerp(0f, 20f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.ChorusDryMix:
                    if (_chorus != null)
                        _chorus.dryMix = normalizedValue;
                    break;
                case FilterLFOBinding.TargetFilter.ChorusWetMix1:
                    if (_chorus != null)
                        _chorus.wetMix1 = normalizedValue;
                    break;
                case FilterLFOBinding.TargetFilter.ChorusWetMix2:
                    if (_chorus != null)
                        _chorus.wetMix2 = normalizedValue;
                    break;
                case FilterLFOBinding.TargetFilter.ChorusWetMix3:
                    if (_chorus != null)
                        _chorus.wetMix3 = normalizedValue;
                    break;

                // echo
                case FilterLFOBinding.TargetFilter.EchoDelay:
                    if (_echo != null)
                        _echo.delay = Mathf.Lerp(10f, 5000f, normalizedValue);
                    break;
                case FilterLFOBinding.TargetFilter.EchoDecayRatio:
                    if (_echo != null)
                        _echo.decayRatio = normalizedValue;
                    break;
                case FilterLFOBinding.TargetFilter.EchoDryMix:
                    if (_echo != null)
                        _echo.dryMix = normalizedValue;
                    break;
                case FilterLFOBinding.TargetFilter.EchoWetMix:
                    if (_echo != null)
                        _echo.wetMix = normalizedValue;
                    break;
            }
        }
    }
}