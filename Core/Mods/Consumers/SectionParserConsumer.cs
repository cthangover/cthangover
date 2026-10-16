using System.Reflection;
using Cthangover.Core.Scenes.TscnParsing;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers mod-shipped .tscn section parsers into
    /// <see cref="SectionParserRegistry"/>.
    /// </summary>
    public class SectionParserConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "tscn_section_parsers";

        /// <inheritdoc />
        public int Order => 140;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => SectionParserRegistry.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => SectionParserRegistry.UnregisterAssembly(assembly);
    }
}
