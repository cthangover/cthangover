namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Classifies the type of resource that the first positional argument of a
    /// scenario command refers to. Used for dependency tracking — the build tool
    /// can scan commands by their <see cref="ICommandReferenceMetadata"/> to
    /// discover which assets (backgrounds, scenes, music, etc.) are referenced
    /// and need to be packed into the final build.
    /// </summary>
    public enum PositionalReferenceKind
    {
        /// <summary>No resource reference in the first positional argument.</summary>
        None,
        /// <summary>A background image resource path.</summary>
        Background,
        /// <summary>A scene resource path (for scene switching).</summary>
        Scene,
        /// <summary>A music/audio resource path.</summary>
        Music,
        /// <summary>A sound effect resource path.</summary>
        Sound,
        /// <summary>An effect/shader resource path.</summary>
        Effect,
        /// <summary>An interactive action definition ID.</summary>
        Action
    }

    /// <summary>
    /// Optional interface for command strategies whose first positional argument
    /// references an external resource. Strategies implementing this interface
    /// are inspected by the build pipeline to collect asset dependencies.
    /// </summary>
    public interface ICommandReferenceMetadata
    {
        /// <summary>
        /// The kind of resource referenced by the first positional argument of this command.
        /// </summary>
        PositionalReferenceKind Positional0Kind { get; }
    }
}
