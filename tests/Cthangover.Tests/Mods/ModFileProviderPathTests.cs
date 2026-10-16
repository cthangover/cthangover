using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Mods.Resolvers;

namespace Engine.Tests.Mods
{
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
