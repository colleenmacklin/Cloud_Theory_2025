using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using Synthic;
using Synthic.Patterns;

namespace Forest
{
    // One note in an animal's hop sequence. Every note in the sequence lasts the
    // animal's noteValue (see AnimalNoteSequencer), which is set by body size.
    [System.Serializable]
    public struct SequencedNote
    {
        [Tooltip("MIDI note number (e.g. 60 = middle C). Matches the FMOD event's MidiNote parameter range (36-84).")]
        [Range(36, 84)] public int midiNote;
    }

    // A scale step plus an octave offset — what a step pattern produces before the
    // scene's MusicalKey turns it into an actual pitch.
    public readonly struct StepNote
    {
        public readonly int Step;
        public readonly int Octave;
        public StepNote(int step, int octave) { Step = step; Octave = octave; }
        public override string ToString() => Octave == 0 ? Step.ToString() : $"{Step}(oct {Octave:+0;-0})";
    }

    // Plays a per-animal sequence of notes through FMOD, one note per hop, walking the
    // sequence forward when the animal flees and backward when it hops closer — the
    // "same mechanism, opposite direction" the design calls for. Timing (how long the
    // animal must wait before its next note is allowed) is derived from
    // RhythmicMasterClock's current BPM via SecondsForDuration, so the whole cast of
    // animals stays in the same tempo even if BPM changes at runtime.
    //
    // Two ways to give an animal its notes:
    //   1. Step pattern (recommended): type scale steps into stepPattern, e.g.
    //      "0 2 4 <5 7>". Steps are turned into pitches by the scene's MusicalKey, so
    //      changing the key retunes everything. The pattern is a Tidal/Strudel-style
    //      pattern (see Synthic.Patterns): each hop plays the slice of pattern time it
    //      covers, forward on flee-hops and backward on hop-closer hops.
    //   2. MIDI list (the original system): used when stepPattern is empty.
    public class AnimalNoteSequencer : MonoBehaviour
    {
        [Header("Step Pattern (scale steps, in the scene's key)")]
        [Tooltip("Scale steps in mini-notation. 0 = the key's root, 1 = next note up the scale, 7 = root an octave up (in a 7-note scale), negative = below the root.\n" +
                 "\"0 2 4 7\"  one step per hop\n" +
                 "\"0 [2 4] 7 ~\"  [ ] = two notes in one hop, ~ = silent hop\n" +
                 "\"0 <2 4>\"  < > = alternate each cycle\n" +
                 "\"0 2, 4 7\"  comma = layers (chords)\n" +
                 "\"0*2 4\"  * = faster, /2 = slower, !3 = repeat, ? = sometimes skip\n" +
                 "Leave empty to use the MIDI note list below instead.")]
        public string stepPattern = "";

        [Tooltip("How many hops one cycle of the pattern lasts. With 4, \"0 2 4 7\" plays one step per hop; \"0 2\" would play one step every two hops.")]
        [Min(1)] public int hopsPerCycle = 4;

        [Tooltip("Optional octave pattern, combined with the steps like the talk's in-octave demo (~10:35). e.g. \"0 1 2\" = up through three octaves. Leave empty for none.")]
        public string octavePattern = "";
        [Tooltip("How the step and octave patterns' rhythms combine:\n" +
                 "Left — keep the steps' rhythm; each step takes the octave playing at that moment.\n" +
                 "Right — keep the octaves' rhythm.\n" +
                 "Both — every overlap becomes its own note.\n" +
                 "SqueezeIn — squeeze all the octaves into each step.\n" +
                 "SqueezeOut — squeeze all the steps into each octave.")]
        public Structure octaveStructure = Structure.Left;

        [Tooltip("Shifts this animal's whole pattern by octaves. e.g. -1 for big animals, +1 for small ones.")]
        [Range(-3, 3)] public int octaveOffset = 0;

        [Header("Step Pattern Transformations")]
        [Tooltip("Speed the pattern up (2 = twice as many steps per hop) or down (0.5).")]
        [Min(0.01f)] public float fast = 1f;
        [Tooltip("Shift the pattern earlier by this many cycles (e.g. 0.25 starts one quarter of the way in).")]
        public float shift = 0f;
        [Tooltip("Play each cycle backward.")]
        public bool reverse = false;
        [Tooltip("Alternate cycles forward and backward: up, then down (the talk's palindrome, ~10:05).")]
        public bool palindrome = false;
        [Tooltip("Chance each note is skipped. Deterministic: the same notes drop out every time the animal passes that point in its pattern.")]
        [Range(0f, 1f)] public float dropChance = 0f;

