namespace Cthangover.Core.UI
{
    /// <summary>
    /// Kernel seam for lazily constructed mod controls (the interface engine
    /// mod's ModWidget). UiHost runs the one-time construction right after a
    /// panel instance is built and parented, so the panel is initialized even
    /// when it is shown through plain Control visibility.
    /// </summary>
    public interface IUiLazyConstruct
    {
        /// <summary>Idempotent one-time construction gate.</summary>
        void EnsureConstructed();
    }
}
