using Cthangover.Core.Mods;
using Godot;

namespace Cthangover.Core.Utils
{
	public static class TextureUtils
	{
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
