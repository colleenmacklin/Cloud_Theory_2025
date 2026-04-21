namespace Synthic
{
    public struct LFOState
    {
        public float phase;
        public float rate;
        public float depth;
        public float baseValue;
        public int waveform; // 0=Sine, 1=Saw, 2=Square, 3=Triangle
    }
}
