using System;
using System.Collections.Generic;
using System.Management;
using mixer.Models;

namespace mixer.Services
{
    public class KeyboardDeviceService
    {
        public List<KeyboardDeviceInfo> GetKeyboards()
        {
            var keyboards = new List<KeyboardDeviceInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Keyboard");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var name = obj["Name"]?.ToString() ?? "";
                        var desc = obj["Description"]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        keyboards.Add(new KeyboardDeviceInfo
                        {
                            Id = obj["DeviceID"]?.ToString() ?? name,
                            Name = name,
                            Description = desc
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
    }
}
