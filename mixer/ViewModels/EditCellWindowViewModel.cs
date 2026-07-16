using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using mixer.Models;
using mixer.Services;

namespace mixer.ViewModels
{
    public class MidiMappingViewModel : ViewModelBase
    {
        public MidiMapping Mapping { get; }
        public string DisplayText { get; }

        public MidiMappingViewModel(MidiMapping mapping)
        {
            Mapping = mapping;
            DisplayText = $"{mapping.DeviceName}: {mapping.ControlType} Ch{mapping.Channel} CC{mapping.ControllerOrNote} -> {mapping.Action}";
        }
    }

    public class EditCellWindowViewModel : ViewModelBase
    {
        private readonly MidiService _midiService;
        private readonly MidiMappingStorage _storage;
        private readonly KeyboardDeviceService _kbService;
        private readonly KeyboardMappingStorage _kbStorage;
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly string _mappingKey;

        public string Title { get; }

        // ─── MIDI ───
        public ObservableCollection<string> AvailableDevices { get; } = new();
        public ObservableCollection<string> ControlTypes { get; } = new() { "Absolute", "Relative", "Button" };
        public ObservableCollection<string> Actions { get; } = new() { "SetLevel", "ToggleMute", "IncrementDecrement" };

        private string? _selectedDevice;
        public string? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetField(ref _selectedDevice, value) && value != null)
                    ConnectToDevice(value);
            }
        }

        private string _selectedControlType = "Absolute";
        public string SelectedControlType
        {
            get => _selectedControlType;
            set => SetField(ref _selectedControlType, value);
        }

        private string _selectedAction = "SetLevel";
        public string SelectedAction
        {
            get => _selectedAction;
            set => SetField(ref _selectedAction, value);
        }

        public ObservableCollection<MidiMappingViewModel> Mappings { get; } = new();

        private MidiMappingViewModel? _selectedMapping;
        public MidiMappingViewModel? SelectedMapping
        {
            get => _selectedMapping;
            set => SetField(ref _selectedMapping, value);
        }

        private bool _isLearning;
        public bool IsLearning
        {
            get => _isLearning;
            set => SetField(ref _isLearning, value);
        }

        private string _learnStatus = "";
        public string LearnStatus
        {
            get => _learnStatus;
            set => SetField(ref _learnStatus, value);
        }

        public ICommand StartLearnCommand { get; }
        public ICommand DeleteMappingCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ImportCommand { get; }

        // ─── Keyboard Shortcuts ───
        public ObservableCollection<KeyboardDeviceInfo> AvailableKeyboards { get; } = new();

        private KeyboardDeviceInfo? _selectedKeyboard;
        public KeyboardDeviceInfo? SelectedKeyboard
        {
            get => _selectedKeyboard;
            set => SetField(ref _selectedKeyboard, value);
        }

        private string _volUpText = "Not set";
        public string VolUpText
        {
            get => _volUpText;
            set => SetField(ref _volUpText, value);
        }

        private string _volDownText = "Not set";
        public string VolDownText
        {
            get => _volDownText;
            set => SetField(ref _volDownText, value);
        }

        private string _muteText = "Not set";
        public string MuteText
        {
            get => _muteText;
            set => SetField(ref _muteText, value);
        }

        private bool _isLearningKb;
        public bool IsLearningKb
        {
            get => _isLearningKb;
            set => SetField(ref _isLearningKb, value);
        }

        public ICommand LearnVolUpCommand { get; }
        public ICommand LearnVolDownCommand { get; }
        public ICommand LearnMuteCommand { get; }
        public ICommand ClearKbCommand { get; }

        private string? _kbSlot;

        public event Action? RequestClose;

        public EditCellWindowViewModel(
            MidiService midiService, MidiMappingStorage storage,
            KeyboardDeviceService kbService, KeyboardMappingStorage kbStorage, GlobalHotkeyService hotkeyService,
            string mappingKey, string title)
        {
            _midiService = midiService;
            _storage = storage;
            _kbService = kbService;
            _kbStorage = kbStorage;
            _hotkeyService = hotkeyService;
            _mappingKey = mappingKey;
            Title = title;

            StartLearnCommand = new RelayCommand(_ => ToggleLearn());
            DeleteMappingCommand = new RelayCommand(_ => DeleteSelectedMapping(), _ => SelectedMapping != null);
            ExportCommand = new RelayCommand(_ => ExportMappings());
            ImportCommand = new RelayCommand(_ => ImportMappings());

            LearnVolUpCommand = new RelayCommand(_ => StartKbLearn("volup"));
            LearnVolDownCommand = new RelayCommand(_ => StartKbLearn("voldown"));
            LearnMuteCommand = new RelayCommand(_ => StartKbLearn("mute"));
            ClearKbCommand = new RelayCommand(_ => ClearKeyboardBinding());

            _hotkeyService.KeyLearned += OnKeyLearned;
            LoadDevices();
            LoadMappings();
            LoadKeyboardDevices();
            LoadKeyboardBinding();
        }

        // ─── MIDI Methods (unchanged) ───
        private void LoadDevices()
        {
            AvailableDevices.Clear();
            foreach (var name in _midiService.GetAvailableDevices())
                AvailableDevices.Add(name);

            if (_midiService.CurrentDeviceName != null && AvailableDevices.Contains(_midiService.CurrentDeviceName))
            {
                _selectedDevice = _midiService.CurrentDeviceName;
                OnPropertyChanged(nameof(SelectedDevice));
            }
        }

        private void ConnectToDevice(string deviceName)
        {
            try
            {
                _midiService.Connect(deviceName);
                LearnStatus = $"Connected to {deviceName}.";
            }
            catch (Exception ex)
            {
                Logger.Error("Error connecting to MIDI device", ex);
                LearnStatus = $"Could not connect to {deviceName}.";
            }
        }

        private void LoadMappings()
        {
            Mappings.Clear();
            foreach (var mapping in _storage.GetMappings(_mappingKey))
                Mappings.Add(new MidiMappingViewModel(mapping));
        }

        private void ToggleLearn()
        {
            if (IsLearning) StopLearn();
            else StartLearn();
        }

        private void StartLearn()
        {
            if (SelectedDevice == null) { LearnStatus = "Select a MIDI device first."; return; }
            try
            {
                _midiService.Connect(SelectedDevice);
                _midiService.MessageReceived += OnLearnMessageReceived;
                IsLearning = true;
                LearnStatus = "Move a knob/fader or press a button...";
            }
            catch (Exception ex) { Logger.Error("Error starting MIDI learn", ex); }
        }

        private void StopLearn()
        {
            _midiService.MessageReceived -= OnLearnMessageReceived;
            IsLearning = false;
            LearnStatus = "Learn stopped.";
        }

        private void OnLearnMessageReceived(object? sender, MidiValueEventArgs e)
        {
            _midiService.MessageReceived -= OnLearnMessageReceived;
            _storage.AddMapping(_mappingKey, new MidiMapping
            {
                DeviceName = SelectedDevice ?? "",
                Kind = e.Kind, Channel = e.Channel, ControllerOrNote = e.ControllerOrNote,
                ControlType = Enum.TryParse<ControlType>(SelectedControlType, out var ct) ? ct : ControlType.Absolute,
                Action = Enum.TryParse<MidiAction>(SelectedAction, out var act) ? act : MidiAction.SetLevel
            });
            LoadMappings();
            IsLearning = false;
            LearnStatus = $"Mapping added.";
        }

        private void DeleteSelectedMapping()
        {
            if (SelectedMapping == null) return;
            int index = Mappings.IndexOf(SelectedMapping);
            if (index >= 0) { _storage.RemoveMapping(_mappingKey, index); LoadMappings(); LearnStatus = "Mapping deleted."; }
        }

        // ─── Keyboard Methods ───
        private void LoadKeyboardDevices()
        {
            AvailableKeyboards.Clear();
            foreach (var kb in _kbService.GetKeyboards())
                AvailableKeyboards.Add(kb);
        }

        private void LoadKeyboardBinding()
        {
            var binding = _kbStorage.GetBinding(_mappingKey);
            if (binding == null) return;

            if (binding.VolUp != null) VolUpText = binding.VolUp.DisplayName;
            if (binding.VolDown != null) VolDownText = binding.VolDown.DisplayName;
            if (binding.Mute != null) MuteText = binding.Mute.DisplayName;

            var savedKb = AvailableKeyboards.FirstOrDefault(k => k.Id == binding.KeyboardDeviceId);
            if (savedKb != null) _selectedKeyboard = savedKb;
        }

        private void StartKbLearn(string slot)
        {
            if (_selectedKeyboard == null) { LearnStatus = "Select a keyboard first."; return; }
            StopKbLearn();
            _kbSlot = slot;
            _hotkeyService.IsLearning = true;
            IsLearningKb = true;
            LearnStatus = $"Press a key for {slot}...";
        }

        private void StopKbLearn()
        {
            _hotkeyService.IsLearning = false;
            _kbSlot = null;
            IsLearningKb = false;
        }

        private void OnKeyLearned(KeyInfo key)
        {
            if (_kbSlot == null) return;

            var binding = _kbStorage.GetBinding(_mappingKey) ?? new KeyboardBinding();

            if (_selectedKeyboard != null)
            {
                binding.KeyboardDeviceId = _selectedKeyboard.Id;
                binding.KeyboardDeviceName = _selectedKeyboard.Name;
            }

            switch (_kbSlot)
            {
                case "volup": binding.VolUp = key; VolUpText = key.DisplayName; break;
                case "voldown": binding.VolDown = key; VolDownText = key.DisplayName; break;
                case "mute": binding.Mute = key; MuteText = key.DisplayName; break;
            }

            _kbStorage.SetBinding(_mappingKey, binding);
            _hotkeyService.SetBindings(_kbStorage.GetAllMappings());
            StopKbLearn();
            LearnStatus = $"Key {key.DisplayName} learned for {_kbSlot}.";
        }

        private void ClearKeyboardBinding()
        {
            _kbStorage.RemoveBinding(_mappingKey);
            _hotkeyService.SetBindings(_kbStorage.GetAllMappings());
            VolUpText = "Not set";
            VolDownText = "Not set";
            MuteText = "Not set";
            LearnStatus = "Keyboard binding cleared.";
        }

        public void Cleanup()
        {
            _midiService.MessageReceived -= OnLearnMessageReceived;
            _hotkeyService.KeyLearned -= OnKeyLearned;
            StopKbLearn();
        }

        private void ExportMappings()
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = "midi_mappings.json", DefaultExt = ".json" };
                if (dlg.ShowDialog() == true) { _storage.SaveToFile(dlg.FileName); LearnStatus = "Exported."; }
            }
            catch (Exception ex) { Logger.Error("Export failed", ex); }
        }

        private void ImportMappings()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON files (*.json)|*.json", DefaultExt = ".json" };
                if (dlg.ShowDialog() == true) { _storage.LoadFromFile(dlg.FileName); LoadMappings(); LearnStatus = "Imported."; }
            }
            catch (Exception ex) { Logger.Error("Import failed", ex); }
        }
    }
}
