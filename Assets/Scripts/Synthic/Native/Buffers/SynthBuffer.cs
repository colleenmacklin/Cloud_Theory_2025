using System;
using System.Runtime.InteropServices;
using Synthic.Native.Data;
using Unity.Collections.LowLevel.Unsafe;

namespace Synthic.Native.Buffers
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SynthBuffer : INativeObject
    { 
        private BufferHandler<StereoData> _buffer;
        
        public int Length => _buffer.Length;
        public bool Allocated => _buffer.Allocated;

        public static NativeBox<SynthBuffer> Construct(int bufferLength)
        {
            return new NativeBox<SynthBuffer>(new SynthBuffer {_buffer = new BufferHandler<StereoData>(bufferLength)});
        }
        
        public StereoData this[int index]
        {
            get => _buffer[index];
            set => _buffer[index] = value;
        }

        public unsafe void CopyToManaged(float[] managedArray)
        {
            if (!Allocated) throw new ObjectDisposedException("Cannot copy. Buffer has been disposed");
            int length = Math.Min(managedArray.Length, Length * 2);
            GCHandle gcHandle = GCHandle.Alloc(managedArray, GCHandleType.Pinned);
            UnsafeUtility.MemCpy((void*) gcHandle.AddrOfPinnedObject(), _buffer.Pointer, length * sizeof(StereoData));
            gcHandle.Free();
        }
// add to SynthBuffer.cs
public unsafe void MixInto(ref SynthBuffer destination)
{
    if (!Allocated || !destination.Allocated) return;
    int length = Math.Min(Length, destination.Length);
    for (int i = 0; i < length; i++)
    {
        StereoData* src = _buffer.Pointer + i;
        StereoData* dst = destination._buffer.Pointer + i;
        dst->LeftChannel  += src->LeftChannel;
        dst->RightChannel += src->RightChannel;
    }
}

public unsafe void Clear()
{
    if (!Allocated) return;
    UnsafeUtility.MemClear(_buffer.Pointer, Length * sizeof(StereoData));
}

public unsafe void Scale(float factor)
{
    if (!Allocated) return;
    for (int i = 0; i < Length; i++)
    {
        StereoData* ptr = _buffer.Pointer + i;
        ptr->LeftChannel  *= factor;
        ptr->RightChannel *= factor;
    }
}
        
        public void CopyTo(ref SynthBuffer buffer) => _buffer.CopyTo(buffer._buffer);

        void INativeObject.ReleaseResources() => _buffer.Dispose();
    }
}