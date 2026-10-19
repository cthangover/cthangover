using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Cthangover.Core.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cthangover.Core.Mods.Compilation
{
    /// <summary>
    /// Host for the official Godot source generators inside runtime mod
    /// compilations: locates the generator assembly, keeps only its type
    /// list cached, and runs a fresh set of generator instances per
    /// compilation so every Godot-derived mod class gets its
    /// <c>[ScriptPath]</c> (plus the assembly marker) exactly like a normal
    /// <c>dotnet build</c>. Without this step engine lifecycle callbacks are
    /// never dispatched for mod nodes.
    /// </summary>
    public static class GodotGeneratorHost
    {
        private static readonly object _generatorLock = new object();
        private static Type[] _godotGeneratorTypes;
        private static readonly GeneratorLoadContext GeneratorContext = new GeneratorLoadContext();

        /// <summary>
        /// Locates the Godot source generator assembly shipped with the
        /// <c>godot.sourcegenerators</c> NuGet package (or copied next to the
        /// game binary).
        /// </summary>
        private static string ResolveGeneratorDll()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "Godot.SourceGenerators.dll"),
            };

            var packagesRoot = System.Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrEmpty(packagesRoot))
                packagesRoot = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages");
            var sgRoot = Path.Combine(packagesRoot, "godot.sourcegenerators");
            try
            {
                if (Directory.Exists(sgRoot))
                {
                    foreach (var version in Directory.GetDirectories(sgRoot).OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase))
                        candidates.Add(Path.Combine(version, "analyzers", "dotnet", "cs", "Godot.SourceGenerators.dll"));
                }
            }
            catch { }

            return candidates.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Creates FRESH generator instances for one compilation. The Godot
        /// generators are single-shot: a reused <c>ISourceGenerator</c>
        /// instance silently stops emitting (notably
        /// <c>ScriptPathAttributeGenerator</c>) on every driver run after the
        /// first one, so only the type list is cached.
        /// </summary>
        public static Microsoft.CodeAnalysis.ISourceGenerator[] GetGodotGenerators()
        {
            lock (_generatorLock)
            {
                if (_godotGeneratorTypes == null)
                {
                    string dllPath = null;
                    try
                    {
                        dllPath = ResolveGeneratorDll();
                        if (string.IsNullOrEmpty(dllPath))
                        {
                            GameLogger.Log("MODS_COMPILE", "ModCompiler: Godot.SourceGenerators.dll not found, mod scripts will lack lifecycle binding", LogLevel.Warning);
                            _godotGeneratorTypes = Array.Empty<Type>();
                            return Array.Empty<Microsoft.CodeAnalysis.ISourceGenerator>();
                        }

                        var generatorAssembly = GeneratorContext.LoadFromAssemblyPath(dllPath);
                        _godotGeneratorTypes = generatorAssembly.GetTypes()
                            .Where(t => !t.IsAbstract && typeof(Microsoft.CodeAnalysis.ISourceGenerator).IsAssignableFrom(t))
                            .ToArray();
                        GameLogger.Log("MODS_COMPILE", $"ModCompiler: Godot source generators loaded from '{dllPath}' ({_godotGeneratorTypes.Length} generator(s))");
                    }
                    catch (ReflectionTypeLoadException rtle)
                    {
                        var details = rtle.LoaderExceptions != null && rtle.LoaderExceptions.Length > 0
                            ? string.Join("\n  ", rtle.LoaderExceptions.Take(5).Select(le => le.GetType().FullName + ": " + le.Message))
                            : "(no loader exceptions) " + rtle + "\n  at " + rtle.StackTrace;
                        GameLogger.Log("MODS_COMPILE", $"ModCompiler: failed to load Godot source generators (dll '{dllPath}'):\n  {details}", LogLevel.Warning);
                        _godotGeneratorTypes = Array.Empty<Type>();
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log("MODS_COMPILE", $"ModCompiler: failed to load Godot source generators (dll '{dllPath}'): {ex.GetType().FullName}: {ex.Message}\n  at {ex.StackTrace}", LogLevel.Warning);
                        _godotGeneratorTypes = Array.Empty<Type>();
                    }
                }

                return _godotGeneratorTypes
                    .Select(t => (Microsoft.CodeAnalysis.ISourceGenerator)Activator.CreateInstance(t, nonPublic: true))
                    .ToArray();
            }
        }

        /// <summary>
        /// Isolated load context for the generator assembly. Every
        /// <c>Microsoft.CodeAnalysis*</c> request binds to the Roslyn instance
        /// the game itself runs on, so the loaded generator types implement the
        /// very <see cref="ISourceGenerator"/> this host filters by. Version-based
        /// binding (a plain <c>Assembly.LoadFrom</c>) may instead pull in or
        /// reject a second Roslyn copy depending on the host process, leaving the
        /// filter with zero matching types — silent loss of all generators.
        /// Everything else (<c>System.Collections.Immutable</c> first of all)
        /// resolves through the load context Roslyn itself lives in: Godot loads
        /// the game assembly into its own plugin context with app-local copies of
        /// those libraries, and a generator bound to the default context's copy
        /// sees a different <c>ImmutableArray&lt;T&gt;</c> — its analyzer overrides
        /// stop matching and the type load fails (seen on the Linux export).
        /// </summary>
        private sealed class GeneratorLoadContext : AssemblyLoadContext
        {
            private static readonly AssemblyLoadContext RoslynContext =
                GetLoadContext(typeof(Microsoft.CodeAnalysis.ISourceGenerator).Assembly) ?? Default;

            public GeneratorLoadContext()
                : base(nameof(GeneratorLoadContext))
            { }

            /// <inheritdoc />
            protected override Assembly Load(AssemblyName assemblyName)
            {
                var name = assemblyName.Name;
                if (name == null)
                    return null;

                if (name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
                {
                    // The game's own Roslyn wins: same instance the filter's
                    // typeof(ISourceGenerator) comes from. Collectible (mod) and
                    // dynamic assemblies must never leak into another context.
                    // A Roslyn part the game has not touched yet (Workspaces)
                    // is loaded into Roslyn's context below.
                    var loaded = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => !a.IsDynamic && !a.IsCollectible && a.GetName().Name == name);
                    if (loaded != null)
                        return loaded;
                }

                try
                {
                    return RoslynContext.LoadFromAssemblyName(assemblyName);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Runs the official Godot source generators over the mod compilation so
        /// every Godot-derived class gets <c>[ScriptPath]</c> (plus the assembly
        /// marker), exactly like the main project build. Without this, engine
        /// lifecycle callbacks are never dispatched for mod nodes.
        /// </summary>
        public static CSharpCompilation RunGodotGenerators(Microsoft.CodeAnalysis.ISourceGenerator[] generators, CSharpCompilation compilation, string modId)
        {
            if (generators == null || generators.Length == 0)
                return compilation;

            try
            {
                var driver = CSharpGeneratorDriver.Create(
                    generators,
                    null,
                    null,
                    new ModAnalyzerConfigOptionsProvider(ModReferenceProvider.ProjectRoot));
                driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

                foreach (var d in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(10))
                    GameLogger.Log("MODS_COMPILE", $"Godot generator ({modId}): {d.GetMessage()}", LogLevel.Warning);

                var genResult = (CSharpCompilation)output;
                var newTrees = genResult.SyntaxTrees.Count() - compilation.SyntaxTrees.Count();
                GameLogger.Log("MODS_COMPILE", $"Godot generator ({modId}): {newTrees} generated tree(s)", LogLevel.Debug);
                foreach (var tree in genResult.SyntaxTrees.Skip(compilation.SyntaxTrees.Count()).Take(40))
                    GameLogger.Log("MODS_COMPILE", $"Godot generator ({modId}): + '{tree.FilePath}'");

                return genResult;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_COMPILE", $"RunGodotGenerators('{modId}') failed: {ex.Message}\n{ex.StackTrace}", LogLevel.Warning);
                return compilation;
            }
        }
    }
}
