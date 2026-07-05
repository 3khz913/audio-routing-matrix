using System;

namespace mixer.ViewModels
{
    public class CellViewModel : ViewModelBase
    {
        public string InputId { get; }
        public string MixId { get; }
        public string MappingKey => $"{InputId}|{MixId}";

        private double _volume;
        private bool _updatingFromService;

        public double Volume
        {
            get => _volume;
            set
            {
                if (SetField(ref _volume, value) && !_updatingFromService)
                    VolumeChangedByUser?.Invoke(this, value);
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (SetField(ref _isMuted, value) && !_updatingFromService)
                    MuteChangedByUser?.Invoke(this, value);
            }
        }

        public bool HasMidiMapping
        {
            get => _hasMidiMapping;
            set => SetField(ref _hasMidiMapping, value);
        }
        private bool _hasMidiMapping;

        public event EventHandler<double>? VolumeChangedByUser;
        public event EventHandler<bool>? MuteChangedByUser;
        public event EventHandler? EditRequested;

        public RelayCommand EditCommand { get; }
        public RelayCommand ToggleMuteCommand { get; }

        public CellViewModel(string inputId, string mixId)
        {
            InputId = inputId;
            MixId = mixId;

            EditCommand = new RelayCommand(_ => EditRequested?.Invoke(this, EventArgs.Empty));
            ToggleMuteCommand = new RelayCommand(_ => IsMuted = !IsMuted);
        }

        public void UpdateFromService(double volume)
        {
            _updatingFromService = true;
            try { Volume = volume; }
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