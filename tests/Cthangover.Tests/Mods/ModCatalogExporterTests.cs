namespace Engine.Tests.Mods
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Providers;

    /// <summary>Export metadata describes the actual prepared package, including repeated exports after edits.</summary>
    public class ModCatalogExporterTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "package_export_" + Guid.NewGuid().ToString("N"));
        private string Source => Path.Combine(root, "source");
        private string Output => Path.Combine(root, "output");
        private readonly CatalogExportEntry entry = new() { Id = "export_test", Version = "1.0" };

        public ModCatalogExporterTests()
        {
            Directory.CreateDirectory(Source);
            File.WriteAllText(Path.Combine(Source, "manifest.json"), "{\"id\":\"export_test\",\"categories\": [\"library\"]}");
        }

        public void Dispose() => Directory.Delete(root, true);

        [Fact]
        public void RepeatedExportRebuildsPackageAndHashesItsActualContent()
        {
            File.WriteAllText(Path.Combine(Source, "data.txt"), "first");
            Export();
            File.WriteAllText(Path.Combine(Source, "data.txt"), "second");
            Export();
            using var package = new ZipModFileProvider(Path.Combine(Output, "export_test.zip"), "export_test");
            Assert.Equal("second", package.ReadFileText("data.txt"));
            var catalog = JsonSerializer.Deserialize<CatalogExportEntry>(File.ReadAllText(Path.Combine(Output, "export_test.json")));
            Assert.Equal(ModHasher.ComputeArchiveSha256(package.RootPath), catalog.Downloads[0].ArchiveSha256);
            Assert.Equal(ModHasher.ComputeInstallSha256(package), catalog.Downloads[0].InstallSha256);
            Assert.Equal(new FileInfo(package.RootPath).Length, catalog.Downloads[0].SizeBytes);
            Assert.Empty(Directory.GetFiles(Output, "*.tmp"));
            Assert.Empty(Directory.GetFiles(Output, "*.previous"));
        }

        [Fact]
        public void PublicationFailureDoesNotReplaceThePreviousArchive()
        {
            Export();
            var previous = File.ReadAllBytes(Path.Combine(Output, "export_test.zip"));
            File.Delete(Path.Combine(Output, "export_test.json"));
            Directory.CreateDirectory(Path.Combine(Output, "export_test.json"));
            File.WriteAllText(Path.Combine(Source, "new.txt"), "new data");
            Assert.NotNull(Record.Exception(Export));
            Assert.Equal(previous, File.ReadAllBytes(Path.Combine(Output, "export_test.zip")));
            Assert.Empty(Directory.GetFiles(Output, "*.tmp"));
        }

        [Theory]
        [InlineData("encrypted_headers_solid.7z", SevenZipFixtures.Password)]
        [InlineData("encrypted_aes.zip", "fixture-password")]
        public void ExistingEncryptedArchivesExportMatchingContentMetadata(string file, string password)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sevenzip", file);
            using var provider = ArchiveUnlockService.OpenProvider(path, password);
            var info = new ModInfo { Id = "test_sevenzip", FileProvider = provider };
            var data = new CatalogExportEntry { Id = info.Id, Version = "1.2.3" };
            var jsonPath = file.EndsWith(".zip", StringComparison.Ordinal)
                ? ModCatalogExporter.ExportFromZip(info, data, Output)
                : ModCatalogExporter.ExportFromArchive(info, data, Output);
            var restored = JsonSerializer.Deserialize<CatalogExportEntry>(File.ReadAllText(jsonPath));
            Assert.Equal(ModHasher.ComputeArchiveSha256(path), restored.Downloads[0].ArchiveSha256);
            Assert.Equal(ModHasher.ComputeInstallSha256(provider), restored.Downloads[0].InstallSha256);
        }

        private void Export()
        {
            using var provider = new FolderModFileProvider(Source, "export_test");
            ModCatalogExporter.ExportFromFolder(new ModInfo { Id = "export_test", FileProvider = provider }, entry, Output);
        }
    }
}
