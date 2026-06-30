using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Godot;

namespace Cthangover.Tools.Services
{
    /// <summary>
    /// Static service for discovering and loading mod background resources.
    /// Queries <see cref="ModManager.Instance.CollectFileList"/> for files under
    /// <c>"backgrounds"</c>, strips extensions to produce IDs, and loads textures
    /// via <see cref="BackgroundFactory"/>. Used by the light editor, interactive
    /// editor, and scenario editor to populate background selection lists and
    /// render previews.
    /// </summary>
    public static class ModResourceService
    {
        /// <summary>
        /// Returns all unique background IDs (filenames without extension) from
        /// the <c>"backgrounds"</c> file collection across all installed mods.
        /// </summary>
        public static List<string> GetBackgroundIds()
        {
            var ids = new List<string>();
            var files = ModManager.Instance.CollectFileList("backgrounds");

            foreach (var kvp in files)
            {
                var id = kvp.Key;
                if (id.EndsWith(".png") || id.EndsWith(".jpg") || id.EndsWith(".jpeg") || id.EndsWith(".webp"))
                {
                    ids.Add(id.Substring(0, id.LastIndexOf('.')));
                }
            }

            return ids;
        }

        /// <summary>
        /// Loads a background texture by ID via <see cref="BackgroundFactory.Instance.Get"/>.
        /// Returns <c>null</c> on any error (missing resource, load failure).
        /// </summary>
        public static Texture2D LoadBackgroundTexture(string id)
        {
            try
            {
                return BackgroundFactory.Instance.Get(id);
            }
            catch
            {
                return null;
            }
        }
    }
}
