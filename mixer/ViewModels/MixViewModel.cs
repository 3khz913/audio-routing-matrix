using System;

namespace mixer.ViewModels
{
    public class MixViewModel : ViewModelBase
    {
        public string Id { get; }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
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
        private bool _updatingFromService;

        public string MappingKey => $"mix|{Id}";

        public event EventHandler<bool>? MuteChangedByUser;
        public event EventHandler? EditRequested;

        public RelayCommand EditCommand { get; }

        public bool HasMidiMapping
        {
            get => _hasMidiMapping;
            set => SetField(ref _hasMidiMapping, value);
        }
        private bool _hasMidiMapping;

        public bool HasKeyboardBinding
        {
            get => _hasKeyboardBinding;
            set => SetField(ref _hasKeyboardBinding, value);
        }
        private bool _hasKeyboardBinding;

        public void UpdateMuteFromService(bool isMuted)
        {
            _updatingFromService = true;
            try { IsMuted = isMuted; }
            finally { _updatingFromService = false; }
        }

        public MixViewModel(string id, string name)
        {
            Id = id;
            _name = name;
            EditCommand = new RelayCommand(_ => EditRequested?.Invoke(this, EventArgs.Empty));
        }
    }
}