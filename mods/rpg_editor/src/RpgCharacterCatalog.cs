using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Mods.Rpg;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// The characters the running game knows (<c>Kind: character</c> of every
    /// loaded mod), sorted by display name, for placing them on a map.
    /// <para/>
    /// A character has no map look of its own, so its charset is found by name:
    /// an explicit <c>"Charset"</c> field of the character (kept by the
    /// character's extra properties; the character editor sets it) wins, otherwise the
    /// portrait path in the <c>characters</c> group (<c>characters/village/slime</c> or
    /// <c>village/slime</c> → <c>village/slime</c>), as village_pack names both. Only charsets that exist are used: the texture
    /// loader logs an error for a missing one. The row picture is the middle frame
    /// of the "down" row, how the character stands on the map.
    /// </summary>
    public static class RpgCharacterCatalog
    {
        private const string ImageGroup = "characters";

        /// <summary>Reads the character list anew.</summary>
        public static List<RpgPickerEntry> Load(RpgTextures textures)
        {
            var charsets = RpgItemCatalog.GroupFiles(RpgTextures.CharsetGroup);
            return (Entities.Collect<CharacterInfo>()?.Values ?? Enumerable.Empty<CharacterInfo>())
                .Where(info => !string.IsNullOrEmpty(info.ID))
                .Select(info => Entry(info, CharsetOf(info, charsets), textures))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static RpgPickerEntry Entry(CharacterInfo info, string charset, RpgTextures textures) => new()
        {
            Id = info.ID,
            Name = string.IsNullOrEmpty(info.Name) ? info.ID : TranslationServer.Translate(info.Name).ToString(),
            Look = charset,
            Icon = FrontFrame(charset == null ? null : textures.Charset(charset))
        };

        private static string CharsetOf(CharacterInfo info, HashSet<string> charsets)
        {
            object explicitCharset = null;
            info.ExtraProperties?.TryGetValue("Charset", out explicitCharset);
            var image = info.Image ?? string.Empty;
            if (image.StartsWith(ImageGroup + "/", StringComparison.OrdinalIgnoreCase))
                image = image.Substring(ImageGroup.Length + 1);
            var candidates = new[] { explicitCharset?.ToString(), image };
            return candidates.FirstOrDefault(path => !string.IsNullOrEmpty(path) && charsets.Contains(path));
        }

        private static Texture2D FrontFrame(Texture2D image)
        {
            if (image == null)
                return null;
            var frame = new Vector2(image.GetWidth() / 3f, image.GetHeight() / 4f);
            return new AtlasTexture { Atlas = image, Region = new Rect2(new Vector2(frame.X, 0f), frame) };
        }
    }
}
