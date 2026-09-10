namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Convenience base class for <see cref="IModAssetResolver"/>
    /// implementations. Stores the <see cref="ModResolutionContext"/>
    /// reference provided by <see cref="Initialize"/> so that derived
    /// classes can access the mod registry and cross-resolver lookup
    /// without manual plumbing.
    /// </summary>
    public abstract class ModAssetResolver : IModAssetResolver
    {
        /// <summary>
        /// Unique identifier for this resolver. Must be overridden by
        /// derived classes (e.g. "textures", "shaders", "scenarios").
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// The <see cref="ModResolutionContext"/> provided during
        /// <see cref="Initialize"/>. Gives access to the mod registry,
        /// file I/O helpers, and cross-resolver lookup via
        /// <see cref="ModResolutionContext.GetResolver{T}"/>.
        /// </summary>
        protected ModResolutionContext Context { get; private set; }

        /// <summary>
        /// Stores the context reference for use by derived classes.
        /// Override to add custom initialisation logic — always call
        /// <c>base.Initialize(context)</c> first.
        /// </summary>
        public virtual void Initialize(ModResolutionContext context)
        {
            Context = context;
        }

        /// <summary>
        /// Clears all cached data. Must be overridden by derived classes
        /// to reset their collection caches so the next collection picks
        /// up changes from newly loaded or unloaded mods.
        /// </summary>
        public abstract void ClearCache();
    }
}
