using UnityEngine;
using UnityEngine.InputSystem;
using Minis;

namespace Synthic
{
    public class MidiInput : MonoBehaviour
    {
        [SerializeField] private NoteInput noteInput;

        // optionally filter to a specific MIDI channel (-1 = all channels)
        [SerializeField] private int midiChannel = -1;

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;

            // subscribe to any MIDI devices already connected
            foreach (var device in InputSystem.devices)
            {
                var midiDevice = device as MidiDevice;
                if (midiDevice != null)
                    SubscribeToDevice(midiDevice);
            }
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;

            // unsubscribe from all connected devices
            foreach (var device in InputSystem.devices)
            {
                var midiDevice = device as MidiDevice;
                if (midiDevice != null)
                    UnsubscribeFromDevice(midiDevice);
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            var midiDevice = device as MidiDevice;
            if (midiDevice == null) return;

            switch (change)
            {
                case InputDeviceChange.Added:
                    SubscribeToDevice(midiDevice);
                    Debug.Log($"MIDI device connected: {device.description.product} " +
                              $"ch:{midiDevice.channel}");
                    break;
                case InputDeviceChange.Removed:
                    UnsubscribeFromDevice(midiDevice);
                    Debug.Log($"MIDI device disconnected: {device.description.product}");
                    break;
            }
        }

        private void SubscribeToDevice(MidiDevice device)
        {
            if (!ChannelMatches(device)) return;
            device.onWillNoteOn  += OnNoteOn;
            device.onWillNoteOff += OnNoteOff;
        }

        private void UnsubscribeFromDevice(MidiDevice device)
        {
            device.onWillNoteOn  -= OnNoteOn;
            device.onWillNoteOff -= OnNoteOff;
        }

        private void OnNoteOn(MidiNoteControl note, float velocity)
        {
            if (noteInput == null) return;

            // convert MIDI note number to frequency using our Note utility
            float frequency = Note.MidiToFrequency(note.noteNumber);
            noteInput.NoteOn(frequency, velocity);
        }

        private void OnNoteOff(MidiNoteControl note)
        {
            if (noteInput == null) return;

            float frequency = Note.MidiToFrequency(note.noteNumber);
            noteInput.NoteOff(frequency);
        }

        private bool ChannelMatches(MidiDevice device)
        {
            // -1 means accept all channels
            return midiChannel == -1 || device.channel == midiChannel;
        }
    }
}