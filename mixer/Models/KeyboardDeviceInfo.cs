namespace mixer.Models
{
    public class KeyboardDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public override string ToString() => $"{Name} ({Description})";
    }
}
