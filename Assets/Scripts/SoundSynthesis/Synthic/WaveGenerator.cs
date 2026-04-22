using Synthic.Native.Buffers;
using Synthic.Native.Data;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Synthic
{
    [BurstCompile]
    public class WaveGenerator : SynthProvider
    {
        public enum WaveformType { Sine, Saw, Square, Triangle }

        public float Frequency
        {
            get => frequency;
            set => frequency = Mathf.Clamp(value, 16.35f, 7902.13f);
        }

        public float Amplitude
        {
            get => amplitude;
            set => amplitude = Mathf.Clamp01(value);
        }

        [SerializeField] private WaveformType waveform = WaveformType.Sine;
        [SerializeField, Range(0, 1)] private float amplitude = 0.5f;
        [SerializeField, Range(16.35f, 7902.13f)] private float frequency = 261.62f;
        [SerializeField] private List<LFO> lfos = new();

        private delegate float BurstWaveDelegate(
            ref SynthBuffer buffer,
            float phase,
            float freqLfoPhase,
            float ampLfoPhase,
            int sampleRate,
            float amplitude,
            float frequency,
            float freqLfoRate,
            float freqLfoDepth,
            float freqLfoBaseValue,
            int freqLfoWaveform,
            float ampLfoRate,
            float ampLfoDepth,
            float ampLfoBaseValue,
            int ampLfoWaveform,
            out float newFreqLfoPhase,
            out float newAmpLfoPhase);

        private static BurstWaveDelegate _burstSine;
        private static BurstWaveDelegate _burstSaw;
        private static BurstWaveDelegate _burstSquare;
        private static BurstWaveDelegate _burstTriangle;

        private BurstWaveDelegate _activeWave;
        private WaveformType _currentWaveform;

        private float _phase;
        private float _freqLfoPhase;
        private float _ampLfoPhase;
        private int _sampleRate;

        private float _freqLfoRate;
        private float _freqLfoDepth;
        private float _freqLfoBase;
        private int _freqLfoWaveform;

        private float _ampLfoRate;
        private float _ampLfoDepth;
        private float _ampLfoBase;
        private int _ampLfoWaveform;

        private void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            _burstSine     ??= BurstCompiler.CompileFunctionPointer<BurstWaveDelegate>(BurstSine).Invoke;
            _burstSaw      ??= BurstCompiler.CompileFunctionPointer<BurstWaveDelegate>(BurstSaw).Invoke;
            _burstSquare   ??= BurstCompiler.CompileFunctionPointer<BurstWaveDelegate>(BurstSquare).Invoke;
            _burstTriangle ??= BurstCompiler.CompileFunctionPointer<BurstWaveDelegate>(BurstTriangle).Invoke;
            SetWaveform(waveform);
        }

        private void Update()
        {
            if (waveform != _currentWaveform)
                SetWaveform(waveform);

            // frequency LFO
            var freqLFO = lfos.Find(l => l.lfoTarget == LFO.LFOTarget.Frequency);
            if (freqLFO != null)
            {
                _freqLfoRate     = freqLFO.rate;
                _freqLfoDepth    = freqLFO.depth;
                _freqLfoBase     = freqLFO.baseValue;
                _freqLfoWaveform = (int)freqLFO.waveform;
            }
            else
            {
                _freqLfoRate     = 0f;
                _freqLfoDepth    = 0f;
                _freqLfoBase     = frequency / 7902.13f;
                _freqLfoWaveform = 0;
            }

            // amplitude LFO
            var ampLFO = lfos.Find(l => l.lfoTarget == LFO.LFOTarget.Amplitude);
            if (ampLFO != null)
            {
                _ampLfoRate     = ampLFO.rate;
                _ampLfoDepth    = ampLFO.depth;
                _ampLfoBase     = ampLFO.baseValue;
                _ampLfoWaveform = (int)ampLFO.waveform;
            }
            else
            {
                _ampLfoRate     = 0f;
                _ampLfoDepth    = 0f;
                _ampLfoBase     = amplitude;
                _ampLfoWaveform = 0;
            }
        }

        private void SetWaveform(WaveformType type)
        {
            _currentWaveform = type;
            _activeWave = type switch
            {
                WaveformType.Sine     => _burstSine,
                WaveformType.Saw      => _burstSaw,
                WaveformType.Square   => _burstSquare,
                WaveformType.Triangle => _burstTriangle,
                _                     => _burstSine
            };
        }

        protected override void ProcessBuffer(ref SynthBuffer buffer)
        {
            _phase = _activeWave(
                ref buffer,
                _phase,
                _freqLfoPhase,
                _ampLfoPhase,
                _sampleRate,
                amplitude,
                frequency,
                _freqLfoRate,
                _freqLfoDepth,
                _freqLfoBase,
                _freqLfoWaveform,
                _ampLfoRate,
                _ampLfoDepth,
                _ampLfoBase,
                _ampLfoWaveform,
                out _freqLfoPhase,
                out _ampLfoPhase);
        }

        private static float ComputeLfo(float lfoPhase, int lfoWaveform)
        {
            return lfoWaveform switch
            {
                0 => (float)math.sin(lfoPhase * 2f * math.PI_DBL),
                1 => lfoPhase * 2f - 1f,
                2 => lfoPhase < 0.5f ? 1f : -1f,
                3 => lfoPhase < 0.5f ? (lfoPhase * 4f - 1f) : (3f - lfoPhase * 4f),
                _ => 0f
            };
        }

        [BurstCompile]
        private static float BurstSine(
            ref SynthBuffer buffer,
            float phase,
            float freqLfoPhase,
            float ampLfoPhase,
            int sampleRate,
            float amplitude,
            float frequency,
            float freqLfoRate,
            float freqLfoDepth,
            float freqLfoBaseValue,
            int freqLfoWaveform,
            float ampLfoRate,
            float ampLfoDepth,
            float ampLfoBaseValue,
            int ampLfoWaveform,
            out float newFreqLfoPhase,
            out float newAmpLfoPhase)
        {
            float freqLfoIncrement = freqLfoRate / sampleRate;
            float ampLfoIncrement  = ampLfoRate  / sampleRate;

            for (int sample = 0; sample < buffer.Length; sample++)
            {
                float freqLfoValue = ComputeLfo(freqLfoPhase, freqLfoWaveform);
                float modulatedFreq = math.clamp(
                    (freqLfoBaseValue + freqLfoValue * freqLfoDepth * freqLfoBaseValue) * 7902.13f,
                    16.35f, 7902.13f);

                float ampLfoValue = ComputeLfo(ampLfoPhase, ampLfoWaveform);
                float modulatedAmp = math.clamp(
                    ampLfoBaseValue + ampLfoValue * ampLfoDepth * ampLfoBaseValue,
                    0f, 1f);

                float phaseIncrement = modulatedFreq / sampleRate;
                float value = (float)math.sin(phase * 2f * math.PI_DBL) * modulatedAmp;
                buffer[sample] = new StereoData(value);

                phase        = (phase        + phaseIncrement)    % 1f;
                freqLfoPhase = (freqLfoPhase + freqLfoIncrement)  % 1f;
                ampLfoPhase  = (ampLfoPhase  + ampLfoIncrement)   % 1f;
            }

            newFreqLfoPhase = freqLfoPhase;
            newAmpLfoPhase  = ampLfoPhase;
            return phase;
        }

        [BurstCompile]
        private static float BurstSaw(
            ref SynthBuffer buffer,
            float phase,
            float freqLfoPhase,
            float ampLfoPhase,
            int sampleRate,
            float amplitude,
            float frequency,
            float freqLfoRate,
            float freqLfoDepth,
            float freqLfoBaseValue,
            int freqLfoWaveform,
            float ampLfoRate,
            float ampLfoDepth,
            float ampLfoBaseValue,
            int ampLfoWaveform,
            out float newFreqLfoPhase,
            out float newAmpLfoPhase)
        {
            float freqLfoIncrement = freqLfoRate / sampleRate;
            float ampLfoIncrement  = ampLfoRate  / sampleRate;

            for (int sample = 0; sample < buffer.Length; sample++)
            {
                float freqLfoValue = ComputeLfo(freqLfoPhase, freqLfoWaveform);
                float modulatedFreq = math.clamp(
                    (freqLfoBaseValue + freqLfoValue * freqLfoDepth * freqLfoBaseValue) * 7902.13f,
                    16.35f, 7902.13f);

                float ampLfoValue = ComputeLfo(ampLfoPhase, ampLfoWaveform);
                float modulatedAmp = math.clamp(
                    ampLfoBaseValue + ampLfoValue * ampLfoDepth * ampLfoBaseValue,
                    0f, 1f);

                float phaseIncrement = modulatedFreq / sampleRate;
                float value = (phase * 2f - 1f) * modulatedAmp;
                buffer[sample] = new StereoData(value);

                phase        = (phase        + phaseIncrement)   % 1f;
                freqLfoPhase = (freqLfoPhase + freqLfoIncrement) % 1f;
                ampLfoPhase  = (ampLfoPhase  + ampLfoIncrement)  % 1f;
            }

            newFreqLfoPhase = freqLfoPhase;
            newAmpLfoPhase  = ampLfoPhase;
            return phase;
        }

        [BurstCompile]
        private static float BurstSquare(
            ref SynthBuffer buffer,
            float phase,
            float freqLfoPhase,
            float ampLfoPhase,
            int sampleRate,
            float amplitude,
            float frequency,
            float freqLfoRate,
            float freqLfoDepth,
            float freqLfoBaseValue,
            int freqLfoWaveform,
            float ampLfoRate,
            float ampLfoDepth,
            float ampLfoBaseValue,
            int ampLfoWaveform,
            out float newFreqLfoPhase,
            out float newAmpLfoPhase)
        {
            float freqLfoIncrement = freqLfoRate / sampleRate;
            float ampLfoIncrement  = ampLfoRate  / sampleRate;

            for (int sample = 0; sample < buffer.Length; sample++)
            {
                float freqLfoValue = ComputeLfo(freqLfoPhase, freqLfoWaveform);
                float modulatedFreq = math.clamp(
                    (freqLfoBaseValue + freqLfoValue * freqLfoDepth * freqLfoBaseValue) * 7902.13f,
                    16.35f, 7902.13f);

                float ampLfoValue = ComputeLfo(ampLfoPhase, ampLfoWaveform);
                float modulatedAmp = math.clamp(
                    ampLfoBaseValue + ampLfoValue * ampLfoDepth * ampLfoBaseValue,
                    0f, 1f);

                float phaseIncrement = modulatedFreq / sampleRate;
                float value = (phase < 0.5f ? 1f : -1f) * modulatedAmp;
                buffer[sample] = new StereoData(value);

                phase        = (phase        + phaseIncrement)   % 1f;
                freqLfoPhase = (freqLfoPhase + freqLfoIncrement) % 1f;
                ampLfoPhase  = (ampLfoPhase  + ampLfoIncrement)  % 1f;
            }

            newFreqLfoPhase = freqLfoPhase;
            newAmpLfoPhase  = ampLfoPhase;
            return phase;
        }

        [BurstCompile]
        private static float BurstTriangle(
            ref SynthBuffer buffer,
            float phase,
            float freqLfoPhase,
            float ampLfoPhase,
            int sampleRate,
            float amplitude,
            float frequency,
            float freqLfoRate,
            float freqLfoDepth,
            float freqLfoBaseValue,
            int freqLfoWaveform,
            float ampLfoRate,
            float ampLfoDepth,
            float ampLfoBaseValue,
            int ampLfoWaveform,
            out float newFreqLfoPhase,
            out float newAmpLfoPhase)
        {
            float freqLfoIncrement = freqLfoRate / sampleRate;
            float ampLfoIncrement  = ampLfoRate  / sampleRate;

            for (int sample = 0; sample < buffer.Length; sample++)
            {
                float freqLfoValue = ComputeLfo(freqLfoPhase, freqLfoWaveform);
                float modulatedFreq = math.clamp(
                    (freqLfoBaseValue + freqLfoValue * freqLfoDepth * freqLfoBaseValue) * 7902.13f,
                    16.35f, 7902.13f);

                float ampLfoValue = ComputeLfo(ampLfoPhase, ampLfoWaveform);
                float modulatedAmp = math.clamp(
                    ampLfoBaseValue + ampLfoValue * ampLfoDepth * ampLfoBaseValue,
                    0f, 1f);

                float phaseIncrement = modulatedFreq / sampleRate;
                float value = (phase < 0.5f ? (phase * 4f - 1f) : (3f - phase * 4f)) * modulatedAmp;
                buffer[sample] = new StereoData(value);

                phase        = (phase        + phaseIncrement)   % 1f;
                freqLfoPhase = (freqLfoPhase + freqLfoIncrement) % 1f;
                ampLfoPhase  = (ampLfoPhase  + ampLfoIncrement)  % 1f;
            }

            newFreqLfoPhase = freqLfoPhase;
            newAmpLfoPhase  = ampLfoPhase;
            return phase;
        }
    }
}