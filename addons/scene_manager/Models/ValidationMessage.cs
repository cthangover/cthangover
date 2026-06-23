#if TOOLS
namespace SceneManagerAddon
{
    public sealed class ValidationMessage
    {
        public string Message { get; set; }
        public SeverityLevel Severity { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
    }
}
#endif
