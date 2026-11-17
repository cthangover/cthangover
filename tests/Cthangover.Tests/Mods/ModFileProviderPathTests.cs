using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Mods.Resolvers;

namespace Engine.Tests.Mods
{
    using Cthangover.Core.Mods;

    public class ModFileProviderPathTests : IDisposable
    {
        private const string MainContent = "Hello ${../shared/data.txt}";
        private const string DataContent = "Resolved";

        private readonly string _testDir;

        public ModFileProviderPathTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"mod_provider_path_test_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        [Fact]
        public void FolderAndZip_ResolveParentInclude_Identically()
        {
            using var folder = CreateFolderProvider();
            using var zip = CreateZipProvider();

            Assert.Equal("shared/data.txt", folder.ResolveIncludePath("../shared/data.txt", "dir/main.txt"));
            Assert.Equal("shared/data.txt", zip.ResolveIncludePath("../shared/data.txt", "dir/main.txt"));
            Assert.Equal(DataContent, folder.ReadFileText("shared/data.txt"));
            Assert.Equal(DataContent, zip.ReadFileText("shared/data.txt"));
        }

        [Fact]
        public void FolderAndZip_ExpandParentInclude_Identically()
        {
            using var folder = CreateFolderProvider();
            using var zip = CreateZipProvider();

            var folderResult = ModResolutionContext.ExpandModIncludes(
                folder, MainContent, "dir/main.txt", new HashSet<string> { "dir/main.txt" });
            var zipResult = ModResolutionContext.ExpandModIncludes(
                zip, MainContent, "dir/main.txt", new HashSet<string> { "dir/main.txt" });

            Assert.Equal("Hello Resolved", folderResult);
            Assert.Equal(folderResult, zipResult);
        }

        [Theory]
        [InlineData("dir/main.txt")]
        [InlineData("dir\\main.txt")]
        [InlineData("/dir/main.txt")]
        public void FolderAndZip_AcceptEquivalentPathForms(string path)
        {
            using var folder = CreateFolderProvider();
            using var zip = CreateZipProvider();

            Assert.True(folder.FileExists(path));
            Assert.True(zip.FileExists(path));
            Assert.Equal(MainContent, folder.ReadFileText(path));
            Assert.Equal(MainContent, zip.ReadFileText(path));
        }

        [Theory]
        [InlineData("Dir/main.txt")]
        [InlineData("dir/Main.txt")]
        [InlineData("DIR/MAIN.TXT")]
        public void FolderAndZip_WrongCase_IsMissOnEveryOs(string path)
        {
            using var folder = CreateFolderProvider();
            using var zip = CreateZipProvider();

            Assert.False(folder.FileExists(path));
            Assert.False(zip.FileExists(path));
            Assert.Null(folder.ReadFileText(path));
            Assert.Null(zip.ReadFileText(path));
            Assert.Null(folder.ReadFileBinary(path));
            Assert.Null(zip.ReadFileBinary(path));
            Assert.Null(folder.OpenStream(path));
            Assert.Null(zip.OpenStream(path));
            Assert.Null(folder.GetFileSystemPath(path));
        }

        [Fact]
        public void FolderAndZip_ListFiles_WrongCaseDirectory_IsEmpty()
        {
            using var folder = CreateFolderProvider();
            using var zip = CreateZipProvider();

            Assert.Empty(folder.ListFiles("Dir"));
            Assert.Empty(zip.ListFiles("Dir"));
        }

        [Fact]
        public void Folder_ListedEntries_KeepTheCasePolicyAndSeeNewFiles()
        {
            using var folder = CreateFolderProvider();

            Assert.Equal(new[] { "dir/", "shared/" }, folder.ListFiles().OrderBy(e => e));
            Assert.Equal(new[] { "dir/main.txt" }, folder.ListFiles("dir"));
            Assert.Equal(MainContent, folder.ReadFileText("dir/main.txt"));

            Assert.Null(folder.ReadFileText("Dir/main.txt"));
            Assert.Null(folder.ReadFileText("dir/Main.txt"));
            Assert.Empty(folder.ListFiles("Dir"));

            File.WriteAllText(Path.Combine(folder.RootPath, "dir", "added.txt"), DataContent);
            Assert.Equal(DataContent, folder.ReadFileText("dir/added.txt"));
            Assert.Equal(new[] { "dir/added.txt", "dir/main.txt" }, folder.ListFiles("dir").OrderBy(e => e));

            File.Delete(Path.Combine(folder.RootPath, "dir", "main.txt"));
            Assert.Null(folder.ReadFileText("dir/main.txt"));
        }

