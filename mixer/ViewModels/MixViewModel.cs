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

        /// <summary>يُرفع عندما يغير المستخدم حالة الكتم (وليس تحديثاً من الخدمة).</summary>
        public event EventHandler<bool>? MuteChangedByUser;

        /// <summary>لتحديث الكتم من الخدمة دون إعادة إرسال.</summary>
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
        }
    }
}