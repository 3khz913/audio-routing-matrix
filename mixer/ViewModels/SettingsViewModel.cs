using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using mixer.Services;

namespace mixer.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsStorage _storage;
        private readonly Action _applyTheme;
        private readonly Action _applyLanguage;

        public ObservableCollection<LanguageOption> Languages { get; } = new()
        {
            new LanguageOption("en", "English"),
            new LanguageOption("ar", "العربية")
        };

        public ObservableCollection<ThemeOption> Themes { get; } = new()
        {
            new ThemeOption("dark", "Dark"),
            new ThemeOption("light", "Light")
        };

        public LanguageOption? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetField(ref _selectedLanguage, value) && value != null)
                {
                    _storage.SetLanguage(value.Code);
                    _applyLanguage();
                }
            }
        }
        private LanguageOption? _selectedLanguage;

        public ThemeOption? SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetField(ref _selectedTheme, value) && value != null)
                {
                    _storage.SetTheme(value.Code);
                    _applyTheme();
                }
            }
        }
        private ThemeOption? _selectedTheme;

        public bool RunOnStartup
        {
            get => _runOnStartup;
            set
            {
                if (SetField(ref _runOnStartup, value))
                    _storage.SetRunOnStartup(value);
            }
        }
        private bool _runOnStartup;

        public ICommand CloseCommand { get; }
        public event Action? RequestClose;

        public SettingsViewModel(SettingsStorage storage, Action applyTheme, Action applyLanguage)
        {
            _storage = storage;
            _applyTheme = applyTheme;
            _applyLanguage = applyLanguage;

            var s = storage.Settings;
            _runOnStartup = s.RunOnStartup;

            foreach (var l in Languages)
                if (l.Code == s.Language) { _selectedLanguage = l; break; }
            _selectedLanguage ??= Languages[0];

            foreach (var t in Themes)
                if (t.Code == s.Theme) { _selectedTheme = t; break; }
            _selectedTheme ??= Themes[0];

            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }
    }

    public class LanguageOption
    {
        public string Code { get; }
        public string Name { get; }
        public LanguageOption(string code, string name) { Code = code; Name = name; }
    }

    public class ThemeOption
    {
        public string Code { get; }
        public string Name { get; }
        public ThemeOption(string code, string name) { Code = code; Name = name; }
    }
}
