using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using mixer.Models;

namespace mixer.Services
{
    public class GlobalHotkeyService : IDisposable
    {
        private HwndSource? _hwndSource;
        private readonly Dictionary<string, KeyboardBinding> _bindings = new();

        public event Action<string, string>? KeyActionTriggered; // (mappingKey, action: volup/voldown/mute)
        public event Action<KeyInfo, string>? KeyLearned; // (key, devicePath)
        public bool IsLearning { get; set; }

        public void Start()
        {
            Stop();
            var parameters = new HwndSourceParameters("mixer_kb_hook")
            {
                Width = 0, Height = 0, WindowStyle = 0,
                ExtendedWindowStyle = unchecked((int)(WS_EX.TOOLWINDOW | WS_EX.TRANSPARENT | WS_EX.NOACTIVATE))
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            RegisterRawInputDevices(_hwndSource.Handle);
        }

        public void Stop()
        {
            if (_hwndSource == null) return;
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }

        public void SetBindings(Dictionary<string, KeyboardBinding> bindings)
        {
            _bindings.Clear();
            foreach (var kv in bindings)
                _bindings[kv.Key] = kv.Value;
        }

        private void RegisterRawInputDevices(IntPtr hwnd)
        {
            var rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x06;
            rid[0].dwFlags = RIDEV_INPUTSINK;
            rid[0].hwndTarget = hwnd;

            if (!RegisterRawInputDevices(rid, 1u, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
                Logger.Warn("Failed to register Raw Input.");
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_INPUT) ProcessRawInput(lParam);
            return IntPtr.Zero;
        }

        private void ProcessRawInput(IntPtr lParam)
        {
            uint size = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            if (size == 0) return;

            var buffer = new byte[size];
            var written = GetRawInputData(lParam, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            if (written != size) return;

            var header = MemoryMarshal.Read<RAWINPUTHEADER>(buffer.AsSpan(0, Marshal.SizeOf<RAWINPUTHEADER>()));
            if (header.dwType != RIM_TYPEKEYBOARD) return;

            var kbOffset = Marshal.SizeOf<RAWINPUTHEADER>();
            var kb = MemoryMarshal.Read<RAWKEYBOARD>(buffer.AsSpan(kbOffset, Marshal.SizeOf<RAWKEYBOARD>()));
            if ((kb.Flags & RI_KEY_BREAK) != 0) return;

            var vk = (int)kb.VKey;
            var sc = (int)kb.MakeCode;
            var devicePath = GetDeviceName(header.hDevice);

            if (IsLearning)
            {
                KeyLearned?.Invoke(new KeyInfo
                {
                    VkCode = vk,
                    ScanCode = sc,
                    DisplayName = VkToName(vk)
                }, devicePath);
                return;
            }

            foreach (var (key, binding) in _bindings)
            {
                // Filter by physical keyboard — only respond to the device that was used when learning
                if (!DeviceMatches(binding, devicePath))
                    continue;

                if (binding.VolUp != null && (binding.VolUp.VkCode == vk || binding.VolUp.ScanCode == sc))
                    KeyActionTriggered?.Invoke(key, "volup");
                else if (binding.VolDown != null && (binding.VolDown.VkCode == vk || binding.VolDown.ScanCode == sc))
                    KeyActionTriggered?.Invoke(key, "voldown");
                else if (binding.Mute != null && (binding.Mute.VkCode == vk || binding.Mute.ScanCode == sc))
                    KeyActionTriggered?.Invoke(key, "mute");
            }
        }

        /// <summary>
        /// Returns the stable raw-input device path for a device handle,
        /// e.g. \?\HID#VID_3151&PID_5029&MI_01&COL03#9&E9BC87E&0&0002
        /// </summary>
        private static string GetDeviceName(IntPtr hDevice)
        {
            if (hDevice == IntPtr.Zero) return "";
            try
            {
                uint size = 0;
                GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);
                if (size == 0) return "";
                var sb = new StringBuilder((int)size);
                GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, sb, ref size);
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Compares the runtime device path against the binding's stored device.
        /// Uses the exact RIDI device path captured at learn time; falls back to a
        /// normalized WMI DeviceID comparison for legacy bindings.
        /// </summary>
        private static bool DeviceMatches(KeyboardBinding binding, string runtimeDevicePath)
        {
            if (string.IsNullOrEmpty(runtimeDevicePath)) return false;

            // Primary: exact device path captured during Learn
            if (!string.IsNullOrEmpty(binding.DevicePath))
                return string.Equals(
                    NormalizeDevicePath(binding.DevicePath),
                    NormalizeDevicePath(runtimeDevicePath),
                    StringComparison.OrdinalIgnoreCase);

            // Fallback: normalized WMI DeviceID vs normalized runtime path
            if (!string.IsNullOrEmpty(binding.KeyboardDeviceId))
                return string.Equals(
                    NormalizeDevicePath(binding.KeyboardDeviceId),
                    NormalizeDevicePath(runtimeDevicePath),
                    StringComparison.OrdinalIgnoreCase);

            // No device info stored — cannot filter, treat as match (legacy behavior)
            return true;
        }

        /// <summary>
        /// Normalizes a device path / WMI ID so both formats are comparable:
        /// strips separators (\ # / ? : .), removes non-alphanumerics, uppercase.
        /// </summary>
        private static string NormalizeDevicePath(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var chars = value.Where(c => char.IsLetterOrDigit(c)).Select(char.ToUpperInvariant);
            return new string(chars.ToArray());
        }

        public static string VkToName(int vk) => vk switch
        {
            >= 0x30 and <= 0x39 => ((char)(vk - 0x30 + '0')).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
            0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
            0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
            0x20 => "Space", 0x0D => "Enter", 0x1B => "Esc",
            0x2D => "Insert", 0x2E => "Delete",
            0x24 => "Home", 0x23 => "End", 0x21 => "PgUp", 0x22 => "PgDn",
            0x25 => "Left", 0x27 => "Right", 0x26 => "Up", 0x28 => "Down",
            0x2C => "PrtScn", 0x13 => "Pause",
            0xA0 => "LShift", 0xA1 => "RShift", 0xA2 => "LCtrl", 0xA3 => "RCtrl",
            0xA4 => "LAlt", 0xA5 => "RAlt", 0x5B => "LWin", 0x5C => "RWin",
            0x6A => "Num*", 0x6B => "Num+", 0x6D => "Num-", 0x6E => "Num.", 0x6F => "Num/",
            0x60 => "Num0", 0x61 => "Num1", 0x62 => "Num2", 0x63 => "Num3", 0x64 => "Num4",
            0x65 => "Num5", 0x66 => "Num6", 0x67 => "Num7", 0x68 => "Num8", 0x69 => "Num9",
            _ => $"Key({vk:X2})"
        };

        public void Dispose() => Stop();

        private const int WM_INPUT = 0x00FF;
        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int RID_INPUT = 0x10000003;
        private const int RIM_TYPEKEYBOARD = 1;
        private const int RI_KEY_BREAK = 1;
        private const uint RIDI_DEVICENAME = 0x20000007;

        [Flags] private enum WS_EX : uint { TOOLWINDOW = 0x80, TRANSPARENT = 0x20, NOACTIVATE = 0x08000000 }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] p, uint n, uint s);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr h, uint cmd, IntPtr p, ref uint sz, uint hdr);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr h, uint cmd, byte[] p, ref uint sz, uint hdr);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, StringBuilder pData, ref uint pcbSize);
    }
}
