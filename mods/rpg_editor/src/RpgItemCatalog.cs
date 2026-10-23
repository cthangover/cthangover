using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Items;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Mods.Rpg;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// The items the running game knows (<c>Kind: item</c> of every loaded mod),
    /// sorted by display name, for laying them on a map. The look is the item's
    /// own <c>Sprite</c> in the <c>items</c> group, the picture the inventory
    /// shows; an item whose picture is missing gets none (the texture loader
    /// would log an error for it).
    /// </summary>
    public static class RpgItemCatalog
    {
        /// <summary>Reads the item list anew.</summary>
        public static List<RpgPickerEntry> Load(RpgTextures textures)
        {
            var pictures = GroupFiles(RpgTextures.IconGroup);
            return (Entities.Collect<ItemInfo>()?.Values ?? Enumerable.Empty<ItemInfo>())
                .Where(info => !string.IsNullOrEmpty(info.ID))
                .Select(info => Entry(info, pictures.Contains(info.Sprite ?? string.Empty) ? info.Sprite : null, textures))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Paths of a file group without extensions, case-insensitive: what exists to be loaded.</summary>
        public static HashSet<string> GroupFiles(string group)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = Files.Collect(group);
            if (files != null)
            {
                foreach (var key in files.Keys)
                    paths.Add(Path.ChangeExtension(key, null));
            }
            return paths;
        }

        private static RpgPickerEntry Entry(ItemInfo info, string picture, RpgTextures textures) => new()
        {
            Id = info.ID,
            Name = string.IsNullOrEmpty(info.Name) ? info.ID : TranslationServer.Translate(info.Name).ToString(),
            Look = picture,
            Icon = picture == null ? null : textures.Icon(picture)
        };
    }
}
