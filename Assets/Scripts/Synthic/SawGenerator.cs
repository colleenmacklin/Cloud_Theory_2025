using Synthic.Native.Buffers;
using Synthic.Native.Data;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Synthic{

[BurstCompile]

public class SawGenerator : SynthProvider
{
    [SerializeField, Range(0, 1)] private float amplitude = 0.5f;
    [SerializeField, Range(16.35f, 7902.13f)] private float frequency = 261.62f; // middle C
    private static BurstSawDelegate _burstSaw;

    private float _phase;
    private int _sampleRate;

    private void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        _burstSaw ??= BurstCompiler.CompileFunctionPointer<BurstSawDelegate>(BurstSaw).Invoke;
    }
    protected override void ProcessBuffer(ref SynthBuffer buffer)
    {
        _phase = _burstSaw(ref buffer, _phase, _sampleRate, amplitude, frequency);
    }

    private delegate float BurstSawDelegate(ref SynthBuffer buffer, float phase, int sampleRate, float amplitude, float frequency);

    [BurstCompile]

    private static float BurstSaw(ref SynthBuffer buffer, float phase, int sampleRate, float amplitude, float frequency)
    {
        // calculate how much the phase should change after each sample
        float phaseIncrement = frequency / sampleRate;

    for (int sample = 0; sample < buffer.Length; sample++)
        {
        float saw = (phase * 2f - 1f) * amplitude;
        buffer[sample] = new StereoData(saw);
        phase = (phase + phaseIncrement) % 1f;
        }

        // return the updated phase
        return phase;
    }
}
}

