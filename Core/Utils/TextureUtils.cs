using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Centralised texture-loading pipeline for the mod system. Because Godot's
    /// <c>ResourceLoader</c> cannot load assets embedded inside mod archives,
    /// this utility reads raw image bytes through <see cref="ModManager"/> and
    /// decodes them into <see cref="Godot.Texture2D"/> instances via Godot's
    /// <see cref="Image"/> API. It also performs extension-based key matching
    /// so that callers can omit file extensions when requesting textures.
    /// </summary>
    public static class TextureUtils
    {
        // FileListResolver replaces its dictionary after invalidation. Weak keys let old
        // mod sets/studio layers go away without a second invalidation protocol here.

        private static readonly ConditionalWeakTable<Dictionary<string, FileEntry>, TextureFileIndex> indexes = new();

        /// <summary>
        /// Loads a texture identified by <paramref name = "imagePath"/> from the
        /// mod group named <paramref name = "groupName"/>. The path is normalised
        /// (slashes, casing, optional extension) before it is matched against
        /// the file manifest maintained by <see cref="ModManager.Instance"/>.
        /// </summary>
        /// <param name = "groupName">
        /// The logical mod group whose file manifest will be searched.
        /// </param>
        /// <param name = "imagePath">
        /// A project-relative path to the image. Leading slashes are stripped,
        /// any <c>groupName/</c> prefix is removed, and the extension may be
        /// omitted — the first file whose key starts with the normalised path
        /// and carries a recognised texture extension (from
        /// <c>ModConfig.Instance.GetTextureExtensionSet()</c>) will be loaded.
        /// </param>
        /// <returns>
        /// A decoded <see cref="Texture2D"/>, or <c>null</c> if the path is
        /// blank, no matching file is found, or decoding fails.
        /// </returns>
        public static Texture2D LoadFromModGroup(string groupName, string imagePath)
        {
            using var image = LoadImageFromModGroup(groupName, imagePath);
            return image == null ? null : ImageTexture.CreateFromImage(image);
        }

        /// <summary>
        /// Decodes the same dependency/overlay/ZIP-aware image as <see cref="LoadFromModGroup"/>
        /// without uploading it to the GPU. The caller owns the returned image and must dispose it,
        /// including after deriving a cropped or resized authoring thumbnail. A missing named image
        /// retains the normal resource diagnostic; blank paths return null.
        /// </summary>
        public static Image LoadImageFromModGroup(string groupName, string imagePath)
        {
            if (!GameEnvironment.IsRuntime)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            var normalized = NormalizeImagePath(imagePath, groupName);
            return LoadImageFromGroup(groupName, normalized);
        }

        private static string NormalizeImagePath(string path, string groupName)
        {
            var normalized = ModPath.Normalize(path);
            var prefix = groupName + "/";
            if (normalized.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(prefix.Length);
            }

            normalized = normalized.ToLowerInvariant();
            return normalized;
        }

        private static Image LoadImageFromGroup(string groupName, string idWithoutGroup)
        {
            var files = Files.Collect(groupName);
            var matchedKey = indexes.GetValue(files, CreateIndex).Find(idWithoutGroup);
            if (matchedKey == null || !files.TryGetValue(matchedKey, out var entry))
            {
                GameLogger.Log("MODS", $"LoadTextureFromModGroup: FAILED - key not found for id='{idWithoutGroup}'", LogLevel.Error);
                return null;
            }

            var bytes = ModManager.Instance.ReadFileBinary(entry.ModId, entry.FullPath);
            if (bytes == null)
            {
                GameLogger.Log("MODS",
                    $"LoadTextureFromModGroup: FAILED - ReadFileBinary returned null for modId='{entry.ModId}' "
                        + $"fullPath='{entry.FullPath}'", LogLevel.Error);
                return null;
            }

            return BytesToImage(bytes, System.IO.Path.GetExtension(matchedKey));
        }

        private static TextureFileIndex CreateIndex(Dictionary<string, FileEntry> files)
        {
            return new(files.Keys, ModConfig.Instance.GetTextureExtensionSet());
        }

        private static Image BytesToImage(byte[] bytes, string extension)
        {
            var image = new Image();
            try
            {
                var error = extension?.ToLowerInvariant() switch
                {
                    ".png" => image.LoadPngFromBuffer(bytes),
                    ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(bytes),
                    ".webp" => image.LoadWebpFromBuffer(bytes),
                    _ => Error.FileUnrecognized,
                };
                if (error == Error.Ok)
                {
                    return image;
                }
            }
            catch
            {
                image.Dispose();
                throw;
            }

            image.Dispose();
            return null;
        }
    }
}
