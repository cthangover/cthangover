namespace Cthangover.TestRunner
{
    /// <summary>Explains why a discovered suite is not automatically executable and lists the setup it needs.</summary>
    public class ManualTestPolicy
    {
        public string Reason { get; set; }
        public bool Windowed { get; set; }
        public string Game { get; set; }
        public string[] Requirements { get; set; } = Array.Empty<string>();
    }
}
