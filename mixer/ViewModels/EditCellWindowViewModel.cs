using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using mixer.Models;
using mixer.Services;

namespace mixer.ViewModels
{
    /// <summary>
    /// غلاف عرض تعيين MIDI في القائمة
    /// </summary>
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

    /// <summary>
    /// فيو مودل نافذة تحرير تعيينات MIDI لخلية/مصدر واحد.
    /// </summary>
    public class EditCellWindowViewModel : ViewModelBase
    {
        private readonly MidiService _midiService;
        private readonly MidiMappingStorage _storage;
        private readonly string _mappingKey;

        public string Title { get; }

        // قوائم الاختيار
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

        // قائمة التعيينات الحالية
        public ObservableCollection<MidiMappingViewModel> Mappings { get; } = new();

        private MidiMappingViewModel? _selectedMapping;
        public MidiMappingViewModel? SelectedMapping
        {
            get => _selectedMapping;
            set => SetField(ref _selectedMapping, value);
        }

        // حالة التعلم
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

        // أوامر
        public ICommand StartLearnCommand { get; }
        public ICommand DeleteMappingCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ImportCommand { get; }

        // حدث لإغلاق النافذة
        public event Action? RequestClose;

        public EditCellWindowViewModel(MidiService midiService, MidiMappingStorage storage, string mappingKey, string title)
        {
            _midiService = midiService;
            _storage = storage;
            _mappingKey = mappingKey;
            Title = title;

            StartLearnCommand = new RelayCommand(_ => ToggleLearn());
            DeleteMappingCommand = new RelayCommand(_ => DeleteSelectedMapping(), _ => SelectedMapping != null);
            ExportCommand = new RelayCommand(_ => ExportMappings());
            ImportCommand = new RelayCommand(_ => ImportMappings());

            LoadDevices();
            LoadMappings();
        }

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
            if (IsLearning)
                StopLearn();
            else
                StartLearn();
        }

        private void StartLearn()
        {
            if (SelectedDevice == null)
            {
                LearnStatus = "Please select a MIDI device first.";
                return;
            }

            try
            {
                _midiService.Connect(SelectedDevice);
                _midiService.MessageReceived += OnLearnMessageReceived;
                IsLearning = true;
                LearnStatus = "Move a knob/fader or press a button...";
            }
            catch (Exception ex)
            {
                Logger.Error("Error starting MIDI learn", ex);
                LearnStatus = $"Learn failed: {ex.Message}";
            }
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

            var mapping = new MidiMapping
            {
                DeviceName = SelectedDevice ?? "",
                Kind = e.Kind,
                Channel = e.Channel,
                ControllerOrNote = e.ControllerOrNote,
                ControlType = Enum.TryParse<ControlType>(SelectedControlType, out var ct) ? ct : ControlType.Absolute,
                Action = Enum.TryParse<MidiAction>(SelectedAction, out var act) ? act : MidiAction.SetLevel
            };

            _storage.AddMapping(_mappingKey, mapping);
            LoadMappings();

            IsLearning = false;
            LearnStatus = $"Mapping added: {SelectedControlType} -> {SelectedAction}";
        }

        private void DeleteSelectedMapping()
        {
            if (SelectedMapping == null) return;
            int index = Mappings.IndexOf(SelectedMapping);
            if (index >= 0)
            {
                _storage.RemoveMapping(_mappingKey, index);
                LoadMappings();
                LearnStatus = "Mapping deleted.";
            }
        }

        public void Cleanup()
        {
            _midiService.MessageReceived -= OnLearnMessageReceived;
        }

        private void ExportMappings()
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    FileName = "midi_mappings.json",
                    DefaultExt = ".json"
                };
                if (dlg.ShowDialog() == true)
                {
                    _storage.SaveToFile(dlg.FileName);
                    LearnStatus = $"Mappings exported to {System.IO.Path.GetFileName(dlg.FileName)}.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export MIDI mappings", ex);
                LearnStatus = "Export failed.";
            }
        }

        private void ImportMappings()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    DefaultExt = ".json"
                };
                if (dlg.ShowDialog() == true)
                {
                    _storage.LoadFromFile(dlg.FileName);
                    LoadMappings();
                    LearnStatus = $"Mappings imported. {Mappings.Count} mapping(s) loaded.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to import MIDI mappings", ex);
                LearnStatus = "Import failed. Check log for details.";
            }
        }
    }
}