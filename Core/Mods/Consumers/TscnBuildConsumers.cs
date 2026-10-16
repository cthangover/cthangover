using System.Reflection;
using Cthangover.Core.Scenes.TscnBuild;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers mod-shipped .tscn property resolvers into
    /// <see cref="PropertyResolverRegistry"/>.
    /// </summary>
    public class PropertyResolverConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "tscn_property_resolvers";

        /// <inheritdoc />
        public int Order => 150;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => PropertyResolverRegistry.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => PropertyResolverRegistry.UnregisterAssembly(assembly);
    }

    /// <summary>
    /// Registers mod-shipped .tscn sub-resource builders into
    /// <see cref="SubResourceBuilderRegistry"/>.
    /// </summary>
    public class SubResourceBuilderConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "tscn_sub_builders";

        /// <inheritdoc />
        public int Order => 151;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => SubResourceBuilderRegistry.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => SubResourceBuilderRegistry.UnregisterAssembly(assembly);
    }

    /// <summary>
    /// Registers mod-shipped .tscn ext-resource builders into
    /// <see cref="ExtResourceBuilderRegistry"/>.
    /// </summary>
    public class ExtResourceBuilderConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "tscn_ext_builders";

        /// <inheritdoc />
        public int Order => 152;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ExtResourceBuilderRegistry.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ExtResourceBuilderRegistry.UnregisterAssembly(assembly);
    }
}
