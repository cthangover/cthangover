namespace Cthangover.Core.Mods.Compilation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The SDK reference assemblies shipped beside the core define one net10.0
    /// API contract for runtime mod/test compilation, including SDK-free players.
    /// Runtime implementation assemblies must never enter that reference set.
    /// Its content signature separates caches across framework/pack changes.
    /// </summary>
    public static class FrameworkReferencePack
    {
        /// <summary>The sole supported mod compilation target.</summary>
        public const string TargetFramework = "net10.0";

        private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Absolute directory copied from the SDK by ShipModReferencePack.</summary>
        public static string DirectoryPath { get; }

        /// <summary>Immutable framework metadata paths; none are runtime implementations.</summary>
        public static IReadOnlyList<string> AssemblyPaths { get; }

        /// <summary>Framework, servicing pack and content identity used by compiled caches.</summary>
        public static string CacheSignature { get; }

        static FrameworkReferencePack()
        {
            var location = typeof(FrameworkReferencePack).Assembly.Location;
            var coreDirectory = Path.GetDirectoryName(location);
            if (string.IsNullOrEmpty(coreDirectory))
                coreDirectory = ModReferenceProvider.BuildOutputDirectory;
            if (string.IsNullOrEmpty(coreDirectory))
                throw new InvalidOperationException("Warm the Godot core output path before compiling mods.");
            DirectoryPath = Path.Combine(coreDirectory,
                "mod_reference_assemblies", TargetFramework);
            var version = File.ReadAllText(Path.Combine(DirectoryPath, "pack.version")).Trim();
            if (!Version.TryParse(version, out var parsed) || parsed.Major != 10)
                throw new InvalidDataException("Runtime mod compilation requires a .NET 10 reference pack: " + DirectoryPath);
            var paths = Directory.GetFiles(DirectoryPath, "*.dll").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
            if (!File.Exists(Path.Combine(DirectoryPath, "System.Runtime.dll")) || !File.Exists(Path.Combine(DirectoryPath, "netstandard.dll")))
                throw new InvalidDataException("The shipped net10.0 reference pack is incomplete: " + DirectoryPath);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes(TargetFramework + ";" + version + "\n"));
            foreach (var path in paths)
            {
                Names.Add(Path.GetFileNameWithoutExtension(path));
                hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\n"));
                using var input = File.OpenRead(path);
                hash.AppendData(SHA256.HashData(input));
            }
            AssemblyPaths = Array.AsReadOnly(paths);
            CacheSignature = TargetFramework + ";pack=" + version + ";sha256=" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            GameLogger.Log("MODS_COMPILE", $"Framework reference pack: {CacheSignature}, directory='{DirectoryPath}', core location='{location}'");
        }

        /// <summary>Framework facade or private implementation names excluded from directory scans.</summary>
        public static bool IsFrameworkAssembly(string simpleName)
            => !string.IsNullOrEmpty(simpleName)
                && (Names.Contains(simpleName) || simpleName.StartsWith("System.Private.", StringComparison.Ordinal));
    }
}
