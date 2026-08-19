namespace mixer.Models
{
    public class KeyboardDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserAlias { get; set; } = string.Empty;

        public string DisplayName => string.IsNullOrWhiteSpace(UserAlias) ? Name : UserAlias;

        public override string ToString() => DisplayName;
    }
}