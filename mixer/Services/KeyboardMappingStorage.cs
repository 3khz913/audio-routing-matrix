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
        private static readonly string _aliasesPath = Path.Combine(_directory, "keyboard_aliases.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private Dictionary<string, KeyboardBinding> _mappings = new();
        private Dictionary<string, string> _aliases = new();

        public KeyboardMappingStorage() { Load(); LoadAliases(); }

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

        // ─── Keyboard aliases (user-defined names) ───

        public string? GetAlias(string keyboardId)
        {
            return _aliases.TryGetValue(keyboardId, out var a) && !string.IsNullOrWhiteSpace(a) ? a : null;
        }

        public void SetAlias(string keyboardId, string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                _aliases.Remove(keyboardId);
            }
            else
            {
                _aliases[keyboardId] = alias.Trim();
            }
            SaveAliases();
        }

        public Dictionary<string, string> GetAllAliases() => _aliases;

        private void LoadAliases()
        {
            try
            {
                if (File.Exists(_aliasesPath))
                {
                    var json = File.ReadAllText(_aliasesPath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
                        if (loaded != null) _aliases = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load keyboard aliases", ex);
            }
        }

        private void SaveAliases()
        {
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(_aliasesPath, JsonSerializer.Serialize(_aliases, JsonOptions));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save keyboard aliases", ex);
            }
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
