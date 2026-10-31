namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>Which kind of layer set a field of a resolved entity (see <see cref="EntityResolver.Explain"/>).</summary>
    public enum EntityLayer
    {
        /// <summary>The entity's definition file (the winning one when several mods define the id).</summary>
        Definition,

        /// <summary>A property patch (<c>patches/&lt;kind&gt;</c>), addressed or wildcard.</summary>
        Patch,
    }
}
