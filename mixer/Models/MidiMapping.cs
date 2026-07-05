namespace mixer.Models
{
    public enum MidiMessageKind
    {
        ControlChange,
        NoteOn,
        NoteOff
    }

    /// <summary>
    /// نوع عنصر التحكم الفيزيائي في جهاز MIDI
    /// </summary>
    public enum ControlType
    {
        Absolute,   // مقبض عادي أو فيدر (0–127)
        Relative,   // مقبض لا نهائي (قيم صغيرة للدوران)
        Button      // زر (NoteOn/Off)
    }

    /// <summary>
    /// الإجراء المطلوب تنفيذه عند استقبال الرسالة
    /// </summary>
    public enum MidiAction
    {
        SetLevel,           // تعيين مستوى الصوت
        ToggleMute,         // تبديل الكتم
        IncrementDecrement  // زيادة/نقصان نسبي
    }

    /// <summary>
    /// تعيين MIDI واحد. المفتاح (MappingKey) يُخزَّن خارج هذا الكائن.
    /// </summary>
    public class MidiMapping
    {
        public string DeviceName { get; set; } = string.Empty;
        public MidiMessageKind Kind { get; set; }
        public int Channel { get; set; }
        public int ControllerOrNote { get; set; }
        public ControlType ControlType { get; set; } = ControlType.Absolute;
        public MidiAction Action { get; set; } = MidiAction.SetLevel;
    }
}