namespace Engine.Tests.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Compilation;
    using Cthangover.Core.Mods.Providers;
    using Microsoft.CodeAnalysis;

    /// <summary>Precompiled references follow discovery's selected provider, even with duplicate installations.</summary>
    public class ModReferenceProviderTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "mod_reference_roots_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void Collect_FrameworkReferencesComeFromTheShippedNet10Pack()
        {
            var references = new List<MetadataReference>();
            ModReferenceProvider.Collect(references, "framework_probe", Array.Empty<string>(), Array.Empty<IModInfo>());
            var paths = references.Select(ReferencePath).ToList();

            Assert.StartsWith("net10.0;pack=10.", FrameworkReferencePack.CacheSignature);
            Assert.NotEmpty(FrameworkReferencePack.AssemblyPaths);
            foreach (var path in FrameworkReferencePack.AssemblyPaths)
                Assert.Contains(path, paths);
            foreach (var path in paths)
            {
                if (FrameworkReferencePack.IsFrameworkAssembly(Path.GetFileNameWithoutExtension(path)))
                    Assert.StartsWith(FrameworkReferencePack.DirectoryPath + Path.DirectorySeparatorChar, path);
            }
            Assert.DoesNotContain(typeof(object).Assembly.Location, paths);
        }

        private static string ReferencePath(MetadataReference reference) => reference.Display;

        [Theory]
        [InlineData("mods")]
        [InlineData("mods_private")]
        public void Collect_UsesTheSelectedLibraryDirectory(string folder)
        {
            var selected = Path.Combine(root, folder, "different_folder_name");
            var other = Path.Combine(root, folder == "mods" ? "mods_private" : "mods", "library");
            Directory.CreateDirectory(Path.Combine(selected, "dll"));
            Directory.CreateDirectory(Path.Combine(other, "dll"));
            var selectedDll = Path.Combine(selected, "dll", "Library.dll");
            var otherDll = Path.Combine(other, "dll", "Library.dll");
            File.Copy(typeof(ModReferenceProvider).Assembly.Location, selectedDll);
            File.Copy(typeof(ModReferenceProvider).Assembly.Location, otherDll);
            using var provider = new FolderModFileProvider(selected, "library");
            var references = new List<MetadataReference>();

            ModReferenceProvider.Collect(references, "consumer", new[] { "library" }, new[]
            {
                new ModInfo { Id = "library", FileProvider = provider },
            });

            Assert.Contains(references, reference => reference.Display == selectedDll);
            Assert.DoesNotContain(references, reference => reference.Display == otherDll);
        }

        [Fact]
        public void ResolveSourcePath_ExternalProviderUsesItsOwnSourceLocation()
        {
            var selected = Path.Combine(root, "user", "mods", "library");
            Directory.CreateDirectory(Path.Combine(selected, "src"));
            var source = Path.Combine(selected, "src", "Widget.cs");
            File.WriteAllText(source, "class Widget { }");

            Assert.Equal(source, ModReferenceProvider.ResolveSourcePath("library", "src/Widget.cs", selected));
        }

        [Fact]
        public void GetScriptResourceRoot_ExternalAndSiblingDirectoriesHaveNoResourcePath()
        {
            var project = Path.Combine(root, "game");
            var external = Path.Combine(root, "user", "mods", "library");
            var sibling = project + "-other";
            Directory.CreateDirectory(external);
            Directory.CreateDirectory(sibling);

            Assert.Null(ModReferenceProvider.GetScriptResourceRoot(external, project));
            Assert.Null(ModReferenceProvider.GetScriptResourceRoot(sibling, project));
            Assert.Null(ModReferenceProvider.GetScriptResourceRoot(Path.Combine(root, "library.zip"), project));
        }

        [Theory]
        [InlineData("mods/library")]
        [InlineData("mods_private/library")]
        [InlineData("mods_private/library/test_suites/contracts")]
        public void GetScriptResourceRoot_ProjectProvidersRetainTheirActualResourceDirectory(string relative)
        {
            var project = Path.Combine(root, "game");
            var selected = Path.Combine(project, relative);
            Directory.CreateDirectory(selected);

            Assert.Equal(relative, ModReferenceProvider.GetScriptResourceRoot(selected, project));
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
