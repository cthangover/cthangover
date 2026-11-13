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
