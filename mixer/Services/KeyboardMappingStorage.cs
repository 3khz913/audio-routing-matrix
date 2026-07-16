using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using mixer.Models;

namespace mixer.Services
{
    public class KeyboardMappingStorage
    {
        private static readonly string _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mixer");
        private static readonly string _filePath = Path.Combine(_directory, "keyboard_mappings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private Dictionary<string, KeyboardBinding> _mappings = new();

        public KeyboardMappingStorage() => Load();

        public Dictionary<string, KeyboardBinding> GetAllMappings() => _mappings;

        public KeyboardBinding? GetBinding(string key)
        {
            return _mappings.TryGetValue(key, out var b) ? b : null;
        }

        public bool HasBinding(string key)
        {
            return _mappings.ContainsKey(key);
        }

        public void SetBinding(string key, KeyboardBinding binding)
        {
            _mappings[key] = binding;
            Save();
        }

        public void RemoveBinding(string key)
        {
            _mappings.Remove(key);
            Save();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var loaded = JsonSerializer.Deserialize<Dictionary<string, KeyboardBinding>>(json, JsonOptions);
                        if (loaded != null) _mappings = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load keyboard mappings", ex);
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(_filePath, JsonSerializer.Serialize(_mappings, JsonOptions));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save keyboard mappings", ex);
            }
        }
    }
}
