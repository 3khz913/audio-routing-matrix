using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using mixer.Models;

namespace mixer.Services
{
    public class KeyboardDeviceService
    {
        public List<KeyboardDeviceInfo> GetKeyboards()
        {
            var keyboards = new List<KeyboardDeviceInfo>();
            var seenIds = new HashSet<string>();

            try
            {
                // Win32_PnPEntity with PNPClass='Keyboard' returns unique keyboard devices
                // (better than Win32_Keyboard which duplicates via filter drivers)
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID, PNPClass, Service FROM Win32_PnPEntity WHERE PNPClass = 'Keyboard'");

                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var deviceId = obj["DeviceID"]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(deviceId)) continue;

                        // Skip duplicate USB parent entries (e.g. USB\VID_...&MI_xx)
                        if (deviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)) continue;

                        // Deduplicate by the hardware instance path
                        if (!seenIds.Add(deviceId)) continue;

                        var name = obj["Name"]?.ToString() ?? "";
                        var description = obj["PNPClass"]?.ToString() ?? "";

                        keyboards.Add(new KeyboardDeviceInfo
                        {
                            Id = deviceId,
                            Name = BuildNeutralName(deviceId),
                            Description = name
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to enumerate keyboards", ex);
            }

            return keyboards;
        }

        /// <summary>
        /// Generates a neutral, human-readable name from the device hardware path.
        /// No assumptions about device type (laptop/desktop/brand) — purely technical.
        /// </summary>
        private static string BuildNeutralName(string deviceId)
        {
            // Extract VID/PID if present (e.g. "HID\VID_3151&PID_5029&MI_01&COL03\..." -> VID 3151, PID 5029)
            var vidMatch = Regex.Match(deviceId, @"VID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
            var pidMatch = Regex.Match(deviceId, @"PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);

            if (vidMatch.Success)
            {
                var vid = vidMatch.Groups[1].Value.ToUpperInvariant();
                var pid = pidMatch.Success ? pidMatch.Groups[1].Value.ToUpperInvariant() : "????";
                return $"Keyboard VID:{vid} PID:{pid}";
            }

            // No VID/PID — show the interface/collection portion for identification
            // e.g. "HID\UVHID&COL04\..." -> "UVHID&COL04"
            var parts = deviceId.Split('\\');
            var key = parts.Length > 1 ? parts[1] : deviceId;
            return $"HID Keyboard ({key})";
        }
    }
}