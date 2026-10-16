using System.Reflection;
using Cthangover.Core.UI.Tool;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers mod-shipped dev tools (<c>IToolProvider</c>) into
    /// <see cref="ToolFactory"/>.
    /// </summary>
    public class ToolProviderConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "tools";

        /// <inheritdoc />
        public int Order => 170;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ToolFactory.Instance.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ToolFactory.Instance.UnregisterAssembly(assembly);
    }

    /// <summary>
    /// Registers mod-shipped toolbar buttons (<c>IToolBoxButton</c>) into
    /// <see cref="ToolBoxButtonFactory"/>.
    /// </summary>
    public class ToolBoxButtonConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "toolbox_buttons";

        /// <inheritdoc />
        public int Order => 171;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ToolBoxButtonFactory.Instance.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ToolBoxButtonFactory.Instance.UnregisterAssembly(assembly);
    }
}
