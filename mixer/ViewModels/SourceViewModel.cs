using System;
using System.Collections.ObjectModel;

namespace mixer.ViewModels
{
    /// <summary>
    /// One row in the matrix: an audio Input/Source (e.g. "System", "Discord") with
    /// its own master volume + mute, plus one CellViewModel per Mix column.
    /// </summary>
    public class SourceViewModel : ViewModelBase
    {
        public string Id { get; }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        private double _masterVolume;
        private bool _updatingFromService;

        public double MasterVolume
        {
            get => _masterVolume;
            set
            {
                if (SetField(ref _masterVolume, value) && !_updatingFromService)
                {
                    MasterVolumeChangedByUser?.Invoke(this, value);
                }
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (SetField(ref _isMuted, value) && !_updatingFromService)
                {
                    MuteChangedByUser?.Invoke(this, value);
                }
            }
        }

        /// <summary>MIDI mapping key for the master volume/mute control of this source.</summary>
        public string MappingKey => $"{Id}|master";

        public ObservableCollection<CellViewModel> Cells { get; } = new();

        public event EventHandler<double>? MasterVolumeChangedByUser;
        public event EventHandler<bool>? MuteChangedByUser;

        public RelayCommand ToggleMuteCommand { get; }

        public SourceViewModel(string id, string name)
        {
            Id = id;
            _name = name;
            ToggleMuteCommand = new RelayCommand(_ => IsMuted = !IsMuted);
        }

        public CellViewModel? GetCell(string mixId)
        {
            foreach (var cell in Cells)
            {
                if (cell.MixId == mixId) return cell;
            }
            return null;
        }

        public void UpdateMasterVolumeFromService(double volume)
        {
            _updatingFromService = true;
            try { MasterVolume = volume; }
            finally { _updatingFromService = false; }
        }

        public void UpdateMuteFromService(bool isMuted)
        {
            _updatingFromService = true;
            try { IsMuted = isMuted; }
            finally { _updatingFromService = false; }
        }
    }
}
