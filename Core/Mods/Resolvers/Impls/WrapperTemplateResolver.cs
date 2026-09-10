using System.Collections.Generic;
using System.IO;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Collects wrapper template files (<c>.wrappertmpl</c>) from mods.
    /// Wrappers are text templates that define how entities are rendered
    /// or serialised — they are collected from <c>wrappers/</c>
    /// directories and resolved by name. Each wrapper definition
    /// contains the raw template content and the mod that provided it.
    /// </summary>
    public class WrapperTemplateResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "wrappers";

        private List<WrapperDefinition> _cache;

        /// <summary>
        /// Collects all wrapper template definitions from every loaded
        /// mod. Templates are read from <c>wrappers/</c> directories and
        /// identified by filename without extension. Caches the result.
        /// </summary>
        public List<WrapperDefinition> Collect()
        {
            Context.Initialize();

            if (_cache != null)
                return _cache;

            var result = new List<WrapperDefinition>();

            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.Mods[modId].FileProvider;
                CollectWrapperDefsRecursive(provider, modId, "wrappers", result);
            }

            _cache = result;
            return result;
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _cache = null;
        }

        private static void CollectWrapperDefsRecursive(IModFileProvider provider, string modId, string dir, List<WrapperDefinition> result)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectWrapperDefsRecursive(provider, modId, subDir, result);
                    continue;
                }

                if (!entry.EndsWith(".wrappertmpl", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = provider.ReadFileText(entry);
                if (text == null)
                    continue;

                var name = Path.GetFileNameWithoutExtension(entry.Replace('\\', '/').Split('/')[^1]);

                result.Add(new WrapperDefinition
                {
                    ModId = modId,
                    Name = name,
                    Content = text
                });
            }
        }
    }

    /// <summary>
    /// Static convenience accessor for
    /// <see cref="WrapperTemplateResolver"/>, proxying through
    /// <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class WrapperTemplates
    {
        public static List<WrapperDefinition> Collect()
            => ModManager.Instance.GetResolver<WrapperTemplateResolver>()?.Collect();
    }
}
