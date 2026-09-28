namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Neutral provider for the character that currently acts on behalf of the
    /// player. Turn-based battle is the original consumer, but the core only
    /// needs to know that some gameplay system can hand out an active actor —
    /// for example the inventory panel reads the actor's action points and
    /// charges item usage to them. A mod that owns such a system implements
    /// this interface and publishes itself through <see cref="ActiveCharacterRegistry"/>.
    /// </summary>
    public interface IActiveCharacterProvider
    {
        /// <summary>The acting character, or <c>null</c> when no action context is active.</summary>
        Character ActiveCharacter { get; }
    }
}
