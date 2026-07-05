using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using mixer.Models;

namespace mixer.Services
{
    public class MidiMappingStorage
    {
        private static readonly string _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mixer");

        private static readonly string _filePath = Path.Combine(_directory, "midi_mappings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // التخزين: لكل مفتاح (مثل "sys|pm") قائمة من التعيينات
        private Dictionary<string, List<MidiMapping>> _mappings = new();

        public MidiMappingStorage()
        {
            Load();
        }

        /// <summary>
        /// يعيد جميع التعيينات كمصفوفة مسطحة للاستخدام في MainViewModel
        /// </summary>
        public IReadOnlyList<(string Key, MidiMapping Mapping)> GetAllMappings()
        {
            var flat = new List<(string Key, MidiMapping Mapping)>();
            foreach (var kvp in _mappings)
            {
                foreach (var mapping in kvp.Value)
                {
                    flat.Add((kvp.Key, mapping));
                }
            }
            return flat;
        }

        /// <summary>
        /// يعيد قائمة التعيينات لمفتاح محدد
        /// </summary>
        public IReadOnlyList<MidiMapping> GetMappings(string key)
        {
            return _mappings.TryGetValue(key, out var list) ? list : Array.Empty<MidiMapping>();
        }

        /// <summary>
        /// التحقق من وجود أي تعيين لمفتاح (مفيد لمؤشر "HasMidiMapping")
        /// </summary>
        public bool HasMapping(string key)
        {
            return _mappings.TryGetValue(key, out var list) && list.Count > 0;
        }

        /// <summary>
        /// إضافة تعيين جديد لمفتاح
        /// </summary>
        public void AddMapping(string key, MidiMapping mapping)
        {
            if (!_mappings.TryGetValue(key, out var list))
            {
                list = new List<MidiMapping>();
                _mappings[key] = list;
            }
            list.Add(mapping);
            Save();
        }

        /// <summary>
        /// حذف تعيين محدد من مفتاح
        /// </summary>
        public void RemoveMapping(string key, int index)
        {
            if (_mappings.TryGetValue(key, out var list))
            {
                if (index >= 0 && index < list.Count)
                {
                    list.RemoveAt(index);
                    if (list.Count == 0)
                    {
                        _mappings.Remove(key);
                    }
                    Save();
                }
            }
        }

        /// <summary>
        /// حذف جميع التعيينات لمفتاح
        /// </summary>
        public void ClearMappings(string key)
        {
            if (_mappings.Remove(key))
            {
                Save();
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
                        var loaded = JsonSerializer.Deserialize<Dictionary<string, List<MidiMapping>>>(json, JsonOptions);
                        if (loaded != null) _mappings = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load MIDI mappings", ex);
                _mappings = new Dictionary<string, List<MidiMapping>>();
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(_directory);
                var json = JsonSerializer.Serialize(_mappings, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save MIDI mappings", ex);
            }
        }
    }
}