namespace Cthangover.Tests.Mods.Unload
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using Cthangover.Core.Mods;

    /// <summary>
    /// Compiles a throw-away mod from source into its own collectible
    /// <see cref="ModLoadContext"/>, exactly as the boot pipeline does (cache
    /// artifact + file-backed load inside a loader batch). The batch gate is
    /// process-wide, so every test using this belongs to the
    /// "mod-load-context" collection (no parallel compile inside the window).
    /// Callers must keep the returned assembly out of locals of the method
    /// that later verifies collection — a Debug-JIT local pins the context.
    /// </summary>
    public static class TestMods
    {
        /// <summary>A unique mod id, so parallel test classes never share cache artifacts.</summary>
        public static string NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 12);

        /// <summary>Compiles <paramref name="source"/> as mod <paramref name="modId"/>; fails the test on a compile error.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Assembly Compile(string modId, string source)
        {
            using (ModAssemblyLoader.BeginBatch())
            {
                var assembly = ModCompiler.Compile(modId, new[] { ("Probe.cs", source) });
                Assert.NotNull(assembly);
                return assembly;
            }
        }
    }
}
