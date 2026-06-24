using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Godot;

namespace Cthangover.Tools.Services
{
    public static class ModResourceService
    {
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
