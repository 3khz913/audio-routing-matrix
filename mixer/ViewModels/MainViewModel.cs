using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using mixer.Models;
using mixer.Services;
using mixer.Views;

namespace mixer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly WaveLinkService _waveLinkService;
        private readonly MidiService _midiService;
        private readonly MidiMappingStorage _midiMappingStorage;
        private bool _initialized;

        public ObservableCollection<MixViewModel> Mixes { get; } = new();
        public ObservableCollection<SourceViewModel> Sources { get; } = new();

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetField(ref _isConnected, value);
        }

        private string _statusText = "Connecting...";
        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        public MainViewModel(WaveLinkService waveLinkService, MidiService midiService, MidiMappingStorage midiMappingStorage)
        {
            _waveLinkService = waveLinkService;
            _midiService = midiService;
            _midiMappingStorage = midiMappingStorage;

            _waveLinkService.StateReceived += OnStateReceived;
            _waveLinkService.ConnectionChanged += OnConnectionChanged;
            _midiService.MessageReceived += OnMidiMessageReceived;
        }

        private void OnConnectionChanged(bool connected)
        {
            RunOnUiThread(() =>
            {
                IsConnected = connected;
                StatusText = connected ? "Connected to Wave Link" : "Disconnected - retrying...";
            });
        }

        private void OnStateReceived(StateData data)
        {
            RunOnUiThread(() => ApplyState(data));
        }

        private void ApplyState(StateData data)
        {
            try
            {
                if (!_initialized)
                {
                    BuildInitialCollections(data);
                    _initialized = true;
                    return;
                }

                // تحديث المخاليط
                foreach (var mixDto in data.Mixes)
                {
                    var mixVm = Mixes.FirstOrDefault(m => m.Id == mixDto.Id);
                    if (mixVm == null) continue;
                    mixVm.Name = mixDto.Name;
                    mixVm.UpdateMuteFromService(mixDto.IsMuted);
                }

                // تحديث المصادر
                foreach (var inputDto in data.Inputs)
                {
                    var sourceVm = Sources.FirstOrDefault(s => s.Id == inputDto.Id);
                    if (sourceVm == null) continue;
                    sourceVm.Name = inputDto.Name;
                    sourceVm.UpdateMasterVolumeFromService(inputDto.Volume);
                    sourceVm.UpdateMuteFromService(inputDto.IsMuted);
                }

                // تحديث الخلايا
                foreach (var cellDto in data.Cells)
                {
                    var sourceVm = Sources.FirstOrDefault(s => s.Id == cellDto.InputId);
                    var cellVm = sourceVm?.GetCell(cellDto.MixId);
                    if (cellVm == null) continue;
                    cellVm.UpdateFromService(cellDto.Volume);
                    cellVm.UpdateMuteFromService(cellDto.IsMuted);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply state update from server", ex);
            }
        }

        private void BuildInitialCollections(StateData data)
        {
            Mixes.Clear();
            Sources.Clear();

            // بناء المخاليط
            foreach (var mixDto in data.Mixes)
            {
                var mixVm = new MixViewModel(mixDto.Id, mixDto.Name);
                mixVm.UpdateMuteFromService(mixDto.IsMuted);
                mixVm.MuteChangedByUser += (_, isMuted) =>
                    _waveLinkService.SetMixMute(mixVm.Id, isMuted);
                Mixes.Add(mixVm);
            }

            // بناء المصادر وخلاياها
            foreach (var inputDto in data.Inputs)
            {
                var sourceVm = new SourceViewModel(inputDto.Id, inputDto.Name);
                sourceVm.UpdateMasterVolumeFromService(inputDto.Volume);
                sourceVm.UpdateMuteFromService(inputDto.IsMuted);

                sourceVm.MasterVolumeChangedByUser += (_, volume) =>
                    _waveLinkService.SetInputVolumeDebounced(sourceVm.Id, volume);
                sourceVm.MuteChangedByUser += (_, isMuted) =>
                    _waveLinkService.SetInputMute(sourceVm.Id, isMuted);

                foreach (var mixVm in Mixes)
                {
                    var cellVm = new CellViewModel(sourceVm.Id, mixVm.Id);
                    var cellDto = data.Cells.FirstOrDefault(c => c.InputId == sourceVm.Id && c.MixId == mixVm.Id);
                    if (cellDto != null)
                    {
                        cellVm.UpdateFromService(cellDto.Volume);
                        cellVm.UpdateMuteFromService(cellDto.IsMuted);
                    }

                    cellVm.HasMidiMapping = _midiMappingStorage.HasMapping(cellVm.MappingKey);

                    cellVm.VolumeChangedByUser += (_, volume) =>
                        _waveLinkService.SetInputMixVolumeDebounced(cellVm.InputId, cellVm.MixId, volume);
                    cellVm.MuteChangedByUser += (_, isMuted) =>
                        _waveLinkService.SetInputMixMute(cellVm.InputId, cellVm.MixId, isMuted);
                    cellVm.EditRequested += (_, _) =>
                    {
                        RunOnUiThread(() => OpenEditWindow(cellVm));
                    };

                    sourceVm.Cells.Add(cellVm);
                }
                Sources.Add(sourceVm);
            }
        }

        private void OpenEditWindow(CellViewModel cell)
        {
            var title = $"Edit MIDI Mapping - {cell.InputId} → {cell.MixId}";
            var editVM = new EditCellWindowViewModel(_midiService, _midiMappingStorage, cell.MappingKey, title);
            var window = new EditCellWindow(editVM) { Owner = Application.Current.MainWindow };
            window.ShowDialog();
            cell.HasMidiMapping = _midiMappingStorage.HasMapping(cell.MappingKey);
        }

        private void OnMidiMessageReceived(object? sender, MidiValueEventArgs e)
        {
            try
            {
                foreach (var (key, mapping) in _midiMappingStorage.GetAllMappings())
                {
                    if (mapping.Kind != e.Kind ||
                        mapping.Channel != e.Channel ||
                        mapping.ControllerOrNote != e.ControllerOrNote)
                        continue;

                    HandleMidiAction(key, mapping, e.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error dispatching MIDI message to mappings", ex);
            }
        }

        private void HandleMidiAction(string key, MidiMapping mapping, int rawValue)
        {
            RunOnUiThread(() =>
            {
                // تحليل المفتاح
                var parts = key.Split('|');
                if (parts.Length != 2) return;
                var sourceId = parts[0];
                var target = parts[1];

                var sourceVm = Sources.FirstOrDefault(s => s.Id == sourceId);
                if (sourceVm == null) return;

                switch (mapping.Action)
                {
                    case MidiAction.SetLevel:
                        var scaled = Math.Round(rawValue / 127.0 * 100.0);
                        if (target == "master")
                            sourceVm.MasterVolume = scaled;
                        else
                        {
                            var cellVm = sourceVm.GetCell(target);
                            if (cellVm != null) cellVm.Volume = scaled;
                        }
                        break;

                    case MidiAction.ToggleMute:
                        if (target == "master")
                            sourceVm.IsMuted = !sourceVm.IsMuted;
                        else
                        {
                            var cellVm = sourceVm.GetCell(target);
                            if (cellVm != null) cellVm.IsMuted = !cellVm.IsMuted;
                        }
                        break;

                    case MidiAction.IncrementDecrement:
                        // القيم النسبية: 127 = نقصان (أو دوران يسار)، 1 = زيادة (يمين) حسب الجهاز
                        // بعض الأجهزة ترسل 127 للزيادة – سنعتمد على 127 كنقصان افتراضيًا
                        int step = (rawValue >= 64) ? -1 : +1; // منتصف النطاق
                        if (target == "master")
                        {
                            sourceVm.MasterVolume = Math.Max(0, Math.Min(100, sourceVm.MasterVolume + step));
                        }
                        else
                        {
                            var cellVm = sourceVm.GetCell(target);
                            if (cellVm != null)
                                cellVm.Volume = Math.Max(0, Math.Min(100, cellVm.Volume + step));
                        }
                        break;
                }
            });
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
    }
}