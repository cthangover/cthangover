namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Contract for a pluggable asset resolver — a subsystem that collects
    /// and resolves typed assets from loaded mods. Each resolver has a
    /// unique <see cref="Id"/> (e.g. "textures", "shaders") and is
    /// initialised with a <see cref="ModResolutionContext"/> that provides
    /// access to the mod registry and cross-resolver lookup via
    /// <see cref="ModResolutionContext.GetResolver{T}"/>.
    ///
    /// Implementations are auto-discovered by
    /// <c>Reflections.FindAndCreate</c> and registered in
    /// <see cref="ModManager"/>. Mod DLLs can ship custom resolvers that
    /// will be picked up automatically when the assembly is loaded.
    /// </summary>
    public interface IModAssetResolver
    {
        /// <summary>
        /// Unique identifier for this resolver, used as a key in the
        /// resolver dictionary inside <see cref="ModManager"/>.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Called once during mod loading to provide the resolver with
        /// access to the mod registry, file I/O helpers, and other
        /// resolvers. Must be called before <c>Collect</c> or
        /// <c>Resolve</c> methods.
        /// </summary>
        void Initialize(ModResolutionContext context);

        /// <summary>
        /// Clears all cached data collected from mods. Called on mod
        /// reload so that the next collection picks up changes from
        /// newly loaded or unloaded mods.
        /// </summary>
        void ClearCache();
    }
}
