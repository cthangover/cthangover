namespace Cthangover.Validator
{
    /// <summary>
    /// A declared entity discovered while collecting contracts (JSON id, asset
    /// file id, …). Declarations are matched against
    /// <see cref="ValidationReference"/> during reconciliation, and declarations
    /// of kinds with reliable reference coverage can be checked for being unused.
    /// </summary>
    public class ValidationDeclaration
    {
        public string Kind { get; set; }
        public string Id { get; set; }
        public string ModId { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
    }
}
