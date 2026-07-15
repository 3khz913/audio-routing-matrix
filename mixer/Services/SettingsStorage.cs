using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using mixer.Models;

namespace mixer.Services
{
    public class SettingsStorage
    {
        private static readonly string _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mixer");

        private static readonly string _filePath = Path.Combine(_directory, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private AppSettings _settings = new();

        public AppSettings Settings => _settings;

        public SettingsStorage()
        {
            Load();
            ApplyStartup();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded != null) _settings = loaded;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings", ex);
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(_directory);
                var json = JsonSerializer.Serialize(_settings, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
            }
        }

        public void SetLanguage(string lang)
        {
            _settings.Language = lang;
            Save();
        }

        public void SetTheme(string theme)
        {
            _settings.Theme = theme;
            Save();
        }

        public void SetRunOnStartup(bool enabled)
        {
            _settings.RunOnStartup = enabled;
            Save();
            ApplyStartup();
        }

        private void ApplyStartup()
        {
            try
            {
                var appName = "mixer";
                var exePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "mixer.exe");

                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);

                if (key == null) return;

                if (_settings.RunOnStartup)
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update startup registry", ex);
            }
        }
    }
}
