using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Providers;
using Xunit;

namespace Engine.Tests.Mods;

public class ModCategoryManifestStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "categories_" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Theory]
    [InlineData("mods", false)]
    [InlineData("mods_private", true)]
    public void SavePreservesFieldsBomAndPrimaryOrder(string folder, bool bom)
    {
        var modPath = Path.Combine(root, folder, "test_pack");
        Directory.CreateDirectory(modPath);
        var path = Path.Combine(modPath, "manifest.json");
        File.WriteAllText(path, "{\"id\":\"test_pack\",\"categories\":[\"story\"],\"extension\":{\"value\":42}}", new UTF8Encoding(bom));
        using var provider = new FolderModFileProvider(modPath, "test_pack");
        var mod = new ModInfo { Id = "test_pack", FileProvider = provider, Manifest = new ModManifest() };
        Assert.True(ModCategoryManifestStore.CanEdit(mod, root));
        ModCategoryManifestStore.Save(mod, root, new[] { " RPG ", "Weap ons! 世界", "rpg" });
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(42, document.RootElement.GetProperty("extension").GetProperty("value").GetInt32());
        var restored = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(path));
        Assert.Equal(new[] { "RPG", "Weap ons! 世界" }, restored.Categories);
        Assert.Equal(restored.Categories, mod.Manifest.Categories);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(bom, bytes[0] == 0xef);
        Assert.DoesNotContain("\r", File.ReadAllText(path));
        Assert.DoesNotContain("\"category\":", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(false, "\n")]
    [InlineData(true, "\n")]
    [InlineData(false, "\r\n")]
    [InlineData(true, "\r\n")]
    public void UnchangedCategoriesPreserveManifestAndExportChecksums(bool bom, string newline)
    {
        var modPath = Path.Combine(root, "mods_private", "test_pack");
        Directory.CreateDirectory(modPath);
        var path = Path.Combine(modPath, "manifest.json");
        var json = "{\n  \"id\": \"test_pack\",\n  \"categories\": [\"RPG\", \"Weap ons! 世界\"],\n  \"extension\": 42\n}\n";
        File.WriteAllText(path, json.Replace("\n", newline), new UTF8Encoding(bom));
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc));
        var original = File.ReadAllBytes(path);
        var timestamp = File.GetLastWriteTimeUtc(path);
        using var provider = new FolderModFileProvider(modPath, "test_pack");
        var mod = new ModInfo { Id = "test_pack", FileProvider = provider, Manifest = new ModManifest() };
        var first = new CatalogExportEntry { Id = mod.Id, Version = "1.0.0" };
        ModCatalogExporter.ExportFromFolder(mod, first, Path.Combine(root, "before"));

        ModCategoryManifestStore.Save(mod, root, new[] { " RPG ", "Weap ons! 世界", "rpg" });
        ModCategoryManifestStore.Save(mod, root, mod.Manifest.Categories);

        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
        var restored = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(path));
        Assert.Equal(new[] { "RPG", "Weap ons! 世界" }, restored.Categories);
        Assert.Equal(restored.Categories, mod.Manifest.Categories);
        var second = new CatalogExportEntry { Id = mod.Id, Version = "1.0.0" };
        ModCatalogExporter.ExportFromFolder(mod, second, Path.Combine(root, "after"));
        Assert.Equal(first.Downloads[0].ArchiveSha256, second.Downloads[0].ArchiveSha256);
        Assert.Equal(first.Downloads[0].InstallSha256, second.Downloads[0].InstallSha256);
    }

    [Fact]
    public void InstalledCopyIsReadOnlyEvenThoughItsParentIsNamedMods()
    {
        var installed = Path.Combine(root, "profile", "mods", "test_pack");
        Directory.CreateDirectory(installed);
        var path = Path.Combine(installed, "manifest.json");
        File.WriteAllText(path, "{\"id\":\"test_pack\"}");
        using var provider = new FolderModFileProvider(installed, "test_pack");
        var mod = new ModInfo { FileProvider = provider, Manifest = new ModManifest() };
        Assert.False(ModCategoryManifestStore.CanEdit(mod, root));
        Assert.Throws<InvalidOperationException>(() => ModCategoryManifestStore.Save(mod, root, new[] { "weapons" }));
        Assert.Equal("{\"id\":\"test_pack\"}", File.ReadAllText(path));
        Assert.False(ModCategoryManifestStore.CanEdit(null, root));
    }
}
