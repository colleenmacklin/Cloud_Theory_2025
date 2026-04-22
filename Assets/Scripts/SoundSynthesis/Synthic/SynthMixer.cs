using Synthic.Native.Buffers;
using UnityEngine;
using System.Collections.Generic;
using Synthic.Native;

namespace Synthic
{
    public class SynthMixer : SynthProvider
    {
        [System.Serializable]
        public struct MixerChannel
        {
            public SynthProvider provider;
            [Range(0f, 1f)] public float volume;
        }

        [SerializeField] private List<MixerChannel> channels = new();
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        private NativeBox<SynthBuffer> _mixBuffer;

protected override void ProcessBuffer(ref SynthBuffer buffer)
{
    // lazy init to correct size
    if (_mixBuffer == null || !_mixBuffer.Allocated)
        _mixBuffer = SynthBuffer.Construct(buffer.Length);

    // resize if buffer length changed
    if (_mixBuffer.Data.Length != buffer.Length)
    {
        _mixBuffer.Dispose();
        _mixBuffer = SynthBuffer.Construct(buffer.Length);
    }

    buffer.Clear();

    foreach (var channel in channels)
    {
        if (channel.provider == null) continue;
        channel.provider.FillBuffer(ref _mixBuffer.Data);
        _mixBuffer.Data.Scale(channel.volume);
        _mixBuffer.Data.MixInto(ref buffer);
        _mixBuffer.Data.Clear();
    }

    buffer.Scale(masterVolume);
}

private void OnDestroy()
{
    if (_mixBuffer is { Allocated: true })
        _mixBuffer.Dispose();
}
    }
}