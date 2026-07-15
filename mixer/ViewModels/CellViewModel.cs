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

        private bool _isRouted = true;
        public bool IsRouted
        {
            get => _isRouted;
            set => SetField(ref _isRouted, value);
        }

        public event EventHandler<double>? VolumeChangedByUser;
        public event EventHandler<bool>? MuteChangedByUser;
        public event EventHandler? EditRequested;
        public event EventHandler? AddToMixRequested;

        public RelayCommand EditCommand { get; }
        public RelayCommand ToggleMuteCommand { get; }
        public RelayCommand AddToMixCommand { get; }

        public CellViewModel(string inputId, string mixId)
        {
            InputId = inputId;
            MixId = mixId;

            EditCommand = new RelayCommand(_ => EditRequested?.Invoke(this, EventArgs.Empty));
            ToggleMuteCommand = new RelayCommand(_ => IsMuted = !IsMuted);
            AddToMixCommand = new RelayCommand(_ => AddToMixRequested?.Invoke(this, EventArgs.Empty));
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

        public void UpdateRoutedFromService(bool routed)
        {
            IsRouted = routed;
        }
    }
}