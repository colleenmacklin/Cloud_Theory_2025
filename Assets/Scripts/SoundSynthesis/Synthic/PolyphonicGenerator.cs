using Synthic.Native;
using Synthic.Native.Buffers;
using Synthic.Native.Data;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

namespace Synthic
{
    [BurstCompile]
    public class PolyphonicGenerator : SynthProvider
    {
        private const int VoiceCount = 8;

        public enum WaveformType { Sine, Saw, Square, Triangle }
        [SerializeField] private WaveformType waveform = WaveformType.Sine;
        [SerializeField, Range(0f, 1f)] private float masterAmplitude = 0.5f;
        [SerializeField] private ADSREnvelope envelope = new ADSREnvelope
        {
            attack  = 0.01f,
            decay   = 0.1f,
            sustain = 0.7f,
            release = 0.3f
        };
        [SerializeField] private List<LFO> lfos = new();
        [SerializeField, Range(0f, 100f)] private float unisonDetune = 10f;
        [SerializeField, Range(0f, 1f)]   private float unisonSpread = 0.5f;
        [SerializeField, Range(0f, 1f)]   private float smoothingFactor = 0.05f;

        public WaveformType Waveform { get => waveform; set { waveform = value; } }
        public float Attack  { get => envelope.attack;  }
        public float Decay   { get => envelope.decay;   }
        public float Sustain { get => envelope.sustain; }
        public float Release { get => envelope.release; }

        public void SetAttack(float v)  => envelope.attack  = Mathf.Clamp(v, 0.001f, 5f);
        public void SetDecay(float v)   => envelope.decay   = Mathf.Clamp(v, 0.001f, 5f);
        public void SetSustain(float v) => envelope.sustain = Mathf.Clamp01(v);
        public void SetRelease(float v) => envelope.release = Mathf.Clamp(v, 0.001f, 5f);

        private float _savedAttack, _savedDecay, _savedSustain, _savedRelease;
        private bool  _adsrSaved = false;

        public List<LFO> GetLFOs() => lfos;

        public void SaveADSR()
        {
            if (_adsrSaved) return;
            _savedAttack  = envelope.attack;
            _savedDecay   = envelope.decay;
            _savedSustain = envelope.sustain;
            _savedRelease = envelope.release;
            _adsrSaved    = true;
        }

        public void RestoreADSR()
        {
            if (!_adsrSaved) return;
            envelope.attack  = _savedAttack;
            envelope.decay   = _savedDecay;
            envelope.sustain = _savedSustain;
            envelope.release = _savedRelease;
            _adsrSaved       = false;
        }

        private delegate void BurstVoiceDelegate(
            ref SynthBuffer buffer, ref SynthVoice voice,
            int sampleRate, float masterAmplitude,
            float attack, float decay, float sustain, float release,
            float freqLfoRate, float freqLfoDepth, float freqLfoBaseValue, int freqLfoWaveform,
            float ampLfoRate,  float ampLfoDepth,  float ampLfoBaseValue,  int ampLfoWaveform,
            float unisonDetune, float unisonSpread, float smoothingFactor);

        private static BurstVoiceDelegate _burstSine;
        private static BurstVoiceDelegate _burstSaw;
        private static BurstVoiceDelegate _burstSquare;
        private static BurstVoiceDelegate _burstTriangle;
        private BurstVoiceDelegate _activeWave;
        private WaveformType _currentWaveform;

        private SynthVoice[] _voices = new SynthVoice[VoiceCount];
        private int _voiceOrder = 0;
        private int _sampleRate;
        private NativeBox<SynthBuffer> _voiceBuffer;

        private float _freqLfoRate, _freqLfoDepth, _freqLfoBase;
        private int   _freqLfoWaveform;
        private float _ampLfoRate, _ampLfoDepth, _ampLfoBase;
        private int   _ampLfoWaveform;

