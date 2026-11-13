using System.IO.Compression;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Utils;

namespace Engine.Tests.Mods;

public class CollectionPackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "collection-package-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExportAndInstall_RoundTripsPresentationWithoutEmbeddingMods()
    {
        Directory.CreateDirectory(Path.Combine(root, "source/locale"));
        File.WriteAllText(Path.Combine(root, "source/locale/en.properties"), "title=Collection\n");
        File.WriteAllBytes(Path.Combine(root, "source/cover.png"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(root, "source/unrelated.cs"), "excluded");
        using var provider = new FolderModFileProvider(Path.Combine(root, "source"), "test_collection");
        var collection = new ModCollection { Id = "test_collection", Version = "0.1.0", Name = "Collection",
            Cover = "cover.png", Kind = ModCollectionKind.Game, Package = provider,
            Mechanics = new() { new() { Id = "core", Mods = new() { "private_editor>=0.1.0" } } },
            Includes = new() { new() { Ref = "other>=0.1.0", Optional = true, Selected = false } },
            Settings = new() { ["mode"] = "test" }, Entry = new() { Scene = "start" } };
        var first = CollectionPackage.Export(collection, Path.Combine(root, "first"));
        var second = CollectionPackage.Export(collection, Path.Combine(root, "second"));
        Assert.Equal(File.ReadAllBytes(first.Archive), File.ReadAllBytes(second.Archive));
        var restored = JsonSerializer.Deserialize<ModCollection>(first.Json, JsonOptionsProvider.ReadOptions);
        Assert.Equal("private_editor>=0.1.0", restored.Mechanics[0].Mods[0]);
        Assert.False(restored.Includes[0].Selected);
        Assert.Equal("test", restored.Settings["mode"]);
        Assert.Equal("start", restored.Entry.Scene);
        Assert.Equal(collection.Name, restored.Name);
        Assert.Equal(collection.Cover, restored.Cover);
        Assert.Equal(collection.Kind, restored.Kind);
        Assert.Equal(collection.Includes[0].Ref, restored.Includes[0].Ref);
        Assert.True(restored.Includes[0].Optional);
        var target = Path.Combine(root, "installed");
        CollectionPackage.Install(first.Archive, target, restored, restored.Downloads.Single());
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(target, "cover.png")));
        Assert.Equal("title=Collection\n", File.ReadAllText(Path.Combine(target, "locale/en.properties")));
        Assert.Equal(3, Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length);
        var installed = JsonSerializer.Deserialize<ModCollection>(File.ReadAllText(Path.Combine(target, "collection.json")), JsonOptionsProvider.ReadOptions);
        Assert.Null(installed.Downloads);
        Assert.Equal(collection.Id, installed.Id);
        Assert.Equal(collection.Version, installed.Version);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("bad/path")]
    [InlineData("bad\\path")]
    public void Export_RejectsEscapingIds(string id)
    {
        Assert.Throws<InvalidDataException>(() => CollectionPackage.Export(new ModCollection { Id = id }, root));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("manifest.json")]
    [InlineData("script.cs")]
    public void Install_RejectsUnrelatedOrEscapingFilesAndPreservesPreviousPackage(string name)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "unsafe.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("collection.json").Open()))
                writer.Write("{\"id\":\"test_collection\",\"version\":\"0.1.0\"}");
            using var bad = new StreamWriter(zip.CreateEntry(name).Open());
            bad.Write("bad");
        }
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "old"), "preserved");
        var release = new CatalogEntryDownload { Version = "0.1.0", ArchiveSha256 = ModHasher.ComputeArchiveSha256(path) };
        Assert.Throws<InvalidDataException>(() => CollectionPackage.Install(path, target, new ModCollection { Id = "test_collection" }, release));
        Assert.Equal("preserved", File.ReadAllText(Path.Combine(target, "old")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Install_RejectsWrongArchiveOrContentHash(bool archiveHash)
    {
        var exported = CollectionPackage.Export(new ModCollection { Id = "test_hash", Version = "0.1.0" }, root);
        var metadata = JsonSerializer.Deserialize<ModCollection>(exported.Json, JsonOptionsProvider.ReadOptions);
        var release = metadata.Downloads.Single();
        if (archiveHash) release.ArchiveSha256 = new string('a', 64);
        else release.InstallSha256 = new string('a', 64);
        var target = Path.Combine(root, "target");
        Assert.Throws<InvalidDataException>(() => CollectionPackage.Install(exported.Archive, target, metadata, release));
        Assert.False(Directory.Exists(target));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