        [Fact]
        public void FolderAndZip_ListFiles_SkipHiddenEntries()
        {
            using var folder = CreateFolderProvider();
            Directory.CreateDirectory(Path.Combine(folder.RootPath, ".git", "objects"));
            File.WriteAllText(Path.Combine(folder.RootPath, ".git", "config.json"), "{}");
            File.WriteAllText(Path.Combine(folder.RootPath, "dir", ".hidden.json"), "{}");

            var zipPath = Path.Combine(_testDir, "hidden_mod.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "dir/main.txt", MainContent);
                WriteEntry(archive, "dir/.hidden.json", "{}");
                WriteEntry(archive, ".git/config.json", "{}");
            }
            using var zip = new ZipModFileProvider(zipPath, "hidden_mod");

            Assert.Equal(new[] { "dir/", "shared/" }, folder.ListFiles().OrderBy(e => e));
            Assert.Equal(new[] { "dir/" }, zip.ListFiles());
            Assert.Equal(new[] { "dir/main.txt" }, folder.ListFiles("dir"));
            Assert.Equal(new[] { "dir/main.txt" }, zip.ListFiles("dir"));
            Assert.Equal("{}", folder.ReadFileText("dir/.hidden.json"));
            Assert.Equal("{}", zip.ReadFileText("dir/.hidden.json"));
        }

        [Fact]
        public void FolderAndZip_EscapeAttempt_StaysInsideMod()
        {
            File.WriteAllText(Path.Combine(_testDir, "outside.txt"), "secret");

            using var folder = CreateFolderProvider();
            using var zip = CreateZipProvider();

            Assert.False(folder.FileExists("../outside.txt"));
            Assert.False(zip.FileExists("../outside.txt"));
            Assert.Null(folder.ReadFileText("../outside.txt"));
            Assert.Null(zip.ReadFileText("../outside.txt"));
        }

        [Fact]
        public void FolderAndZip_ListFiles_UseCanonicalForwardSlashes()
        {
            using var folder = CreateFolderProvider();
            using var zip = CreateZipProvider();

            Assert.Contains("dir/main.txt", folder.ListFiles("dir").ToList());
            Assert.Contains("dir/main.txt", zip.ListFiles("dir").ToList());
        }

        [Fact]
        public void FolderAndZip_OpenStreamAndBinary_ReturnSameContent()
        {
            using var folder = CreateFolderProvider();
            using var zip = CreateZipProvider();

            using var folderStream = folder.OpenStream("dir\\main.txt");
            using var zipStream = zip.OpenStream("/dir/main.txt");
            Assert.NotNull(folderStream);
            Assert.NotNull(zipStream);

            using var folderReader = new StreamReader(folderStream);
            using var zipReader = new StreamReader(zipStream);
            Assert.Equal(MainContent, folderReader.ReadToEnd());
            Assert.Equal(MainContent, zipReader.ReadToEnd());

            Assert.Equal(folder.ReadFileBinary("dir/main.txt"), zip.ReadFileBinary("dir/main.txt"));
        }

        [Fact]
        public void Folder_GetFileSystemPath_StaysInsideRoot()
        {
            File.WriteAllText(Path.Combine(_testDir, "outside.txt"), "secret");

            using var folder = CreateFolderProvider();

            var path = folder.GetFileSystemPath("dir/main.txt");
            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            Assert.Null(folder.GetFileSystemPath("../outside.txt"));
        }

        [Fact]
        public void Zip_GetFileSystemPath_ReturnsNull()
        {
            using var zip = CreateZipProvider();

            Assert.Null(zip.GetFileSystemPath("dir/main.txt"));
        }

