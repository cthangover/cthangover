namespace Cthangover.Core.Mods.Compilation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Reflection.Metadata;
    using System.Reflection.PortableExecutable;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using Cthangover.Core.Utils;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    /// <summary>
    /// Pins runtime compilation to the reference assembly built with the loaded core. Its
    /// hash preserves cache entries across implementation changes, while stale or absent
    /// reference files fall back to the loaded implementation's module version id.
    /// </summary>
    internal static class CoreAssemblyContract
    {
        private static readonly Assembly CoreAssembly = typeof(ModRegistry).Assembly;
        private static readonly string CoreAssemblyName = CoreAssembly.GetName().Name;
        private static readonly string ImplementationSignature = "mvid:" + CoreAssembly.ManifestModule.ModuleVersionId.ToString("N");
        private static readonly string ToolchainSignature = ";platform=" + RuntimeInformation.RuntimeIdentifier
            + ";godot=" + typeof(Godot.Node).Assembly.ManifestModule.ModuleVersionId.ToString("N")
            + ";roslyn=" + typeof(Microsoft.CodeAnalysis.Compilation).Assembly.ManifestModule.ModuleVersionId.ToString("N")
            + "/" + typeof(CSharpCompilation).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        private static readonly Lazy<(string Signature, PortableExecutableReference Reference, string Directory)> Contract = new(Resolve);

        /// <summary>Identifies the core metadata, declared runtime dependencies, and compiler used by mod and test caches.</summary>
        internal static string CacheSignature => CanResolve ? Contract.Value.Signature : ImplementationSignature + ToolchainSignature;

        /// <summary>Returns the pinned core metadata, or null when only the ordinary scan can supply a reference.</summary>
        internal static PortableExecutableReference Reference => CanResolve ? Contract.Value.Reference : null;

        /// <summary>Physical directory of the loaded core, also used to resolve its companion framework pack.</summary>
        internal static string ImplementationDirectory => CanResolve ? Contract.Value.Directory : null;

        /// <summary>Determines whether a scan result would duplicate the core metadata already selected.</summary>
        internal static bool IsPinnedAssembly(string name)
            => string.Equals(name, CoreAssemblyName, StringComparison.OrdinalIgnoreCase) && Reference != null;

        // Godot can load the core from bytes. Avoid retaining a fallback before its main-thread path setup.
        private static bool CanResolve => CoreAssembly.Location.Length != 0 || ModReferenceProvider.PathsWarmed;

        private static (string Signature, PortableExecutableReference Reference, string Directory) Resolve()
        {
            var mvid = CoreAssembly.ManifestModule.ModuleVersionId;
            foreach (var path in CandidateImplementations())
            {
                try
                {
                    if (!File.Exists(path))
                        continue;
                    var implementation = File.ReadAllBytes(path);
                    using var stream = new MemoryStream(implementation, writable: false);
                    using var image = new PEReader(stream);
                    if (!image.HasMetadata)
                        continue;
                    var metadata = image.GetMetadataReader();
                    if (metadata.GetGuid(metadata.GetModuleDefinition().Mvid) != mvid)
                        continue;

                    var dependencies = ReadDependenciesHash(path);
                    var directory = Path.GetDirectoryName(path);
                    var reference = ReadVerifiedReference(path, implementation, out var referenceHash);
                    if (reference != null)
                    {
                        GameLogger.Log("MODS_COMPILE", $"Using core reference contract {referenceHash} from '{reference.FilePath}'", LogLevel.Debug);
                        return ("abi:" + referenceHash + ToolchainSignature + dependencies, reference, directory);
                    }

                    GameLogger.Log("MODS_COMPILE", $"Core reference contract missing or stale beside '{path}'; using loaded core MVID", LogLevel.Debug);
                    return (ImplementationSignature + ToolchainSignature + dependencies,
                        MetadataReference.CreateFromImage(implementation, filePath: path), directory);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (BadImageFormatException) { }
            }

            GameLogger.Log("MODS_COMPILE", "No on-disk core matches the loaded assembly; using loaded core MVID", LogLevel.Debug);
            return (ImplementationSignature + ToolchainSignature, null, null);
        }

        private static IEnumerable<string> CandidateImplementations()
        {
            if (CoreAssembly.Location.Length != 0)
                yield return CoreAssembly.Location;

            var name = CoreAssemblyName + ".dll";
            yield return Path.Combine(AppContext.BaseDirectory, name);
            var compilerDirectory = Path.GetDirectoryName(typeof(CSharpCompilation).Assembly.Location);
            if (!string.IsNullOrEmpty(compilerDirectory))
                yield return Path.Combine(compilerDirectory, name);

            var projectRoot = ModReferenceProvider.ProjectRoot;
            if (projectRoot != null)
            {
                foreach (var config in ModReferenceProvider.BuildConfigurations)
                    yield return Path.Combine(projectRoot, ".godot", "mono", "temp", "bin", config, name);
            }
        }

        private static PortableExecutableReference ReadVerifiedReference(string path, byte[] implementation, out string referenceHash)
        {
            referenceHash = null;
            var contractDirectory = Path.Combine(Path.GetDirectoryName(path), "mod_reference_assemblies", "core");
            var receiptPath = Path.Combine(contractDirectory, "contract.sha256");
            var referencePath = Path.Combine(contractDirectory, CoreAssemblyName + ".dll");

            try
            {
                if (!File.Exists(receiptPath) || !File.Exists(referencePath))
                    return null;

                var checksums = File.ReadAllLines(receiptPath);
                if (checksums.Length != 2 || !string.Equals(checksums[0], ComputeHash(implementation), StringComparison.OrdinalIgnoreCase))
                    return null;

                var reference = File.ReadAllBytes(referencePath);
                var hash = ComputeHash(reference);
                if (!string.Equals(checksums[1], hash, StringComparison.OrdinalIgnoreCase))
                    return null;

                using var stream = new MemoryStream(reference, writable: false);
                using var image = new PEReader(stream);
                if (!image.HasMetadata)
                    return null;
                var metadata = image.GetMetadataReader();
                if (!metadata.IsAssembly || !metadata.StringComparer.Equals(metadata.GetAssemblyDefinition().Name, CoreAssemblyName))
                    return null;

                var metadataReference = MetadataReference.CreateFromImage(reference, filePath: referencePath);
                referenceHash = hash;
                return metadataReference;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (BadImageFormatException) { }
            return null;
        }

        private static string ReadDependenciesHash(string implementationPath)
        {
            var dependenciesPath = Path.ChangeExtension(implementationPath, ".deps.json");
            try
            {
                if (File.Exists(dependenciesPath))
                {
                    using var input = File.OpenRead(dependenciesPath);
                    return ";deps=" + Convert.ToHexStringLower(SHA256.HashData(input));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return string.Empty;
        }

        private static string ComputeHash(byte[] image) => Convert.ToHexStringLower(SHA256.HashData(image));
    }
}
