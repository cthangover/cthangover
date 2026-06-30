using Cthangover.Core.Mods;
using Godot;

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
		/// <summary>
		/// Loads a texture identified by <paramref name="imagePath"/> from the
		/// mod group named <paramref name="groupName"/>. The path is normalised
		/// (slashes, casing, optional extension) before it is matched against
		/// the file manifest maintained by <see cref="ModManager.Instance"/>.
		/// </summary>
		/// <param name="groupName">
		/// The logical mod group whose file manifest will be searched.
		/// </param>
		/// <param name="imagePath">
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
			if (string.IsNullOrWhiteSpace(imagePath))
				return null;

			var normalized = NormalizeImagePath(imagePath, groupName);
			return LoadTextureFromModGroup(groupName, normalized);
		}

		private static string NormalizeImagePath(string path, string groupName)
		{
			var normalized = path.Replace('\\', '/').TrimStart('/');
			var prefix = groupName + "/";
			if (normalized.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
				normalized = normalized.Substring(prefix.Length);
			normalized = normalized.ToLowerInvariant();
			return normalized;
		}

		private static Texture2D LoadTextureFromModGroup(string groupName, string idWithoutGroup)
		{
			var files = ModManager.Instance.CollectFileList(groupName);
			var textureExts = ModConfig.Instance.GetTextureExtensionSet();

            GameLogger.Log("MODS", $"LoadTextureFromModGroup: group='{groupName}' id='{idWithoutGroup}' files.Count={files.Count}", LogLevel.Debug);
			foreach (var k in files.Keys)
                GameLogger.Log("MODS", $"  file key='{k}' modId='{files[k].ModId}' fullPath='{files[k].FullPath}'", LogLevel.Debug);

			string matchedKey = null;
			foreach (var key in files.Keys)
			{
				var ext = System.IO.Path.GetExtension(key);
				if (!textureExts.Contains(ext))
					continue;
				if (key == idWithoutGroup || key.StartsWith(idWithoutGroup + ".", System.StringComparison.OrdinalIgnoreCase))
				{
					matchedKey = key;
					break;
				}
			}


			if (matchedKey == null || !files.TryGetValue(matchedKey, out var entry))
			{
				GameLogger.Log("MODS", $"LoadTextureFromModGroup: FAILED - key not found for id='{idWithoutGroup}'", LogLevel.Error);
				return null;
			}

			var bytes = ModManager.Instance.ReadFileBinary(entry.ModId, entry.FullPath);
			if (bytes == null)
			{
                GameLogger.Log("MODS", $"LoadTextureFromModGroup: FAILED - ReadFileBinary returned null for modId='{entry.ModId}' fullPath='{entry.FullPath}'", LogLevel.Error);
                return null;
			}

			return BytesToTexture(bytes, System.IO.Path.GetExtension(matchedKey));
		}

		private static Texture2D BytesToTexture(byte[] bytes, string extension)
		{
			var image = new Image();
			Error error;
			var ext = extension?.ToLowerInvariant();
			if (ext == ".png")
				error = image.LoadPngFromBuffer(bytes);
			else if (ext == ".jpg" || ext == ".jpeg")
				error = image.LoadJpgFromBuffer(bytes);
			else if (ext == ".webp")
				error = image.LoadWebpFromBuffer(bytes);
			else
				return null;

			if (error != Error.Ok)
				return null;
			
			var texture = ImageTexture.CreateFromImage(image);
			image.Dispose();

			return texture;
		}
	}
}
