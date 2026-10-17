namespace Cthangover.Core.Mods
{
    using System.Collections.Generic;

    /// <summary>
    /// One mod compiled into the assembly cache but not loaded yet
    /// (<see cref="ModCompiler.PrepareAll"/>): the artifact path is the mod's
    /// code identity — same sources and dependencies give the same file — and
    /// the sources feed the script-type registry once the assembly loads.
    /// </summary>
    public sealed class ModCompileArtifact
    {
        /// <summary>Records one compiled mod; <paramref name="artifactPath"/> is the cache file <c>&lt;id&gt;_&lt;hash&gt;.dll</c>.</summary>
        public ModCompileArtifact(string modId, string artifactPath, List<(string FilePath, string Source)> sources)
        {
            ModId = modId;
            ArtifactPath = artifactPath;
            Sources = sources;
        }

        /// <summary>The compiled mod.</summary>
        public string ModId { get; }

        /// <summary>Content-addressed DLL in the assembly cache (PDB next to it).</summary>
        public string ArtifactPath { get; }

        /// <summary>The sources it was compiled from, for <see cref="ScriptTypeRegistry.RegisterAssembly"/>.</summary>
        public List<(string FilePath, string Source)> Sources { get; }
    }
}
