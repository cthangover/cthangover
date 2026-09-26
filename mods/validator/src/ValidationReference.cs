namespace Cthangover.Validator
{
    /// <summary>
    /// A reference from one entity to another (loot item, default background,
    /// character action, …). Collected by contract strategies and matched
    /// against declarations during reconciliation.
    /// </summary>
    public class ValidationReference
    {
        public string Kind { get; set; }
        public string TargetId { get; set; }
        public string ModId { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
    }
}
