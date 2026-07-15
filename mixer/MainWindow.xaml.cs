using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using mixer.Services;

namespace mixer
{
    public partial class MainWindow : Window
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mixer", "window_settings.json");

        public MainWindow()
        {
            InitializeComponent();
            LoadWindowSettings();
        }

        private void LoadWindowSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var s = JsonSerializer.Deserialize<WindowSettings>(json);
                    if (s != null && s.Width > 0 && s.Height > 0)
                    {
                        Left = Math.Max(0, s.Left);
                        Top = Math.Max(0, s.Top);
                        Width = s.Width;
                        Height = s.Height;
                        WindowState = s.Maximized ? WindowState.Maximized : WindowState.Normal;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to load window settings: {ex.Message}");
            }
        }

        private void SaveWindowSettings()
        {
            try
            {
                if (WindowState == WindowState.Normal)
                {
                    var s = new WindowSettings
                    {
                        Left = Left,
                        Top = Top,
                        Width = Width,
                        Height = Height,
                        Maximized = false
                    };
                    var dir = Path.GetDirectoryName(SettingsPath);
                    if (dir != null) Directory.CreateDirectory(dir);
                    File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s));
                }
                else
                {
                    var s = new WindowSettings { Maximized = true };
                    var dir = Path.GetDirectoryName(SettingsPath);
                    if (dir != null) Directory.CreateDirectory(dir);
                    File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save window settings", ex);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveWindowSettings();
            base.OnClosed(e);
        }

        private class WindowSettings
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public bool Maximized { get; set; }
        }
    }
}