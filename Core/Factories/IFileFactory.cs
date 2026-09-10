namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Contract for factories that load JSON-serialized objects from mod file groups.
    /// The <c>out T</c> covariance enables returning a <c>IFileFactory&lt;Derived&gt;</c>
    /// where an <c>IFileFactory&lt;Base&gt;</c> is expected, since the factory only
    /// produces, never consumes <c>T</c>. The <c>IIdentifiable</c> constraint ensures
    /// every object can be keyed by its <c>ID</c> — <c>FileFactory&lt;T&gt;</c> uses
    /// this to build the initial lookup dictionary from JSON arrays.
    /// </summary>
    public interface IFileFactory<out T> where T : class, IIdentifiable
    {
        /// <summary>
        /// Mod resource group name (e.g. "skills", "status_effects") that
        /// <c>ModManager.CollectJsonGroup</c> scans to populate this factory's cache.
        /// </summary>
        string GroupName { get; }
        T Get(string id);
    }
}
