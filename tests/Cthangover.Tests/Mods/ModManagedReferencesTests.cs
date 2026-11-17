namespace Engine.Tests.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Compilation;
    using Cthangover.Core.Mods.Providers;
    using Cthangover.Core.Utils;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    /// <summary>
    /// Managed reference declarations are persisted data, not an IDE-only promise: the selected
    /// provider must compile, stage and execute the same bytes, and changed libraries invalidate code.
    /// </summary>
    public sealed class ModManagedReferencesTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "cthangover_managed_" + Guid.NewGuid().ToString("N"));
        private readonly List<string> artifacts = new();
        private readonly List<string> modIds = new();
        private readonly List<IModFileProvider> providers = new();
        private readonly List<WeakReference> contexts = new();
        private ModInfo collecting;

        /// <summary>Explicit patterns survive a full manifest JSON cycle without changing legacy DLL ownership.</summary>
        [Fact]
        public void Manifest_RoundTrip_PreservesRequiredPatterns()
        {
            var original = new ModManifest
            {
                Id = "managed_fixture",
                References = new List<string>
                {
                    "lib/**/*.dll",
                    "vendor/Api.dll"
                }
            };
            var restored = JsonSerializer.Deserialize<ModManifest>(JsonSerializer.Serialize(original));
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.References, restored.References);
            Assert.Null(restored.DependsNative);
        }

        /// <summary>Loose and archived providers resolve identical paths and bytes, deduplicating legacy overlap.</summary>
        [Fact]
        public void Collect_FolderAndArchive_ReturnSameManagedFiles()
        {
            var mod = MakeMod("managed_collect", "lib/**/*.dll", "dll/*.dll");
            Write(mod, "lib/deep/Api.dll", new byte[] { 1, 2, 3 });
            Write(mod, "dll/Legacy.dll", new byte[] { 4, 5 });
            Write(mod, "test_suites/hidden/dll/Hidden.dll", new byte[] { 9 });
            var loose = ModManagedReferences.Collect(mod);
            Assert.Equal(2, loose.Count);
            Assert.Equal("dll/Legacy.dll", loose[0].FilePath);
            Assert.Equal("lib/deep/Api.dll", loose[1].FilePath);
            var zip = Path.Combine(root, "fixture.zip");
            ZipFile.CreateFromDirectory(mod.FileProvider.RootPath, zip);
            using var archived = new ZipModFileProvider(zip, mod.Id);
            mod.FileProvider = archived;
            var packed = ModManagedReferences.Collect(mod);
            Assert.Equal(loose.Count, packed.Count);
            for (var index = 0; index < loose.Count; index++)
            {
                Assert.Equal(loose[index].FilePath, packed[index].FilePath);
                Assert.Equal(loose[index].Bytes, packed[index].Bytes);
            }
        }

        /// <summary>Missing or escaping declarations fail explicitly rather than borrowing an unrelated DLL.</summary>
        [Fact]
        public void Collect_MissingAndEscapingPatterns_AreRejected()
        {
            var mod = MakeMod("managed_missing", "lib/*.dll");
            collecting = mod;
            Assert.Throws<FileNotFoundException>(CollectCurrent);
            mod.Manifest.References[0] = "../foreign/*.dll";
            Assert.Throws<ArgumentException>(CollectCurrent);
        }

        /// <summary>
        /// A DLL outside dll/ is visible in production compilation and executable through staged paths;
        /// replacing its bytes changes both producer and dependent artifacts without changing source/version.
        /// </summary>
        [Fact]
        public void PrepareAll_DeclaredLibrary_ExecutesAndInvalidatesDependent()
        {
            var owner = MakeMod("managed_owner", "lib/*.dll");
            var dependent = MakeMod("managed_consumer");
            owner.Manifest.Sources = new List<string> { "src/**/*.cs" };
            dependent.Manifest.Sources = new List<string> { "src/**/*.cs" };
            dependent.Manifest.Depends = new List<ModDependency> { ModDependency.Parse(owner.Id) };
            Write(owner, "lib/ManagedFixtureApi.dll", Library(7));
            WriteText(owner, "src/Read.cs", "public static class ManagedOwner { public static int Read() => ManagedFixtureApi.Value; }");
            WriteText(dependent, "src/Read.cs", "public static class ManagedConsumer { public static int Read() => ManagedOwner.Read(); }");
            var mods = new Dictionary<string, IModInfo>
            {
                [owner.Id] = owner,
                [dependent.Id] = dependent
            };
            var prepared = ModCompiler.PrepareAll(mods);
            Assert.Empty(GameLogger.CompilationErrors);
            Assert.Equal(2, prepared.Count);
            foreach (var entry in prepared)
            {
                artifacts.Add(entry.ArtifactPath);
            }

            var staged = ModAssemblyLoader.StagePrecompiledDlls(mods.Values);
            Assert.Single(staged);
            artifacts.Add(staged[0].CachedPath);
            Assert.Equal(7, ReadStaged(staged[0].CachedPath, prepared[0].ArtifactPath));
            Write(owner, "lib/ManagedFixtureApi.dll", Library(11));
            var changed = ModCompiler.PrepareAll(mods);
            Assert.Empty(GameLogger.CompilationErrors);
            Assert.Equal(2, changed.Count);
            Assert.NotEqual(prepared[0].ArtifactPath, changed[0].ArtifactPath);
            Assert.NotEqual(prepared[1].ArtifactPath, changed[1].ArtifactPath);
            foreach (var entry in changed)
            {
                artifacts.Add(entry.ArtifactPath);
            }

            var newStaged = ModAssemblyLoader.StagePrecompiledDlls(mods.Values);
            Assert.Single(newStaged);
            artifacts.Add(newStaged[0].CachedPath);
            Assert.NotEqual(staged[0].CachedPath, newStaged[0].CachedPath);
            Assert.Equal(11, ReadStaged(newStaged[0].CachedPath, changed[0].ArtifactPath));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadStaged(string library, string artifact)
        {
            var context = new ModLoadContext("managed_execution", null);
            contexts.Add(new WeakReference(context));
            try
            {
                context.StageAssembly(library);
                var name = context.StageAssembly(artifact);
                var assembly = context.LoadStaged(name);
                return (int)assembly.GetType("ManagedOwner").GetMethod("Read").Invoke(null, null);
            }
            finally
            {
                context.TryUnload();
            }
        }

        private void CollectCurrent()
        {
            ModManagedReferences.Collect(collecting);
        }

        private ModInfo MakeMod(string prefix, params string[] references)
        {
            var id = prefix + Guid.NewGuid().ToString("N");
            var directory = Path.Combine(root, id);
            Directory.CreateDirectory(directory);
            var provider = new FolderModFileProvider(directory, id);
            providers.Add(provider);
            modIds.Add(id);
            return new ModInfo
            {
                Id = id,
                FileProvider = provider,
                Manifest = new ModManifest
                {
                    Id = id,
                    Version = "1.0.0",
                    References = new List<string>(references)
                }
            };
        }

        private static byte[] Library(int value)
        {
            var references = new List<MetadataReference>();
            foreach (var path in FrameworkReferencePack.AssemblyPaths)
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }

            var tree = CSharpSyntaxTree.ParseText(
                "public static class ManagedFixtureApi { public static int Value => " + value + "; }");
            var compilation = CSharpCompilation.Create("ManagedFixtureApi", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var emitted = compilation.Emit(stream);
            Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
            return stream.ToArray();
        }

        private static void Write(ModInfo mod, string path, byte[] bytes)
        {
            var fullPath = Path.Combine(mod.FileProvider.RootPath, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, bytes);
        }

        private static void WriteText(ModInfo mod, string path, string text)
        {
            Write(mod, path, System.Text.Encoding.UTF8.GetBytes(text));
        }

        /// <summary>Closes providers and removes only this case's unique source/cache artifacts.</summary>
        public void Dispose()
        {
            foreach (var context in contexts)
            {
                Assert.Null(ModLoadContext.CollectAndVerify(context, "managed_execution"));
            }

            foreach (var provider in providers)
            {
                provider.Dispose();
            }

            foreach (var id in modIds)
            {
                ModAssemblyCache.CurrentArtifacts.TryRemove(id, out _);
                ModCompiler.InvalidateCache(id);
            }

            foreach (var artifact in artifacts)
            {
                File.Delete(artifact);
                File.Delete(Path.ChangeExtension(artifact, ".pdb"));
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
