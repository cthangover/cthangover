namespace Cthangover.Core.Bootstrap
{
    /// <summary>
    /// A mod-derived registry that can be rebuilt from the currently loaded
    /// mod set and cleared back to its empty state. Registries register
    /// themselves in <see cref="AppServices"/> (explicit list in its
    /// constructor), which turns mod reload and boot from hand-maintained
    /// clear-lists into a data-driven loop: adding a registry is one line in
    /// <see cref="AppServices"/>, with no edits in <c>ModManager.Reload</c>.
    /// <para/>
    /// Implementation contract: the instance constructor must stay cheap and
    /// side-effect free (no Godot API, no mod reads); <see cref="Rebuild"/>
    /// fills the registry from the loaded mod set and must be safe to call
    /// again without a preceding <see cref="Clear"/>; <see cref="Clear"/>
    /// returns the registry to its just-constructed state.
    /// </summary>
    public interface IGameRegistry
    {
        /// <summary>Stable registry name for logs.</summary>
        string Id { get; }

        /// <summary>(Re)fills the registry from the currently loaded mods.</summary>
        void Rebuild();

        /// <summary>Drops all collected entries; the next <see cref="Rebuild"/> re-reads the mods.</summary>
        void Clear();
    }
}
