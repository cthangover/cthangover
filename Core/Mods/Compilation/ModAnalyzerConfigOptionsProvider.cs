using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cthangover.Core.Mods.Compilation
{
    /// <summary>
    /// Analyzer config provider for runtime mod compilations: every syntax
    /// tree and additional file sees the same global options (the Godot
    /// generators only read global entries), so a single shared
    /// <see cref="ModAnalyzerConfigOptions"/> instance serves all lookups.
    /// </summary>
    internal sealed class ModAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly ModAnalyzerConfigOptions _options;

        public ModAnalyzerConfigOptionsProvider(string projectDir) => _options = new ModAnalyzerConfigOptions(projectDir);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }
}
