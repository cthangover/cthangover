using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Mods.Providers;

namespace Cthangover.Core.Mods;

/// <summary>Edits only category metadata of development folders; catalog installations and archives stay read-only.</summary>
public static class ModCategoryManifestStore
{
    /// <summary>Only a direct mod folder of a project's declared roots is editable, including mods_private.</summary>
    public static bool CanEdit(ModInfo mod, string projectRoot)
    {
        if (mod?.FileProvider is not FolderModFileProvider || string.IsNullOrEmpty(projectRoot)) return false;
        var folder = Path.GetFullPath(mod.FileProvider.RootPath);
        return ModRootFolders.Names.Any(root => string.Equals(Path.GetDirectoryName(folder),
            Path.GetFullPath(Path.Combine(projectRoot, root)), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
    }

    /// <summary>Atomically replaces categories while preserving unknown manifest fields and the original UTF-8 BOM.</summary>
    public static void Save(ModInfo mod, string projectRoot, IEnumerable<string> categories)
    {
        if (!CanEdit(mod, projectRoot)) throw new InvalidOperationException("Only development mod folders can edit categories");
        var path = Path.Combine(mod.FileProvider.RootPath, "manifest.json");
        var bytes = File.ReadAllBytes(path);
        var bom = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
        var text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        var root = JsonNode.Parse(text, documentOptions: Cthangover.Core.Utils.JsonOptionsProvider.DocumentOptions) as JsonObject
            ?? throw new JsonException("manifest.json must be an object");
        var normalized = ModCategories.Normalize(categories);
        // An unchanged publish/export must preserve manifest bytes and the ZIP timestamp.
        // Compare authored spelling/order as well, so explicit category edits still persist.
        if (root["categories"] is JsonArray current
            && current.Select(value => value?.GetValue<string>()).SequenceEqual(normalized, StringComparer.Ordinal))
        {
            mod.Manifest.Categories = normalized;
            return;
        }
        root["categories"] = new JsonArray(normalized.Select(code => JsonValue.Create(code)).ToArray());
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n", new UTF8Encoding(bom));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        mod.Manifest.Categories = normalized;
        mod.DeferInstallSha256(() => InstallHashCache.Get(mod.FileProvider, ModConfig.Instance.CatalogIgnorePatterns));
    }
}
