namespace Cthangover.Core.Mods.Pipeline
{
    using System.Collections.Generic;
    using Cthangover.Core.Games;

    /// <summary>
    /// The first, side-effect-free half of an in-process mod-set reload
    /// (<see cref="ModSetReloader.Prepare"/>): the new mod set discovered and
    /// filtered for <see cref="Selection"/>, every mod compiled into the cache
    /// and every precompiled DLL staged — but nothing loaded, unloaded or
    /// activated. <see cref="Succeeded"/> decides whether the running set may
    /// be replaced; a failed preparation is discarded and the game keeps
    /// running the previous code.
    /// </summary>
    public sealed class ModReloadPreparation
    {
        internal ModReloadPreparation(GameLaunchArgs selection, IReadOnlyCollection<string> force)
        {
            Selection = selection;
            Force = force ?? new List<string>();
        }

        /// <summary>The launch selection the new set was computed for.</summary>
        public GameLaunchArgs Selection { get; }

        /// <summary>Mods reloaded even when unchanged (<c>*</c> = every mod).</summary>
        public IReadOnlyCollection<string> Force { get; }

        /// <summary>Compile and staging errors; empty when the set may be applied.</summary>
        public List<string> Errors { get; } = new();

        /// <summary>True when every mod compiled and staged: only then may <see cref="ModSetReloader.Apply"/> replace the running set.</summary>
        public bool Succeeded => Errors.Count == 0;

        internal Dictionary<string, IModInfo> Mods { get; } = new();

        internal Dictionary<string, IModInfo> AllDiscovered { get; } = new();

        internal Dictionary<string, int> Priorities { get; } = new();

        internal ActiveGameChoice Choice { get; set; } = ActiveGameChoice.None;

        internal List<ModCompileArtifact> Compiled { get; set; } = new();

        internal List<(string ModId, string CachedPath)> Precompiled { get; set; } = new();

        /// <summary><see cref="Compilation.ModAssemblyCache.CurrentArtifacts"/> before the compile, restored when the preparation is discarded.</summary>
        internal Dictionary<string, string> PreviousArtifacts { get; set; } = new();
    }
}
