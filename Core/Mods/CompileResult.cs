using System.Collections.Generic;
using System.Reflection;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Result of a Roslyn C# compilation attempt. Carries either the
    /// loaded <c>Assembly</c> on success or the first set of error
    /// messages on failure. Used both by <c>ModCompiler.CompileString</c>
    /// (ad-hoc tool-side compilation) and by the full mod build pipeline
    /// which logs errors to <c>GameLogger.CompilationErrors</c> for
    /// display in the in-game error panel.
    /// </summary>
    public class CompileResult
    {
        /// <summary>True if Roslyn emitted the assembly without errors.</summary>
        public bool Success { get; set; }

        /// <summary>The loaded assembly, or null if compilation failed.</summary>
        public Assembly Assembly { get; set; }

        /// <summary>Compiler error messages (empty on success).</summary>
        public List<string> Errors { get; set; } = new();
    }
}
