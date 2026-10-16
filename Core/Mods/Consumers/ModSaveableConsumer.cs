using System.Reflection;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers <c>IModSaveable</c> implementations into
    /// <see cref="ModSaveableRegistry"/>.
    /// </summary>
    public class ModSaveableConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "mod_saveables";

        /// <inheritdoc />
        public int Order => 110;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ModSaveableRegistry.RegisterAssembly(assembly, modId);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ModSaveableRegistry.UnregisterAssembly(assembly, modId);
    }
}
