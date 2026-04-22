using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Synthic
{
    public class NoteInput : MonoBehaviour
    {

// single note
//noteInput.NoteOn(Note.Name.C, 4);
//noteInput.NoteOff(Note.Name.C, 4);

// chord
//noteInput.ChordOn(Note.Name.C, Note.Major, 4);
//noteInput.ChordOff(Note.Name.C, Note.Major, 4);

        [SerializeField] private PolyphonicGenerator generator;

        // track which keys are currently held to avoid retriggering
        private readonly HashSet<KeyCode> _heldKeys = new();

        private void Update()
{
    foreach (var kvp in Note.KeyboardMap)
    {
        KeyCode key     = kvp.Key;
        float frequency = kvp.Value;

        // convert legacy KeyCode to new Input System Key
        Key inputKey = KeyCodeToKey(key);
        if (inputKey == Key.None) continue;

        bool isDown = Keyboard.current != null && Keyboard.current[inputKey].wasPressedThisFrame;
        bool isUp   = Keyboard.current != null && Keyboard.current[inputKey].wasReleasedThisFrame;

        if (isDown && !_heldKeys.Contains(key))
        {
            _heldKeys.Add(key);
        }
        else if (isUp && _heldKeys.Contains(key))
        {
            _heldKeys.Remove(key);
        }
    }

}

private Key KeyCodeToKey(KeyCode keyCode)
{
    return keyCode switch
    {
        KeyCode.A => Key.A, KeyCode.B => Key.B, KeyCode.C => Key.C,
        KeyCode.D => Key.D, KeyCode.E => Key.E, KeyCode.F => Key.F,
        KeyCode.G => Key.G, KeyCode.H => Key.H, KeyCode.I => Key.I,
        KeyCode.J => Key.J, KeyCode.K => Key.K, KeyCode.L => Key.L,
        KeyCode.M => Key.M, KeyCode.N => Key.N, KeyCode.O => Key.O,
        KeyCode.P => Key.P, KeyCode.Q => Key.Q, KeyCode.R => Key.R,
        KeyCode.S => Key.S, KeyCode.T => Key.T, KeyCode.U => Key.U,
        KeyCode.V => Key.V, KeyCode.W => Key.W, KeyCode.X => Key.X,
        KeyCode.Y => Key.Y, KeyCode.Z => Key.Z,
        KeyCode.Alpha0 => Key.Digit0, KeyCode.Alpha1 => Key.Digit1,
        KeyCode.Alpha2 => Key.Digit2, KeyCode.Alpha3 => Key.Digit3,
        KeyCode.Alpha4 => Key.Digit4, KeyCode.Alpha5 => Key.Digit5,
        KeyCode.Alpha6 => Key.Digit6, KeyCode.Alpha7 => Key.Digit7,
        KeyCode.Alpha8 => Key.Digit8, KeyCode.Alpha9 => Key.Digit9,
        _ => Key.None
    };
}

        // public methods for triggering from code or UI buttons
        //public void NoteOn(float frequency)  => generator.NoteOn(frequency);
        //added velocity to notes
        public void NoteOn(float frequency, float velocity = 1f)  => generator.NoteOn(frequency, velocity);

        public void NoteOff(float frequency) => generator.NoteOff(frequency);

        // convenience methods using Note names
        //public void NoteOn(Note.Name note, int octave = 4) => generator.NoteOn(Note.Get(note, octave));
        //added velocity to notes
        public void NoteOn(Note.Name note, int octave = 4, float velocity = 1f) => generator.NoteOn(Note.Get(note, octave), velocity);

        public void NoteOff(Note.Name note, int octave = 4)
            => generator.NoteOff(Note.Get(note, octave));

        // play a full chord
        //public void ChordOn(Note.Name root, int[] intervals, int octave = 4)
        //{
            //foreach (float freq in Note.GetChord(root, intervals, octave))
                //generator.NoteOn(freq);
        //}
//added velocity to chords
public void ChordOn(Note.Name root, int[] intervals, int octave = 4, float velocity = 1f)
{
    foreach (float freq in Note.GetChord(root, intervals, octave))
        generator.NoteOn(freq, velocity);
}

        public void ChordOff(Note.Name root, int[] intervals, int octave = 4)
        {
            foreach (float freq in Note.GetChord(root, intervals, octave))
                generator.NoteOff(freq);
        }
    }
}