        [Header("MIDI Note List (used when Step Pattern is empty)")]
        [Tooltip("This animal's note pattern, in the order played while fleeing (forward). Hopping closer plays the same list backward.")]
        public List<SequencedNote> notes = new();

        [Header("Rhythm")]
        [Tooltip("How long each note lasts before the animal may hop (and play) again. Set per animal by body size: whole notes for the largest animals, 16ths for the smallest. The hop's airtime is a fraction of this.")]
        public NoteDuration noteValue = NoteDuration.Quarter;

        [Header("FMOD")]
        [Tooltip("FMOD event for a single synth note. Must expose a MidiNote parameter (range 36-84), same convention as FMODChordPlayer.")]
        public EventReference noteEvent;
        [Tooltip("FMOD parameter name that selects the pitch.")]
        public string midiNoteParam = "MidiNote";
        [Range(0f, 1f)] public float noteVolume = 0.8f;

        [Header("Debug")]
        [Tooltip("Log every note this animal plays (step, octave and MIDI number).")]
        public bool logNotes = false;

        private const int MinMidi = 36;
        private const int MaxMidi = 84;

        // Index of the note that was last played (MIDI list mode). Starts at -1 so the
        // very first PlayNextForward() call plays notes[0] rather than notes[1].
        private int _index = -1;
        private float _readyTime = 0f;

        // Step pattern mode: where this animal is in pattern time, in cycles. Moves
        // forward one hop's worth on each flee-hop and back one hop's worth on each
        // hop-closer, so hopping closer literally replays the pattern in reverse.
        private Rational _cyclePos = Rational.Zero;
        private Pattern<StepNote> _pattern;
        private Pattern<StepNote> _codePattern;
        private bool _patternDirty = true;

        // True once the cooldown from the last-played note's duration has elapsed —
        // AnimalHopController gates hopping on this so hops never outrun the sequence's
        // own rhythm.
        public bool IsReady => Time.time >= _readyTime;
        public float TimeUntilReady => Mathf.Max(0f, _readyTime - Time.time);

        // Length of noteValue in seconds at the master clock's current tempo (falls back
        // to 120 BPM if no clock exists in the scene yet).
        public float NoteSeconds => RhythmicMasterClock.Instance != null
            ? RhythmicMasterClock.Instance.SecondsForDuration(noteValue)
            : FallbackBeats(noteValue) * 0.5f;

        public bool UsesStepPattern => _codePattern != null || !string.IsNullOrWhiteSpace(stepPattern);

        private static float FallbackBeats(NoteDuration d) => d switch
        {
            NoteDuration.ThirtySecond => 0.125f,
            NoteDuration.Sixteenth    => 0.25f,
            NoteDuration.Eighth       => 0.5f,
            NoteDuration.Quarter      => 1f,
            NoteDuration.Half         => 2f,
            NoteDuration.Whole        => 4f,
            _ => 1f
        };

        private void OnValidate() => _patternDirty = true;

        public void ResetToStart()
        {
            _index = -1;
            _cyclePos = Rational.Zero;
        }

        // Give this animal a pattern written in code instead of the Inspector string,
        // e.g. Pattern.Palindrome(Mini.Parse("0 2 4 7")).Fmap(s => new StepNote(s, 0)).
        // Pass null to go back to the Inspector fields.
        public void SetPattern(Pattern<StepNote> pattern)
        {
            _codePattern = pattern;
            _patternDirty = true;
        }

        public void SetPattern(Pattern<int> steps) =>
            SetPattern(steps?.Fmap(s => new StepNote(s, 0)));

        // Plays the next note going forward (wrapping to the start past the end) —
        // called on each flee-hop.
        public void PlayNextForward()
        {
            if (UsesStepPattern) { PlayPatternSlice(forward: true); return; }
            if (notes.Count == 0) return;
            _index = (_index + 1) % notes.Count;
            PlayCurrent();
        }

        // Plays the previous note going backward (wrapping to the end past the start) —
        // called on each hop-closer.
        public void PlayNextReverse()
        {
            if (UsesStepPattern) { PlayPatternSlice(forward: false); return; }
            if (notes.Count == 0) return;
            _index = _index <= 0 ? notes.Count - 1 : _index - 1;
            PlayCurrent();
        }

        private void PlayCurrent()
        {
            SequencedNote note = notes[_index];
            PlayNote(note.midiNote);

            _readyTime = Time.time + NoteSeconds;
        }

        // ── Step pattern playback ─────────────────────────────────────────

        private Pattern<StepNote> CurrentPattern()
        {
            if (_patternDirty || _pattern == null)
            {
                _pattern = BuildPattern();
                _patternDirty = false;
            }
            return _pattern;
        }

