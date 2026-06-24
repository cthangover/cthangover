namespace Cthangover.Core.Scenarios
{
    public enum PositionalReferenceKind
    {
        None,
        Background,
        Scene,
        Music,
        Sound,
        Effect,
        Action
    }

    public interface ICommandReferenceMetadata
    {
        PositionalReferenceKind Positional0Kind { get; }
    }
}