        /// <summary>Real storage providers preserve runtime data through named resolution and nested JSON fallback.</summary>
        [Fact]
        public void ExplicitScenario_FolderAndZip_NamedOverrideReturnsIdenticalBytes()
        {
            var files = ScenarioFiles();
            using var folder = CreateScenarioFolder(files);
            using var zip = CreateScenarioZip(files);
            const string expected = "scene: explicit_provider_probe\ninclude_syntax: explicit-v1\n---\n"
                + "set parent ${reference}\nset child ${value}\n{\"Runtime\":\"${reference}\",\"Value\":[1,2]}";

            Assert.Null(zip.GetFileSystemPath("scenarios/main.scenario"));
            Assert.Equal(expected, ScenarioDefs.Resolve("main", folder));
            Assert.Equal(expected, ScenarioDefs.Resolve("main", zip));
        }

        /// <summary>Explicit directives retain exact-case lookup, missing-file refusal and the logical root boundary.</summary>
        [Fact]
        public void ExplicitScenario_FolderAndZip_RefuseWrongCaseMissingAndOutsideIncludes()
        {
            var files = ScenarioFiles();
            files["scenarios/wrong.scenario"] = "include_syntax: explicit-v1\n---\n$include{Parts/body.txt}";
            files["scenarios/missing.scenario"] = "include_syntax: explicit-v1\n---\n$include{absent.txt}";
            files["scenarios/outside.scenario"] = "include_syntax: explicit-v1\n---\n$include{../../outside.txt}";
            File.WriteAllText(Path.Combine(_testDir, "outside.txt"), "outside secret");
            using var folder = CreateScenarioFolder(files);
            using var zip = CreateScenarioZip(files);

            foreach (var provider in new IModFileProvider[]
            {
                folder,
                zip
            })
            {
                Assert.Null(ModManager.Instance.ReadResolvedText(provider.Mod, "scenarios/Main.scenario", provider));
                Assert.Null(ModManager.Instance.ReadResolvedText(provider.Mod, "scenarios/wrong.scenario", provider));
                Assert.Null(ModManager.Instance.ReadResolvedText(provider.Mod, "scenarios/missing.scenario", provider));
                Assert.Null(ModManager.Instance.ReadResolvedText(provider.Mod, "scenarios/outside.scenario", provider));
            }
        }

        /// <summary>A mounted suite resolves against its projected root even when its host contains matching secrets.</summary>
        [Fact]
        public void ExplicitScenario_FolderAndZip_SubdirKeepsIncludesInsideSuiteRoot()
        {
            var files = new Dictionary<string, string>
            {
                ["suite/scenarios/main.scenario"] = "scene: suite_probe\ninclude_syntax: explicit-v1\n---\n"
                    + "$include{../../shared.txt}\n${reference}",
                ["suite/shared.txt"] = "suite data",
                ["shared.txt"] = "host secret",
                ["suite/scenarios/reference.json"] = "wrong substitution"
            };
            using var folder = CreateScenarioFolder(files);
            using var zip = CreateScenarioZip(files);
            using var folderSuite = new SubdirModFileProvider(folder, "suite", "folder_suite");
            using var zipSuite = new SubdirModFileProvider(zip, "suite", "zip_suite");
            const string expected = "scene: suite_probe\ninclude_syntax: explicit-v1\n---\nsuite data\n${reference}";

            Assert.Equal(expected, ScenarioDefs.Resolve("main", folderSuite));
            Assert.Equal(expected, ScenarioDefs.Resolve("main", zipSuite));
            Assert.Null(zipSuite.GetFileSystemPath("scenarios/main.scenario"));
        }

