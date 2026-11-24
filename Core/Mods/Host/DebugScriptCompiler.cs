namespace Cthangover.Core.Mods.Host
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using Cthangover.Core.Mods.Compilation;
    using Godot;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Emit;

    /// <summary>Emits only in-memory artifacts, using the shipped .NET contract and selected game assemblies.</summary>
    internal sealed class DebugScriptCompiler
    {
        private const int MaxDiagnostics = 24;
        private const int MaxDiagnosticLength = 1500;
        private readonly string name;
        private readonly string source;
        private readonly IReadOnlyDictionary<string, Assembly> mods;
        private readonly CancellationToken cancellationToken;

        internal DebugScriptCompiler(string name, string source, IReadOnlyDictionary<string, Assembly> mods,
            CancellationToken cancellationToken)
        {
            this.name = name;
            this.source = source;
            this.mods = mods;
            this.cancellationToken = cancellationToken;
        }

        internal (byte[] Image, byte[] Symbols, JsonArray Diagnostics) Compile()
        {
            var references = new List<MetadataReference>();
            foreach (var file in FrameworkReferencePack.AssemblyPaths)
                references.Add(MetadataReference.CreateFromFile(file));
            AddGameReference(references, typeof(IDebugRuntimeScript).Assembly);
            AddGameReference(references, typeof(SceneTree).Assembly);

            foreach (var assembly in mods.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            var tree = CSharpSyntaxTree.ParseText(source, path: "debug-script.cs", encoding: Encoding.UTF8,
                cancellationToken: cancellationToken);
            var compilation = CSharpCompilation.Create(name, new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var image = new MemoryStream();
            using var symbols = new MemoryStream();
            var result = compilation.Emit(image, symbols,
                options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: name + ".pdb"),
                cancellationToken: cancellationToken);

            var diagnostics = new JsonArray();
            AddDiagnostics(result.Diagnostics, DiagnosticSeverity.Error, diagnostics);
            AddDiagnostics(result.Diagnostics, DiagnosticSeverity.Warning, diagnostics);
            return result.Success ? (image.ToArray(), symbols.ToArray(), diagnostics) : (null, null, diagnostics);
        }

        private static void AddGameReference(List<MetadataReference> references, Assembly assembly)
        {
            var path = assembly.Location;
            if (string.IsNullOrEmpty(path) && ModReferenceProvider.BuildOutputDirectory != null)
                path = Path.Combine(ModReferenceProvider.BuildOutputDirectory, assembly.GetName().Name + ".dll");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new InvalidOperationException("No runtime compilation reference for " + assembly.GetName().Name + ".");
            references.Add(MetadataReference.CreateFromFile(path));
        }

        private static void AddDiagnostics(IEnumerable<Diagnostic> source, DiagnosticSeverity severity, JsonArray destination)
        {
            foreach (var diagnostic in source)
            {
                if (destination.Count >= MaxDiagnostics)
                    return;
                if (diagnostic.Severity != severity)
                    continue;

                var message = diagnostic.GetMessage();
                var result = new JsonObject
                {
                    ["severity"] = severity == DiagnosticSeverity.Error ? "error" : "warning",
                    ["code"] = diagnostic.Id,
                    ["message"] = message.Length <= MaxDiagnosticLength ? message : message[..MaxDiagnosticLength]
                };
                if (diagnostic.Location.IsInSource)
                {
                    var position = diagnostic.Location.GetLineSpan();
                    result["file"] = position.Path;
                    result["line"] = position.StartLinePosition.Line + 1;
                    result["column"] = position.StartLinePosition.Character + 1;
                }
                destination.Add(result);
            }
        }
    }
}
