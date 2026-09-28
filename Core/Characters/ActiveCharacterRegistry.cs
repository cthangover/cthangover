namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Static slot for the gameplay system that currently owns the active
    /// character (see <see cref="IActiveCharacterProvider"/>). The provider is
    /// set when the owning system enters its scene root and cleared when it
    /// leaves; core UI reads <see cref="ActiveCharacter"/> to decide whether
    /// action-point rules apply. The core holds no reference to the concrete
    /// system, so battle (or any other turn-based mode) stays entirely in mods.
    /// </summary>
    public static class ActiveCharacterRegistry
    {
        /// <summary>Current provider; <c>null</c> outside an active turn context.</summary>
        public static IActiveCharacterProvider Provider { get; set; }

        /// <summary>The acting character of the current provider, or <c>null</c>.</summary>
        public static Character ActiveCharacter => Provider?.ActiveCharacter;
    }
}
