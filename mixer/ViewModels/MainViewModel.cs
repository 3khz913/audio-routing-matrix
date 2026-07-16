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
        private readonly KeyboardMappingStorage _keyboardMappingStorage;
        private bool _initialized;

        public ObservableCollection<MixViewModel> Mixes { get; } = new();
        public ObservableCollection<SourceViewModel> Sources { get; } = new();

        public RelayCommand MuteAllCommand { get; }
        public RelayCommand UnmuteAllCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }

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

        private string _focusedAppText = "";
        public string FocusedAppText
        {
            get => _focusedAppText;
            set => SetField(ref _focusedAppText, value);
        }

        private string _micInfoText = "";
        public string MicInfoText
        {
            get => _micInfoText;
            set => SetField(ref _micInfoText, value);
        }

        public MainViewModel(WaveLinkService waveLinkService, MidiService midiService, MidiMappingStorage midiMappingStorage, KeyboardMappingStorage keyboardMappingStorage)
        {
            _waveLinkService = waveLinkService;
            _midiService = midiService;
            _midiMappingStorage = midiMappingStorage;
            _keyboardMappingStorage = keyboardMappingStorage;

            MuteAllCommand = new RelayCommand(_ => MuteAll());
            UnmuteAllCommand = new RelayCommand(_ => UnmuteAll());
            OpenSettingsCommand = new RelayCommand(_ => mixer.App.OpenSettings());

            _waveLinkService.StateReceived += OnStateReceived;
            _waveLinkService.ConnectionChanged += OnConnectionChanged;
            _waveLinkService.StatusReceived += OnStatusReceived;
            _waveLinkService.LevelMetersReceived += OnLevelMetersReceived;
            _waveLinkService.FocusedAppReceived += OnFocusedAppReceived;
            _midiService.MessageReceived += OnMidiMessageReceived;
        }

        private void OnConnectionChanged(bool connected)
        {
            RunOnUiThread(() =>
            {
                IsConnected = connected;
                StatusText = connected ? Loc.Get("Status.Connected") : Loc.Get("Status.Disconnected");
            });
        }

        private void OnStatusReceived(string status)
        {
            RunOnUiThread(() =>
            {
                StatusText = status switch
                {
                    "connected" => Loc.Get("Status.Connected"),
                    "disconnected" => Loc.Get("Status.WaveLinkDisconnected"),
                    "noWaveLink" => Loc.Get("Status.NoWaveLink"),
                    _ => StatusText
                };
            });
        }

        private void OnLevelMetersReceived(LevelMeterData data)
        {
            RunOnUiThread(() =>
            {
                foreach (var ch in data.Channels)
                {
                    var source = Sources.FirstOrDefault(s => s.Id == ch.Id);
                    if (source != null)
                    {
                        source.VuLeft = ch.Left;
                        source.VuRight = ch.Right;
                    }
                }
            });
        }

        private void OnFocusedAppReceived(FocusedAppData app)
        {
            RunOnUiThread(() =>
            {
                var channelName = Sources.FirstOrDefault(s => s.Id == app.ChannelId)?.Name ?? "";
                FocusedAppText = string.IsNullOrEmpty(channelName)
                    ? Loc.Get("Label.FocusedAppNoChannel", app.Name)
                    : Loc.Get("Label.FocusedApp", app.Name, channelName);
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

                // --- مزامنة المخاليط: إزالة المحذوفة، إضافة الجديدة ---
                var newMixIds = new HashSet<string>(data.Mixes.Select(m => m.Id));
                for (int i = Mixes.Count - 1; i >= 0; i--)
                {
                    if (!newMixIds.Contains(Mixes[i].Id))
                    {
                        Mixes.RemoveAt(i);
                    }
                }
                foreach (var mixDto in data.Mixes)
                {
                    var mixVm = Mixes.FirstOrDefault(m => m.Id == mixDto.Id);
                    if (mixVm == null)
                    {
                        mixVm = new MixViewModel(mixDto.Id, mixDto.Name);
                        mixVm.MuteChangedByUser += (_, isMuted) =>
                            _waveLinkService.SetMixMute(mixVm.Id, isMuted);
                        mixVm.EditRequested += (_, _) =>
                            RunOnUiThread(() => OpenEditWindowForMix(mixVm));
                        mixVm.HasMidiMapping = _midiMappingStorage.HasMapping(mixVm.MappingKey);
                        Mixes.Add(mixVm);
                    }
                    mixVm.Name = mixDto.Name;
                    mixVm.UpdateMuteFromService(mixDto.IsMuted);
                }

                // --- مزامنة المصادر: إزالة المحذوفة، إضافة الجديدة ---
                var newSourceIds = new HashSet<string>(data.Inputs.Select(i => i.Id));
                for (int i = Sources.Count - 1; i >= 0; i--)
                {
                    if (!newSourceIds.Contains(Sources[i].Id))
                    {
                        Sources.RemoveAt(i);
                    }
                }
                foreach (var inputDto in data.Inputs)
                {
                    var sourceVm = Sources.FirstOrDefault(s => s.Id == inputDto.Id);
                    if (sourceVm == null)
                    {
                        sourceVm = new SourceViewModel(inputDto.Id, inputDto.Name);
                        sourceVm.HasMidiMapping = _midiMappingStorage.HasMapping(sourceVm.MappingKey);
                        sourceVm.MasterVolumeChangedByUser += (_, volume) =>
                            _waveLinkService.SetInputVolumeDebounced(sourceVm.Id, volume);
                        sourceVm.MuteChangedByUser += (_, isMuted) =>
                            _waveLinkService.SetInputMute(sourceVm.Id, isMuted);
                        sourceVm.EditRequested += (_, _) =>
                            RunOnUiThread(() => OpenEditWindowForSource(sourceVm));

                        foreach (var mixVm in Mixes)
                        {
                            var cellVm = new CellViewModel(sourceVm.Id, mixVm.Id);
                            cellVm.HasMidiMapping = _midiMappingStorage.HasMapping(cellVm.MappingKey);
                            cellVm.VolumeChangedByUser += (_, volume) =>
                                _waveLinkService.SetInputMixVolumeDebounced(cellVm.InputId, cellVm.MixId, volume);
                            cellVm.MuteChangedByUser += (_, isMuted) =>
                                _waveLinkService.SetInputMixMute(cellVm.InputId, cellVm.MixId, isMuted);
                            cellVm.EditRequested += (_, _) =>
                                RunOnUiThread(() => OpenEditWindow(cellVm));
                            cellVm.AddToMixRequested += (_, _) =>
                                _waveLinkService.AddChannelToMix(cellVm.InputId, cellVm.MixId);
                            sourceVm.Cells.Add(cellVm);
                        }
                        Sources.Add(sourceVm);
                    }
                    sourceVm.Name = inputDto.Name;
                    sourceVm.UpdateMasterVolumeFromService(inputDto.Volume);
                    sourceVm.UpdateMuteFromService(inputDto.IsMuted);

                    // مزامنة خلايا المصدر (إزالة/إضافة حسب المخاليط الحالية)
                    for (int i = sourceVm.Cells.Count - 1; i >= 0; i--)
                    {
                        if (!newMixIds.Contains(sourceVm.Cells[i].MixId))
                        {
                            sourceVm.Cells.RemoveAt(i);
                        }
                    }
                    foreach (var mixVm in Mixes)
                    {
                        var existingCell = sourceVm.GetCell(mixVm.Id);
                        if (existingCell == null)
                        {
                            var cellVm = new CellViewModel(sourceVm.Id, mixVm.Id);
                            cellVm.HasMidiMapping = _midiMappingStorage.HasMapping(cellVm.MappingKey);
                            cellVm.VolumeChangedByUser += (_, volume) =>
                                _waveLinkService.SetInputMixVolumeDebounced(cellVm.InputId, cellVm.MixId, volume);
                            cellVm.MuteChangedByUser += (_, isMuted) =>
                                _waveLinkService.SetInputMixMute(cellVm.InputId, cellVm.MixId, isMuted);
                            cellVm.EditRequested += (_, _) =>
                                RunOnUiThread(() => OpenEditWindow(cellVm));
                            cellVm.AddToMixRequested += (_, _) =>
                                _waveLinkService.AddChannelToMix(cellVm.InputId, cellVm.MixId);
                            sourceVm.Cells.Add(cellVm);
                        }
                    }
                }

                // تحديث قيم الخلايا
                foreach (var cellDto in data.Cells)
                {
                    var sourceVm = Sources.FirstOrDefault(s => s.Id == cellDto.InputId);
                    var cellVm = sourceVm?.GetCell(cellDto.MixId);
                    if (cellVm == null) continue;
                    cellVm.UpdateFromService(cellDto.Volume);
                    cellVm.UpdateMuteFromService(cellDto.IsMuted);
                    cellVm.UpdateRoutedFromService(cellDto.Routed);
                }

                // تحديث معلومات الميكروفون
                if (data.MicDevices.Count > 0)
                {
                    var mic = data.MicDevices[0];
                    MicInfoText = Loc.Get("Label.Mic", mic.DeviceName, mic.Gain, mic.MicPcMix * 100);
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
                mixVm.EditRequested += (_, _) =>
                    RunOnUiThread(() => OpenEditWindowForMix(mixVm));
                mixVm.HasMidiMapping = _midiMappingStorage.HasMapping(mixVm.MappingKey);
                mixVm.HasKeyboardBinding = _keyboardMappingStorage.HasBinding(mixVm.MappingKey);
                Mixes.Add(mixVm);
            }

            // بناء المصادر وخلاياها
            foreach (var inputDto in data.Inputs)
            {
                var sourceVm = new SourceViewModel(inputDto.Id, inputDto.Name);
                sourceVm.UpdateMasterVolumeFromService(inputDto.Volume);
                sourceVm.UpdateMuteFromService(inputDto.IsMuted);
                sourceVm.HasMidiMapping = _midiMappingStorage.HasMapping(sourceVm.MappingKey);
                sourceVm.HasKeyboardBinding = _keyboardMappingStorage.HasBinding(sourceVm.MappingKey);

                sourceVm.MasterVolumeChangedByUser += (_, volume) =>
                    _waveLinkService.SetInputVolumeDebounced(sourceVm.Id, volume);
                sourceVm.MuteChangedByUser += (_, isMuted) =>
                    _waveLinkService.SetInputMute(sourceVm.Id, isMuted);
                sourceVm.EditRequested += (_, _) =>
                    RunOnUiThread(() => OpenEditWindowForSource(sourceVm));

                foreach (var mixVm in Mixes)
                {
                    var cellVm = new CellViewModel(sourceVm.Id, mixVm.Id);
                    var cellDto = data.Cells.FirstOrDefault(c => c.InputId == sourceVm.Id && c.MixId == mixVm.Id);
                    if (cellDto != null)
                    {
                        cellVm.UpdateFromService(cellDto.Volume);
                        cellVm.UpdateMuteFromService(cellDto.IsMuted);
                        cellVm.UpdateRoutedFromService(cellDto.Routed);
                    }

                    cellVm.HasMidiMapping = _midiMappingStorage.HasMapping(cellVm.MappingKey);
                    cellVm.HasKeyboardBinding = _keyboardMappingStorage.HasBinding(cellVm.MappingKey);

                    cellVm.VolumeChangedByUser += (_, volume) =>
                        _waveLinkService.SetInputMixVolumeDebounced(cellVm.InputId, cellVm.MixId, volume);
                    cellVm.MuteChangedByUser += (_, isMuted) =>
                        _waveLinkService.SetInputMixMute(cellVm.InputId, cellVm.MixId, isMuted);
                    cellVm.EditRequested += (_, _) =>
                    {
                        RunOnUiThread(() => OpenEditWindow(cellVm));
                    };
                    cellVm.AddToMixRequested += (_, _) =>
                        _waveLinkService.AddChannelToMix(cellVm.InputId, cellVm.MixId);

                    sourceVm.Cells.Add(cellVm);
                }
                Sources.Add(sourceVm);
            }
        }

        private void OpenEditWindow(CellViewModel cell)
        {
            var title = Loc.Get("Midi.Title", cell.InputId, cell.MixId);
            var editVM = new EditCellWindowViewModel(_midiService, _midiMappingStorage,
                App.KeyboardDeviceService, App.KeyboardMappingStorage, App.GlobalHotkeyService,
                cell.MappingKey, title);
            var window = new EditCellWindow(editVM) { Owner = System.Windows.Application.Current.MainWindow };
            window.ShowDialog();
            cell.HasMidiMapping = _midiMappingStorage.HasMapping(cell.MappingKey);
            cell.HasKeyboardBinding = _keyboardMappingStorage.HasBinding(cell.MappingKey);
            App.GlobalHotkeyService.SetBindings(_keyboardMappingStorage.GetAllMappings());
        }

        private void OpenEditWindowForSource(SourceViewModel source)
        {
            var title = Loc.Get("Midi.TitleMaster", source.Name);
            var editVM = new EditCellWindowViewModel(_midiService, _midiMappingStorage,
                App.KeyboardDeviceService, App.KeyboardMappingStorage, App.GlobalHotkeyService,
                source.MappingKey, title);
            var window = new EditCellWindow(editVM) { Owner = System.Windows.Application.Current.MainWindow };
            window.ShowDialog();
            source.HasMidiMapping = _midiMappingStorage.HasMapping(source.MappingKey);
            source.HasKeyboardBinding = _keyboardMappingStorage.HasBinding(source.MappingKey);
        }

        private void OpenEditWindowForMix(MixViewModel mix)
        {
            var title = Loc.Get("Midi.TitleMix", mix.Name);
            var editVM = new EditCellWindowViewModel(_midiService, _midiMappingStorage,
                App.KeyboardDeviceService, App.KeyboardMappingStorage, App.GlobalHotkeyService,
                mix.MappingKey, title);
            var window = new EditCellWindow(editVM) { Owner = System.Windows.Application.Current.MainWindow };
            window.ShowDialog();
            mix.HasMidiMapping = _midiMappingStorage.HasMapping(mix.MappingKey);
            mix.HasKeyboardBinding = _keyboardMappingStorage.HasBinding(mix.MappingKey);
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

        public void HandleKeyAction(string key, string action)
        {
            RunOnUiThread(() =>
            {
                var parts = key.Split('|');
                if (parts.Length != 2) return;
                var sourceId = parts[0];
                var target = parts[1];

                // Mix-level keys start with "mix|"
                if (key.StartsWith("mix|"))
                {
                    var mixId = key.Substring(4);
                    var mixVm = Mixes.FirstOrDefault(m => m.Id == mixId);
                    if (mixVm == null) return;
                    if (action == "mute")
                        mixVm.IsMuted = !mixVm.IsMuted;
                    return;
                }

                var sourceVm = Sources.FirstOrDefault(s => s.Id == sourceId);
                if (sourceVm == null) return;

                if (target == "master")
                {
                    switch (action)
                    {
                        case "volup":
                            sourceVm.MasterVolume = Math.Min(100, sourceVm.MasterVolume + 10);
                            break;
                        case "voldown":
                            sourceVm.MasterVolume = Math.Max(0, sourceVm.MasterVolume - 10);
                            break;
                        case "mute":
                            sourceVm.IsMuted = !sourceVm.IsMuted;
                            break;
                    }
                }
                else
                {
                    var cellVm = sourceVm.GetCell(target);
                    if (cellVm == null) return;
                    switch (action)
                    {
                        case "volup":
                            cellVm.Volume = Math.Min(100, cellVm.Volume + 10);
                            break;
                        case "voldown":
                            cellVm.Volume = Math.Max(0, cellVm.Volume - 10);
                            break;
                        case "mute":
                            cellVm.IsMuted = !cellVm.IsMuted;
                            break;
                    }
                }
            });
        }

        private void HandleMidiAction(string key, MidiMapping mapping, int rawValue)
        {
            RunOnUiThread(() =>
            {
                // مفاتيح المكس: "mix|{mixId}"
                if (key.StartsWith("mix|"))
                {
                    var mixId = key.Substring(4);
                    var mixVm = Mixes.FirstOrDefault(m => m.Id == mixId);
                    if (mixVm == null) return;

                    switch (mapping.Action)
                    {
                        case MidiAction.ToggleMute:
                            mixVm.IsMuted = !mixVm.IsMuted;
                            break;
                        case MidiAction.SetLevel:
                            mixVm.IsMuted = rawValue < 64;
                            break;
                    }
                    return;
                }

                // المفاتيح العادية: "{sourceId}|master" أو "{sourceId}|{mixId}"
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
                        int step = (rawValue >= 64) ? -1 : +1;
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

        private void MuteAll()
        {
            foreach (var source in Sources)
            {
                _waveLinkService.SetInputMute(source.Id, true);
                source.IsMuted = true;
            }
            foreach (var mix in Mixes)
            {
                _waveLinkService.SetMixMute(mix.Id, true);
                mix.IsMuted = true;
            }
        }

        private void UnmuteAll()
        {
            foreach (var source in Sources)
            {
                _waveLinkService.SetInputMute(source.Id, false);
                source.IsMuted = false;
            }
            foreach (var mix in Mixes)
            {
                _waveLinkService.SetMixMute(mix.Id, false);
                mix.IsMuted = false;
            }
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
    }
}