        private Pattern<StepNote> BuildPattern()
        {
            Pattern<StepNote> p;
            if (_codePattern != null)
            {
                p = _codePattern;
            }
            else
            {
                if (!Mini.TryParse(stepPattern, out Pattern<int> steps, out string err))
                    Debug.LogWarning($"[AnimalNoteSequencer] {name}: step pattern error — {err}");

                if (string.IsNullOrWhiteSpace(octavePattern))
                {
                    p = steps.Fmap(s => new StepNote(s, 0));
                }
                else
                {
                    if (!Mini.TryParse(octavePattern, out Pattern<int> octaves, out string octErr))
                        Debug.LogWarning($"[AnimalNoteSequencer] {name}: octave pattern error — {octErr}");
                    p = Pattern.Combine(steps, octaves, (s, o) => new StepNote(s, o), octaveStructure);
                }
            }

            // Order matters, same as chaining functions in Strudel.
            if (!Mathf.Approximately(fast, 1f)) p = p.Fast(Rational.FromDouble(fast));
            if (!Mathf.Approximately(shift, 0f)) p = p.Early(Rational.FromDouble(shift));
            if (reverse) p = p.Rev();
            if (palindrome) p = Pattern.Palindrome(p);
            if (dropChance > 0f) p = p.Degrade(dropChance, seed: GetInstanceID());
            return p;
        }

        private void PlayPatternSlice(bool forward)
        {
            float noteSeconds = NoteSeconds;
            _readyTime = Time.time + noteSeconds;

            Rational hop = new Rational(1, Mathf.Max(1, hopsPerCycle));
            Rational begin = forward ? _cyclePos : _cyclePos - hop;
            Rational end = begin + hop;
            _cyclePos = forward ? end : begin;

            // Seconds per cycle of pattern time, for spacing notes within one hop.
            float secondsPerCycle = noteSeconds * Mathf.Max(1, hopsPerCycle);

            var haps = CurrentPattern().QueryArc(begin, end);
            var toPlay = new List<(float delay, StepNote note)>();
            foreach (var h in haps)
            {
                if (forward)
                {
                    // Trigger notes that START in this hop, at their start time.
                    if (!h.HasOnset) continue;
                    toPlay.Add(((h.Whole.Begin - begin).ToFloat() * secondsPerCycle, h.Value));
                }
                else
                {
                    // Time runs backward: a note "starts" at its end. Trigger notes that
                    // END in this hop, measured from the hop's end.
                    if (h.Part.End != h.Whole.End || h.Whole.End <= begin) continue;
                    toPlay.Add(((end - h.Whole.End).ToFloat() * secondsPerCycle, h.Value));
                }
            }
            toPlay.Sort((a, b) => a.delay.CompareTo(b.delay));

            foreach (var (delay, note) in toPlay)
            {
                if (delay <= 0.0001f) PlayStep(note);
                else StartCoroutine(PlayStepAfter(delay, note));
            }
        }

        private IEnumerator PlayStepAfter(float delay, StepNote note)
        {
            yield return new WaitForSeconds(delay);
            PlayStep(note);
        }

        private void PlayStep(StepNote note)
        {
            int midi = MusicalKey.StepToMidiOrDefault(note.Step, note.Octave + octaveOffset);
            // Fold into FMOD's parameter range by octaves, so the note stays in key.
            while (midi < MinMidi) midi += 12;
            while (midi > MaxMidi) midi -= 12;

            if (logNotes)
                Debug.Log($"[AnimalNoteSequencer] {name}: step {note} → MIDI {midi} (cycle {_cyclePos})");

            PlayNote(midi);
        }

        // ── FMOD ──────────────────────────────────────────────────────────

        private void PlayNote(int midiNote)
        {
            if (noteEvent.IsNull)
            {
                Debug.LogWarning($"[AnimalNoteSequencer] {name}: noteEvent is not assigned.");
                return;
            }

            try
            {
                var inst = RuntimeManager.CreateInstance(noteEvent);
                // Required once the event has a spatializer: sets 3D attributes immediately
                // and keeps them updated as the animal hops, so distance-based volume/pan
                // tracks it correctly instead of FMOD warning and playing unpositioned.
                RuntimeManager.AttachInstanceToGameObject(inst, transform);
                inst.setParameterByName(midiNoteParam, midiNote);
                inst.setVolume(noteVolume);
                inst.start();
                inst.release(); // one-shot: FMOD keeps playing until the sound finishes, then cleans up
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AnimalNoteSequencer] {name}: failed to play note {midiNote}: {e.Message}");
            }
        }
    }
}
