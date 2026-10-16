using System.Reflection;
using Cthangover.Core.Scenes;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers <c>SceneEventAttribute</c>-decorated executable events from
    /// mod assemblies into <see cref="SceneEventRegistry"/>.
    /// </summary>
    public class SceneEventConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "scene_events";

        /// <inheritdoc />
        public int Order => 160;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => SceneEventRegistry.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => SceneEventRegistry.UnregisterAssembly(assembly, modId);
    }
}
