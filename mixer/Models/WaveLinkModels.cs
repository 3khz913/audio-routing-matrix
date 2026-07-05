using System.Collections.Generic;

namespace mixer.Models
{
    public class ServerMessage
    {
        public string Type { get; set; } = string.Empty;
        public StateData? Data { get; set; }
    }

    public class StateData
    {
        public List<InputDto> Inputs { get; set; } = new();
        public List<MixDto> Mixes { get; set; } = new();
        public List<CellDto> Cells { get; set; } = new();
    }

    public class InputDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Volume { get; set; }
        public bool IsMuted { get; set; }
    }

    public class MixDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsMuted { get; set; }  // 🆕 تمت الإضافة
    }

    public class CellDto
    {
        public string InputId { get; set; } = string.Empty;
        public string MixId { get; set; } = string.Empty;
        public double Volume { get; set; }
        public bool IsMuted { get; set; }
    }
}