        private void Awake()
        {
            _sampleRate    = AudioSettings.outputSampleRate;
            _burstSine     ??= BurstCompiler.CompileFunctionPointer<BurstVoiceDelegate>(BurstSine).Invoke;
            _burstSaw      ??= BurstCompiler.CompileFunctionPointer<BurstVoiceDelegate>(BurstSaw).Invoke;
            _burstSquare   ??= BurstCompiler.CompileFunctionPointer<BurstVoiceDelegate>(BurstSquare).Invoke;
            _burstTriangle ??= BurstCompiler.CompileFunctionPointer<BurstVoiceDelegate>(BurstTriangle).Invoke;
            SetWaveform(waveform);

            for (int i = 0; i < VoiceCount; i++)
                _voices[i] = new SynthVoice { stage = EnvelopeStage.Off };
        }

        private void Update()
        {
            if (waveform != _currentWaveform) SetWaveform(waveform);

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
                _freqLfoRate = 0f; _freqLfoDepth = 0f;
                _freqLfoBase = 0.5f; _freqLfoWaveform = 0;
            }

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
                _ampLfoRate = 0f; _ampLfoDepth = 0f;
                _ampLfoBase = 1f; _ampLfoWaveform = 0;
            }
        }

        private void OnDestroy()
        {
            if (_voiceBuffer is { Allocated: true }) _voiceBuffer.Dispose();
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

        public void NoteOn(float frequency, float velocity = 1f)
        {
            int noteId = Note.GetId(frequency);
            for (int i = 0; i < VoiceCount; i++)
                if (_voices[i].noteId == noteId && _voices[i].stage != EnvelopeStage.Off) return;

            int voiceIndex = -1;
            for (int i = 0; i < VoiceCount; i++)
                if (_voices[i].stage == EnvelopeStage.Off) { voiceIndex = i; break; }

            if (voiceIndex == -1)
            {
                int oldestOrder = int.MaxValue;
                for (int i = 0; i < VoiceCount; i++)
                    if (_voices[i].startOrder < oldestOrder) { oldestOrder = _voices[i].startOrder; voiceIndex = i; }
            }

            _voices[voiceIndex] = new SynthVoice
            {
                frequency = frequency, phase = 0f, phase2 = 0f,
                freqLfoPhase = 0f, ampLfoPhase = 0f,
                envelopePosition = 0f, currentAmplitude = 0f, smoothedAmplitude = 0f,
                velocity = Mathf.Clamp01(velocity), stage = EnvelopeStage.Attack,
                noteId = noteId, startOrder = _voiceOrder++
            };
        }

        public void ForceNoteOn(float frequency, float velocity = 1f)
        {
            int voiceIndex = -1;
            for (int i = 0; i < VoiceCount; i++)
                if (_voices[i].stage == EnvelopeStage.Off) { voiceIndex = i; break; }

            if (voiceIndex == -1)
            {
                int oldestOrder = int.MaxValue;
                for (int i = 0; i < VoiceCount; i++)
                    if (_voices[i].startOrder < oldestOrder) { oldestOrder = _voices[i].startOrder; voiceIndex = i; }
            }

            float prevPhase    = _voices[voiceIndex].phase;
            float prevFreqLfo  = _voices[voiceIndex].freqLfoPhase;
            float prevAmpLfo   = _voices[voiceIndex].ampLfoPhase;
            float prevSmoothed = _voices[voiceIndex].smoothedAmplitude;
            float vel          = Mathf.Clamp01(velocity);

            _voices[voiceIndex] = new SynthVoice
            {
                frequency         = frequency,
                phase             = prevPhase,
                phase2            = prevPhase,
                freqLfoPhase      = prevFreqLfo,
                ampLfoPhase       = prevAmpLfo,
                envelopePosition  = 0f,
                currentAmplitude  = prevSmoothed,
                smoothedAmplitude = prevSmoothed,
                velocity          = vel,
                stage             = EnvelopeStage.Attack,
                noteId            = Note.GetId(frequency),
                startOrder        = _voiceOrder++
            };
        }

        public void ForceNoteOn(float frequency, float velocity, float pan)
        {
            int voiceIndex = -1;
            for (int i = 0; i < VoiceCount; i++)
                if (_voices[i].stage == EnvelopeStage.Off) { voiceIndex = i; break; }

            if (voiceIndex == -1)
            {
                int oldestOrder = int.MaxValue;
                for (int i = 0; i < VoiceCount; i++)
                    if (_voices[i].startOrder < oldestOrder) { oldestOrder = _voices[i].startOrder; voiceIndex = i; }
            }

            // prefer stealing a voice already playing the same note
            int sameNoteIndex2 = -1;
            int noteId2 = Note.GetId(frequency);
            for (int i = 0; i < VoiceCount; i++)
                if (_voices[i].noteId == noteId2 && _voices[i].stage != EnvelopeStage.Off)
                    { sameNoteIndex2 = i; break; }
            if (sameNoteIndex2 != -1) voiceIndex = sameNoteIndex2;

            float prevPhase    = _voices[voiceIndex].phase;
            float prevFreqLfo  = _voices[voiceIndex].freqLfoPhase;
            float prevAmpLfo   = _voices[voiceIndex].ampLfoPhase;
            float prevSmoothed = _voices[voiceIndex].smoothedAmplitude;
            float vel          = Mathf.Clamp01(velocity);

            _voices[voiceIndex] = new SynthVoice
            {
                frequency         = frequency,
                phase             = prevPhase,
                phase2            = prevPhase,   // sync — eliminates anti-phase pop
                freqLfoPhase      = prevFreqLfo,
                ampLfoPhase       = prevAmpLfo,
                envelopePosition  = 0f,          // always start attack from 0
                currentAmplitude  = prevSmoothed,
                smoothedAmplitude = prevSmoothed,
                velocity          = vel,
                pan               = Mathf.Clamp(pan, -1f, 1f),
                stage             = EnvelopeStage.Attack,
                noteId            = Note.GetId(frequency),
                startOrder        = _voiceOrder++
            };
        }

        public void NoteOff(float frequency)
        {
            int noteId = Note.GetId(frequency);
            for (int i = 0; i < VoiceCount; i++)
            {
                if (_voices[i].noteId == noteId &&
                    _voices[i].stage != EnvelopeStage.Off &&
                    _voices[i].stage != EnvelopeStage.Release)
                {
                    _voices[i].stage            = EnvelopeStage.Release;
                    _voices[i].envelopePosition = 0f;
                }
            }
        }

        protected override void ProcessBuffer(ref SynthBuffer buffer)
        {
            if (_voiceBuffer == null || !_voiceBuffer.Allocated)
                _voiceBuffer = SynthBuffer.Construct(buffer.Length);
            if (_voiceBuffer.Data.Length != buffer.Length)
            {
                _voiceBuffer.Dispose();
                _voiceBuffer = SynthBuffer.Construct(buffer.Length);
            }

            buffer.Clear();

            for (int i = 0; i < VoiceCount; i++)
            {
                if (_voices[i].stage == EnvelopeStage.Off) continue;
                _voiceBuffer.Data.Clear();
                _activeWave(
                    ref _voiceBuffer.Data, ref _voices[i],
                    _sampleRate, masterAmplitude,
                    envelope.attack, envelope.decay, envelope.sustain, envelope.release,
                    _freqLfoRate, _freqLfoDepth, _freqLfoBase, _freqLfoWaveform,
                    _ampLfoRate,  _ampLfoDepth,  _ampLfoBase,  _ampLfoWaveform,
                    unisonDetune, unisonSpread, smoothingFactor);
                _voiceBuffer.Data.MixInto(ref buffer);
            }
        }

        private static float ComputeEnvelope(ref SynthVoice voice, int sampleRate,
                                             float attack, float decay,
                                             float sustain, float release,
                                             float smoothingFactor)
        {
            float amplitude = voice.currentAmplitude;

            switch (voice.stage)
            {
                case EnvelopeStage.Attack:
                    voice.envelopePosition += 1f / (attack * sampleRate);
                    amplitude = voice.envelopePosition * voice.velocity;
                    if (voice.envelopePosition >= 1f)
                    {
                        voice.envelopePosition = 0f;
                        voice.stage = EnvelopeStage.Decay;
                        amplitude   = 1f * voice.velocity;
                    }
                    break;

                case EnvelopeStage.Decay:
                    voice.envelopePosition += 1f / (decay * sampleRate);
                    amplitude = math.lerp(1f, sustain, voice.envelopePosition) * voice.velocity;
                    if (voice.envelopePosition >= 1f)
                    {
                        voice.envelopePosition = 0f;
                        voice.stage = EnvelopeStage.Sustain;
                        amplitude   = sustain * voice.velocity;
                    }
                    break;

                case EnvelopeStage.Sustain:
                    amplitude = sustain * voice.velocity;
                    break;

                case EnvelopeStage.Release:
                    voice.envelopePosition += 1f / (release * sampleRate);
                    amplitude = math.lerp(sustain, 0f, voice.envelopePosition) * voice.velocity;
                    if (voice.envelopePosition >= 1f)
                    {
                        voice.stage = EnvelopeStage.Off;
                        amplitude   = 0f;
                    }
                    break;
            }

            voice.currentAmplitude = amplitude;
            voice.smoothedAmplitude += (amplitude - voice.smoothedAmplitude) * smoothingFactor;
            return voice.smoothedAmplitude;
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
        private static void BurstSine(
            ref SynthBuffer buffer, ref SynthVoice voice,
            int sampleRate, float masterAmplitude,
            float attack, float decay, float sustain, float release,
            float freqLfoRate, float freqLfoDepth, float freqLfoBaseValue, int freqLfoWaveform,
            float ampLfoRate,  float ampLfoDepth,  float ampLfoBaseValue,  int ampLfoWaveform,
            float unisonDetune, float unisonSpread, float smoothingFactor)
        {
            float freqLfoIncrement = freqLfoRate / sampleRate;
            float ampLfoIncrement  = ampLfoRate  / sampleRate;

            float detuneRatio = math.pow(2f, (unisonDetune * 0.5f) / 1200f);
            float freq1 = voice.frequency / detuneRatio;
            float freq2 = voice.frequency * detuneRatio;

            float panLeft  = 1f - unisonSpread * 0.5f;
            float panRight = 1f - unisonSpread * 0.5f;

            for (int sample = 0; sample < buffer.Length; sample++)
            {
                // freq LFO: modulate around 1.0 — depth scales the swing
                float freqLfo  = ComputeLfo(voice.freqLfoPhase, freqLfoWaveform);
                float freqMod  = 1f + freqLfo * freqLfoDepth;

                // amp LFO: baseValue is the resting level, depth scales swing
                float ampLfo       = ComputeLfo(voice.ampLfoPhase, ampLfoWaveform);
                float modulatedAmp = math.clamp(
                    ampLfoBaseValue + ampLfo * ampLfoDepth * ampLfoBaseValue, 0f, 1f);

                float envelope = ComputeEnvelope(ref voice, sampleRate,
                    attack, decay, sustain, release, smoothingFactor);
                float gain = envelope * modulatedAmp * masterAmplitude * 0.5f;

                float osc1 = (float)math.sin(voice.phase  * 2f * math.PI_DBL);
                float osc2 = (float)math.sin(voice.phase2 * 2f * math.PI_DBL);

                float left  = (osc1 * panLeft  + osc2 * (1f - panRight)) * gain;
                float right = (osc1 * (1f - panLeft) + osc2 * panRight)  * gain;
                buffer[sample] = new StereoData(left, right);

                voice.phase        = (voice.phase        + (freq1 * freqMod) / sampleRate) % 1f;
                voice.phase2       = (voice.phase2       + (freq2 * freqMod) / sampleRate) % 1f;
                voice.freqLfoPhase = (voice.freqLfoPhase + freqLfoIncrement) % 1f;
                voice.ampLfoPhase  = (voice.ampLfoPhase  + ampLfoIncrement)  % 1f;

                if (voice.stage == EnvelopeStage.Off) break;
            }
        }

        [BurstCompile]
        private static void BurstSaw(
            ref SynthBuffer buffer, ref SynthVoice voice,
            int sampleRate, float masterAmplitude,
            float attack, float decay, float sustain, float release,
            float freqLfoRate, float freqLfoDepth, float freqLfoBaseValue, int freqLfoWaveform,
            float ampLfoRate,  float ampLfoDepth,  float ampLfoBaseValue,  int ampLfoWaveform,
            float unisonDetune, float unisonSpread, float smoothingFactor)
        {
            float freqLfoIncrement = freqLfoRate / sampleRate;
            float ampLfoIncrement  = ampLfoRate  / sampleRate;
            float detuneRatio = math.pow(2f, (unisonDetune * 0.5f) / 1200f);
            float freq1 = voice.frequency / detuneRatio;
            float freq2 = voice.frequency * detuneRatio;
            float panLeft  = 1f - unisonSpread * 0.5f;
            float panRight = 1f - unisonSpread * 0.5f;

            for (int sample = 0; sample < buffer.Length; sample++)
            {
                float freqLfo      = ComputeLfo(voice.freqLfoPhase, freqLfoWaveform);
                float freqMod      = 1f + freqLfo * freqLfoDepth;
                float ampLfo       = ComputeLfo(voice.ampLfoPhase, ampLfoWaveform);
                float modulatedAmp = math.clamp(
                    ampLfoBaseValue + ampLfo * ampLfoDepth * ampLfoBaseValue, 0f, 1f);

                float envelope = ComputeEnvelope(ref voice, sampleRate,
                    attack, decay, sustain, release, smoothingFactor);
                float gain = envelope * modulatedAmp * masterAmplitude * 0.5f;

                float osc1 = voice.phase  * 2f - 1f;
                float osc2 = voice.phase2 * 2f - 1f;
                float left  = (osc1 * panLeft  + osc2 * (1f - panRight)) * gain;
                float right = (osc1 * (1f - panLeft) + osc2 * panRight)  * gain;
                buffer[sample] = new StereoData(left, right);

                voice.phase        = (voice.phase        + (freq1 * freqMod) / sampleRate) % 1f;
                voice.phase2       = (voice.phase2       + (freq2 * freqMod) / sampleRate) % 1f;
                voice.freqLfoPhase = (voice.freqLfoPhase + freqLfoIncrement) % 1f;
                voice.ampLfoPhase  = (voice.ampLfoPhase  + ampLfoIncrement)  % 1f;
                if (voice.stage == EnvelopeStage.Off) break;
            }
        }

        [BurstCompile]
        private static void BurstSquare(
            ref SynthBuffer buffer, ref SynthVoice voice,
            int sampleRate, float masterAmplitude,
            float attack, float decay, float sustain, float release,
            float freqLfoRate, float freqLfoDepth, float freqLfoBaseValue, int freqLfoWaveform,
            float ampLfoRate,  float ampLfoDepth,  float ampLfoBaseValue,  int ampLfoWaveform,
            float unisonDetune, float unisonSpread, float smoothingFactor)
        {
            float freqLfoIncrement = freqLfoRate / sampleRate;
            float ampLfoIncrement  = ampLfoRate  / sampleRate;
            float detuneRatio = math.pow(2f, (unisonDetune * 0.5f) / 1200f);
            float freq1 = voice.frequency / detuneRatio;
            float freq2 = voice.frequency * detuneRatio;
            float panLeft  = 1f - unisonSpread * 0.5f;
            float panRight = 1f - unisonSpread * 0.5f;

            for (int sample = 0; sample < buffer.Length; sample++)
            {
                float freqLfo      = ComputeLfo(voice.freqLfoPhase, freqLfoWaveform);
                float freqMod      = 1f + freqLfo * freqLfoDepth;
                float ampLfo       = ComputeLfo(voice.ampLfoPhase, ampLfoWaveform);
                float modulatedAmp = math.clamp(
                    ampLfoBaseValue + ampLfo * ampLfoDepth * ampLfoBaseValue, 0f, 1f);

                float envelope = ComputeEnvelope(ref voice, sampleRate,
                    attack, decay, sustain, release, smoothingFactor);
                float gain = envelope * modulatedAmp * masterAmplitude * 0.5f;

                float osc1 = voice.phase  < 0.5f ? 1f : -1f;
                float osc2 = voice.phase2 < 0.5f ? 1f : -1f;
                float left  = (osc1 * panLeft  + osc2 * (1f - panRight)) * gain;
                float right = (osc1 * (1f - panLeft) + osc2 * panRight)  * gain;
                buffer[sample] = new StereoData(left, right);

                voice.phase        = (voice.phase        + (freq1 * freqMod) / sampleRate) % 1f;
                voice.phase2       = (voice.phase2       + (freq2 * freqMod) / sampleRate) % 1f;
                voice.freqLfoPhase = (voice.freqLfoPhase + freqLfoIncrement) % 1f;
                voice.ampLfoPhase  = (voice.ampLfoPhase  + ampLfoIncrement)  % 1f;
                if (voice.stage == EnvelopeStage.Off) break;
            }
        }

        [BurstCompile]
        private static void BurstTriangle(
            ref SynthBuffer buffer, ref SynthVoice voice,
            int sampleRate, float masterAmplitude,
            float attack, float decay, float sustain, float release,
            float freqLfoRate, float freqLfoDepth, float freqLfoBaseValue, int freqLfoWaveform,
            float ampLfoRate,  float ampLfoDepth,  float ampLfoBaseValue,  int ampLfoWaveform,
            float unisonDetune, float unisonSpread, float smoothingFactor)
        {
            float freqLfoIncrement = freqLfoRate / sampleRate;
            float ampLfoIncrement  = ampLfoRate  / sampleRate;
            float detuneRatio = math.pow(2f, (unisonDetune * 0.5f) / 1200f);
            float freq1 = voice.frequency / detuneRatio;
            float freq2 = voice.frequency * detuneRatio;
            float panLeft  = 1f - unisonSpread * 0.5f;
            float panRight = 1f - unisonSpread * 0.5f;

            for (int sample = 0; sample < buffer.Length; sample++)
            {
                float freqLfo      = ComputeLfo(voice.freqLfoPhase, freqLfoWaveform);
                float freqMod      = 1f + freqLfo * freqLfoDepth;
                float ampLfo       = ComputeLfo(voice.ampLfoPhase, ampLfoWaveform);
                float modulatedAmp = math.clamp(
                    ampLfoBaseValue + ampLfo * ampLfoDepth * ampLfoBaseValue, 0f, 1f);

                float envelope = ComputeEnvelope(ref voice, sampleRate,
                    attack, decay, sustain, release, smoothingFactor);
                float gain = envelope * modulatedAmp * masterAmplitude * 0.5f;

                float osc1 = voice.phase  < 0.5f ? (voice.phase  * 4f - 1f) : (3f - voice.phase  * 4f);
                float osc2 = voice.phase2 < 0.5f ? (voice.phase2 * 4f - 1f) : (3f - voice.phase2 * 4f);
                float left  = (osc1 * panLeft  + osc2 * (1f - panRight)) * gain;
                float right = (osc1 * (1f - panLeft) + osc2 * panRight)  * gain;
                buffer[sample] = new StereoData(left, right);

                voice.phase        = (voice.phase        + (freq1 * freqMod) / sampleRate) % 1f;
                voice.phase2       = (voice.phase2       + (freq2 * freqMod) / sampleRate) % 1f;
                voice.freqLfoPhase = (voice.freqLfoPhase + freqLfoIncrement) % 1f;
                voice.ampLfoPhase  = (voice.ampLfoPhase  + ampLfoIncrement)  % 1f;
                if (voice.stage == EnvelopeStage.Off) break;
            }
        }
    }
}
