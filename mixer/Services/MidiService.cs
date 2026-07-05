using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using mixer.Models;

namespace mixer.Services
{
    public class MidiValueEventArgs : EventArgs
    {
        public MidiMessageKind Kind { get; init; }
        public int Channel { get; init; }
        public int ControllerOrNote { get; init; }
        /// <summary>0-127 raw MIDI value (CC value, or velocity for NoteOn/NoteOff).</summary>
        public int Value { get; init; }
    }

    /// <summary>
    /// Wraps Melanchall.DryWetMidi to support ANY MIDI input device.
    /// All device I/O is wrapped in try-catch: a failing MIDI device must never crash
    /// the rest of the application, it should just report an error and stay inert.
    /// </summary>
    public class MidiService : IDisposable
    {
        private InputDevice? _inputDevice;
        private string? _currentDeviceName;

        /// <summary>Fired for every ControlChange / NoteOn / NoteOff message received.</summary>
        public event EventHandler<MidiValueEventArgs>? MessageReceived;

        /// <summary>Fired when a device connection attempt fails, with a user-facing message.</summary>
        public event EventHandler<string>? DeviceError;

        public IReadOnlyList<string> GetAvailableDevices()
        {
            try
            {
                return InputDevice.GetAll().Select(d => d.Name).ToList();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to enumerate MIDI input devices", ex);
                DeviceError?.Invoke(this, "Could not list MIDI devices.");
                return Array.Empty<string>();
            }
        }

        public bool Connect(string deviceName)
        {
            try
            {
                Disconnect();

                _inputDevice = InputDevice.GetByName(deviceName);
                _inputDevice.EventReceived += OnEventReceived;
                _inputDevice.StartEventsListening();
                _currentDeviceName = deviceName;

                Logger.Info($"Connected to MIDI device: {deviceName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to MIDI device '{deviceName}'", ex);
                DeviceError?.Invoke(this, $"Could not connect to MIDI device '{deviceName}'.");
                _inputDevice = null;
                _currentDeviceName = null;
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_inputDevice != null)
                {
                    _inputDevice.EventReceived -= OnEventReceived;
                    if (_inputDevice.IsListeningForEvents)
                    {
                        _inputDevice.StopEventsListening();
                    }
                    _inputDevice.Dispose();
                    _inputDevice = null;
                    _currentDeviceName = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error while disconnecting MIDI device", ex);
            }
        }

        public string? CurrentDeviceName => _currentDeviceName;

        private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
        {
            try
            {
                switch (e.Event)
                {
                    case ControlChangeEvent cc:
                        MessageReceived?.Invoke(this, new MidiValueEventArgs
                        {
                            Kind = MidiMessageKind.ControlChange,
                            Channel = cc.Channel,
                            ControllerOrNote = cc.ControlNumber,
                            Value = cc.ControlValue
                        });
                        break;

                    case NoteOnEvent noteOn:
                        MessageReceived?.Invoke(this, new MidiValueEventArgs
                        {
                            Kind = MidiMessageKind.NoteOn,
                            Channel = noteOn.Channel,
                            ControllerOrNote = noteOn.NoteNumber,
                            Value = noteOn.Velocity
                        });
                        break;

                    case NoteOffEvent noteOff:
                        MessageReceived?.Invoke(this, new MidiValueEventArgs
                        {
                            Kind = MidiMessageKind.NoteOff,
                            Channel = noteOff.Channel,
                            ControllerOrNote = noteOff.NoteNumber,
                            Value = noteOff.Velocity
                        });
                        break;

                    default:
                        // Ignore other MIDI event types (SysEx, clock, etc).
                        break;
                }
            }
            catch (Exception ex)
            {
                // A malformed or unexpected MIDI event must never crash the app.
                Logger.Error("Error handling incoming MIDI event", ex);
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