        /// <summary>Explicit provider selection cannot reuse another provider's content or capture runtime values in a cache.</summary>
        [Fact]
        public void ExplicitScenario_OverrideReadsFreshProviderContentAfterCacheClear()
        {
            var files = ScenarioFiles();
            using var folder = CreateScenarioFolder(files);
            using var zip = CreateScenarioZip(files);
            var original = ScenarioDefs.Resolve("main", zip);
            File.WriteAllText(Path.Combine(folder.RootPath, "scenarios", "parts", "body.txt"), "set changed ${reference}");

            var changed = "scene: explicit_provider_probe\ninclude_syntax: explicit-v1\n---\n"
                + "set parent ${reference}\nset changed ${reference}";
            Assert.Equal(changed, ScenarioDefs.Resolve("main", folder));
            ModManager.Instance.GetResolver<ScenarioResolver>().ClearCache();
            Assert.Equal(original, ScenarioDefs.Resolve("main", zip));
            Assert.Equal(changed, ScenarioDefs.Resolve("main", folder));
        }

        /// <summary>The selected overlay id follows remounts and resolves every include through that overlay's provider.</summary>
        [Fact]
        public void ExplicitScenario_OverlayRemountUsesSelectedProviderAfterInvalidation()
        {
            var files = ScenarioFiles();
            using var folder = CreateScenarioFolder(files);
            using var zip = CreateScenarioZip(files);
            File.WriteAllText(Path.Combine(folder.RootPath, "scenarios", "parts", "body.txt"), "set overlay ${reference}");
            var id = "explicit_overlay_" + System.Guid.NewGuid().ToString("N");
            const string expected = "scene: explicit_provider_probe\ninclude_syntax: explicit-v1\n---\n"
                + "set parent ${reference}\nset overlay ${reference}";
            try
            {
                ModOverlays.Mount(id, zip);
                Assert.Equal(ScenarioDefs.Resolve("main", zip), ScenarioDefs.ReadText(id, "scenarios/main.scenario"));
                ModOverlays.Mount(id, folder);
                ScenarioDefs.Invalidate();
                Assert.Equal(expected, ScenarioDefs.ReadText(id, "scenarios/main.scenario"));
                Assert.Null(ScenarioDefs.ReadText(id, "scenarios/Main.scenario"));
            }
            finally
            {
                ModOverlays.Unmount(id);
                ScenarioDefs.Invalidate();
            }
        }

        private static Dictionary<string, string> ScenarioFiles()
        {
            return new Dictionary<string, string>
            {
                ["scenarios/main.scenario"] = "scene: explicit_provider_probe\ninclude_syntax: explicit-v1\n---\n"
                    + "set parent ${reference}\n$include{parts/body.txt}",
                ["scenarios/parts/body.txt"] = "set child ${value}\n$include{../data}",
                ["scenarios/data.json"] = "{\"Runtime\":\"${reference}\",\"Value\":${piece}}",
                ["scenarios/piece.json"] = "[1,2]",
                ["scenarios/reference.json"] = "wrong substitution",
                ["scenarios/parts/value.json"] = "wrong substitution"
            };
        }

        private FolderModFileProvider CreateScenarioFolder(Dictionary<string, string> files)
        {
            var root = Path.Combine(_testDir, "scenario_folder");
            foreach (var file in files)
            {
                var path = Path.Combine(root, file.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, file.Value, new UTF8Encoding(false));
            }

            return new FolderModFileProvider(root, "scenario_folder");
        }

        private ZipModFileProvider CreateScenarioZip(Dictionary<string, string> files)
        {
            var path = Path.Combine(_testDir, "scenario_zip.zip");
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    WriteEntry(archive, file.Key, file.Value);
                }
            }

            return new ZipModFileProvider(path, "scenario_zip");
        }

        private FolderModFileProvider CreateFolderProvider()
        {
            var root = Path.Combine(_testDir, "folder_mod");
            Directory.CreateDirectory(Path.Combine(root, "dir"));
            Directory.CreateDirectory(Path.Combine(root, "shared"));
            File.WriteAllText(Path.Combine(root, "dir", "main.txt"), MainContent);
            File.WriteAllText(Path.Combine(root, "shared", "data.txt"), DataContent);
            return new FolderModFileProvider(root, "folder_mod");
        }

        private ZipModFileProvider CreateZipProvider()
        {
            var zipPath = Path.Combine(_testDir, "zip_mod.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "dir/main.txt", MainContent);
                WriteEntry(zip, "shared/data.txt", DataContent);
            }
            return new ZipModFileProvider(zipPath, "zip_mod");
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }
}
