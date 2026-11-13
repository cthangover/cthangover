namespace Engine.Tests.Mods
{
    using System.IO;
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.TestRunner;
    using Engine.Tests.Arch;

    /// <summary>
    /// The list of game-dir mods folders and the places that must follow it: the
    /// test runner's own copy, the core project's compile exclusions and the
    /// <c>.gdignore</c> that keeps Godot from importing a mods folder.
    /// </summary>
    public class ModRootFoldersTests
    {
        [Fact]
        public void Names_StartWithThePublicFolderAndAreUnique()
        {
            Assert.Equal("mods", ModRootFolders.Primary);
            Assert.Equal(ModRootFolders.Primary, ModRootFolders.Names[0]);
            Assert.Equal(ModRootFolders.Names.Count, ModRootFolders.Names.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void Contains_KnowsEveryFolderIgnoringCase()
        {
            foreach (var name in ModRootFolders.Names)
            {
                Assert.True(ModRootFolders.Contains(name));
                Assert.True(ModRootFolders.Contains(name.ToUpperInvariant()));
            }

            Assert.False(ModRootFolders.Contains("scenes"));
            Assert.False(ModRootFolders.Contains("mods_"));
            Assert.False(ModRootFolders.Contains(""));
            Assert.False(ModRootFolders.Contains(null));
        }

        [Fact]
        public void StripModPrefix_RemovesFolderAndModOfEveryFolder()
        {
            foreach (var name in ModRootFolders.Names)
                Assert.Equal("scenes/ui/panel.tscn", ModRootFolders.StripModPrefix(name + "/some_mod/scenes/ui/panel.tscn"));
        }

        [Fact]
        public void StripModPrefix_LeavesOtherPathsUnchanged()
        {
            Assert.Equal("scenes/menu/launcher.tscn", ModRootFolders.StripModPrefix("scenes/menu/launcher.tscn"));
            Assert.Equal("mods/some_mod", ModRootFolders.StripModPrefix("mods/some_mod"));
            Assert.Equal("modsx/some_mod/a.png", ModRootFolders.StripModPrefix("modsx/some_mod/a.png"));
            Assert.Equal("", ModRootFolders.StripModPrefix(""));
            Assert.Null(ModRootFolders.StripModPrefix(null));
        }

        [Fact]
        public void TestRunner_UsesTheSameFolders()
        {
            Assert.Equal(ModRootFolders.Primary, ModFolders.Primary);
            Assert.Equal(ModRootFolders.Names, ModFolders.Names);
        }

        [Fact]
        public void CoreProject_ExcludesEveryFolderFromCompilation()
        {
            var project = File.ReadAllText(Path.Combine(RepoPaths.FindRepoRoot(), "Cthangover.csproj"));

            foreach (var name in ModRootFolders.Names)
                Assert.Contains($"<Compile Remove=\"{name}\\**\\*\" />", project);
        }

        [Fact]
        public void ExistingFolders_AreHiddenFromTheGodotImporter()
        {
            var root = RepoPaths.FindRepoRoot();

            foreach (var name in ModRootFolders.Names)
            {
                var folder = Path.Combine(root, name);
                if (Directory.Exists(folder))
                    Assert.True(File.Exists(Path.Combine(folder, ".gdignore")), $"{name}/.gdignore is missing");
            }
        }
    }
}
