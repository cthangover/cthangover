using System.Reflection;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers <c>IModSettings</c> implementations into
    /// <see cref="ModSettingsRegistry"/>.
    /// </summary>
    public class ModSettingsConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "mod_settings";

        /// <inheritdoc />
        public int Order => 120;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ModSettingsRegistry.RegisterAssembly(assembly, modId);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ModSettingsRegistry.UnregisterAssembly(assembly, modId);
    }
}
