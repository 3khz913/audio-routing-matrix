namespace mixer.Models
{
    public class KeyInfo
    {
        public int VkCode { get; set; }
        public int ScanCode { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class KeyboardBinding
    {
        public string KeyboardDeviceId { get; set; } = string.Empty;
        public string KeyboardDeviceName { get; set; } = string.Empty;
        public KeyInfo? VolUp { get; set; }
        public KeyInfo? VolDown { get; set; }
        public KeyInfo? Mute { get; set; }
    }
}
