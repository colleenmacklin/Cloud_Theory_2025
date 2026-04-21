namespace Synthic
{
    public enum EnvelopeStage { Off, Attack, Decay, Sustain, Release }
    

    public struct SynthVoice
    {
        public float frequency;
        public float phase;
        public float phase2;
        public float freqLfoPhase;
        public float ampLfoPhase;
        public float envelopePosition;
        public float currentAmplitude;
        public float velocity;
        public float pan;           // -1 = full left, 0 = center, 1 = full right
        public EnvelopeStage stage;
        public int noteId;
        public int startOrder;
        public float smoothedAmplitude; //added to eliminate clicking when notes are triggered via the sequencer
    }
}