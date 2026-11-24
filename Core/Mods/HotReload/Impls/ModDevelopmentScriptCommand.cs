namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Games;
    using Cthangover.Core.Games.LaunchParameters;
    using Cthangover.Core.Mods.Host;
    using Cthangover.Core.Mods.Unload;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Compiles a trusted debug script without registering a persistent mod or writing an assembly cache.</summary>
    public sealed class ModDevelopmentScriptCommand : IModDevelopmentCommand
    {
        private const int MaxSourceBytes = 32 * 1024;
        private const int MaxResultBytes = 128 * 1024;
        private const int DefaultTimeoutMs = 5000;
        private const int MaxModReferences = 16;

        /// <summary>Publishes the one-shot script operation in the kernel command catalog.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "script.run" };

        /// <summary>Describes how to opt in, supply code and resolve optional mod references.</summary>
        public string Description => "script.run: trusted C# source implementing one public IDebugRuntimeScript; optional mods (loaded mod IDs and their active dependencies), timeout_ms (100–30000); requires "
            + "--" + AllowDebugScriptsParameter.Name + " at startup. Returns value, diagnostics and unload state.";

        /// <summary>Compiles on a worker, invokes on the game thread, and verifies collection after execution.</summary>
        public async Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            if (!IsEnabled())
            {
                context.Succeeded = false;
                return Reply("disabled", "Start the game with --" + AllowDebugScriptsParameter.Name + " to execute C#.");
            }

            var source = context.Require("source");
            if (Encoding.UTF8.GetByteCount(source) > MaxSourceBytes)
                throw new ArgumentException("Script source exceeds 32 KiB.");
            var timeoutMs = context.Request["timeout_ms"]?.GetValue<int>() ?? DefaultTimeoutMs;
            if (timeoutMs < 100 || timeoutMs > 30000)
                throw new ArgumentException("timeout_ms must be between 100 and 30000.");

            var mods = LoadReferences(context.Request["mods"]);
            var name = "debug_script_" + Guid.NewGuid().ToString("N");
            using var timeout = new CancellationTokenSource(timeoutMs);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            var compiler = new DebugScriptCompiler(name, source, mods, cancellation.Token);
            (byte[] Image, byte[] Symbols, JsonArray Diagnostics) compilation;
            try
            {
                compilation = await Task.Run(compiler.Compile, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !token.IsCancellationRequested)
            {
                context.Succeeded = false;
                return Reply("timeout", "The script compilation exceeded its time budget.");
            }

            if (compilation.Image == null)
            {
                context.Succeeded = false;
                return Reply("compile", "The script did not compile.", compilation.Diagnostics);
            }

            if (!GameEnvironment.IsMainThread)
                throw new InvalidOperationException("The script dispatcher did not resume on the game thread.");
            if (GameSession.IsSwitching || ModManager.Instance.IsReloading || ModHotReloadService.IsReloadRunning
                || !ReferencesAreCurrent(mods, context.Request["mods"]))
            {
                context.Succeeded = false;
                return Reply("transition", "The active assemblies changed while the script compiled. Wait for the transition and retry.",
                    compilation.Diagnostics);
            }

            var scriptContext = new DebugScriptContext(context.Tree);
            var (reply, weakContext) = await InvokeAsync(name, compilation.Image, compilation.Symbols,
                mods, scriptContext, cancellation.Token);
            if (scriptContext.Changed)
                context.MarkChanged();

            reply["diagnostics"] = compilation.Diagnostics;
            if (reply["stage"].GetValue<string>() == "cancelled" && timeout.IsCancellationRequested && !token.IsCancellationRequested)
            {
                reply["stage"] = "timeout";
                reply["error"] = "The script execution exceeded its cooperative time budget.";
            }

            var ticket = new ModUnloadTicket(new[] { (name, weakContext) });
            var alive = await ticket.CollectAsync(context.Tree, frames: 5);
            reply["unload"] = alive.Count == 0 ? "collected" : "pending";
            context.Succeeded = reply["stage"].GetValue<string>() == "completed";
            return reply;
        }

        private static bool IsEnabled()
            => GameEnvironment.IsRuntime && GameEnvironment.IsMainThread
                && AllowsScripts(GameLaunchArgs.Startup) && AllowsScripts(GameLaunchArgs.Current);

        private static bool AllowsScripts(GameLaunchArgs args)
            => args.Contains<AllowDebugScriptsParameter>() && !args.Contains<NoDevelopmentControlParameter>();

        private static Dictionary<string, Assembly> LoadReferences(JsonNode selection)
        {
            var references = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            if (selection == null)
                return references;
            if (selection is not JsonArray items || items.Count > MaxModReferences)
                throw new ArgumentException("mods must be an array of at most 16 loaded mod IDs.");

            var ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (item is not JsonValue value || !value.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("Each mods entry must be a nonempty mod ID.");
                ids.Add(id);
            }

            var activeMods = new Dictionary<string, IModInfo>(ModRegistry.Instance.Mods, StringComparer.OrdinalIgnoreCase);
            var closure = new SortedSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                if (!activeMods.TryGetValue(id, out var candidate) || candidate is not ModInfo mod)
                    continue;
                foreach (var dependency in ModCompiler.DependencyClosure(mod, activeMods))
                {
                    if (activeMods.ContainsKey(dependency))
                        closure.Add(dependency);
                }
            }

            foreach (var record in ModAssemblyLoader.Records(closure))
            {
                var found = AddReferences(references, record.Assemblies);
                if (record.Context != null)
                    found |= AddReferences(references, record.Context.Assemblies);
                if (ids.Remove(record.ModId) && !found)
                    throw new ArgumentException("The loaded mod has no file-backed assemblies: " + record.ModId);
            }

            if (ids.Count != 0)
                throw new ArgumentException("Mods must already be loaded: " + string.Join(", ", ids));
            return references;
        }

        private static bool AddReferences(Dictionary<string, Assembly> references, IEnumerable<Assembly> assemblies)
        {
            var found = false;
            foreach (var assembly in assemblies)
            {
                if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location) || !File.Exists(assembly.Location))
                    continue;
                var name = assembly.GetName().Name;
                if (references.TryGetValue(name, out var existing) && !ReferenceEquals(existing, assembly))
                    throw new ArgumentException("Selected mods contain different assemblies named " + name + ".");
                references[name] = assembly;
                found = true;
            }
            return found;
        }

        private static bool ReferencesAreCurrent(Dictionary<string, Assembly> compiledReferences, JsonNode selection)
        {
            if (compiledReferences.Count == 0)
                return true;

            Dictionary<string, Assembly> current;
            try
            {
                current = LoadReferences(selection);
            }
            catch (ArgumentException)
            {
                return false;
            }
            if (current.Count != compiledReferences.Count)
                return false;
            foreach (var (name, assembly) in compiledReferences)
            {
                if (!current.TryGetValue(name, out var active) || !ReferenceEquals(active, assembly))
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<(JsonObject Reply, WeakReference Context)> InvokeAsync(string name, byte[] image,
            byte[] symbols, IReadOnlyDictionary<string, Assembly> mods, DebugScriptContext context, CancellationToken token)
        {
            var loadContext = new DebugScriptLoadContext(name, mods);
            var reference = new WeakReference(loadContext);
            IDebugRuntimeScript entry = null;
            var stage = "entry";
            try
            {
                token.ThrowIfCancellationRequested();
                entry = LoadEntry(loadContext, image, symbols);
                stage = "execute";
                var answer = await entry.RunAsync(context, token);
                token.ThrowIfCancellationRequested();
                var reply = Reply("completed");
                reply["value"] = Detach(answer);
                return (reply, reference);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return (Reply("cancelled", "The script was cancelled."), reference);
            }
            catch (Exception error)
            {
                var message = error.GetBaseException().Message;
                return (Reply(stage, message.Length <= 2000 ? message : message[..2000]), reference);
            }
            finally
            {
                entry = null;
                loadContext.Unload();
                loadContext = null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDebugRuntimeScript LoadEntry(DebugScriptLoadContext context, byte[] image, byte[] symbols)
        {
            using var assemblyStream = new MemoryStream(image, writable: false);
            using var symbolStream = new MemoryStream(symbols, writable: false);
            var assembly = context.LoadFromStream(assemblyStream, symbolStream);
            Type entry = null;
            foreach (var type in assembly.GetExportedTypes())
            {
                if (!type.IsClass || type.IsAbstract || !typeof(IDebugRuntimeScript).IsAssignableFrom(type))
                    continue;
                if (entry != null)
                    throw new InvalidOperationException("Define exactly one public IDebugRuntimeScript class.");
                entry = type;
            }

            if (entry == null || entry.ContainsGenericParameters || entry.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException("Define one public, non-generic IDebugRuntimeScript class with a public parameterless constructor.");
            return (IDebugRuntimeScript)Activator.CreateInstance(entry);
        }

        private static JsonNode Detach(JsonNode value)
        {
            if (value == null)
                return null;
            ValidateJson(value, 0);
            var json = value.ToJsonString();
            if (Encoding.UTF8.GetByteCount(json) > MaxResultBytes)
                throw new InvalidOperationException("The script result exceeds 128 KiB.");
            return JsonNode.Parse(json);
        }

        private static void ValidateJson(JsonNode value, int depth)
        {
            if (depth >= 64)
                throw new InvalidOperationException("The script result is too deeply nested.");
            if (value is JsonObject properties)
            {
                foreach (var property in properties)
                    ValidateJson(property.Value, depth + 1);
            }
            else if (value is JsonArray elements)
            {
                foreach (var element in elements)
                    ValidateJson(element, depth + 1);
            }
            else if (value is JsonValue scalar && scalar.GetValue<object>() is not (JsonElement or string or bool or
                sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal))
            {
                throw new InvalidOperationException("Return JSON primitives, arrays and objects from the script.");
            }
        }

        private static JsonObject Reply(string stage, string error = null, JsonArray diagnostics = null)
        {
            var reply = new JsonObject
            {
                ["stage"] = stage,
                ["value"] = null,
                ["diagnostics"] = diagnostics ?? new JsonArray(),
                ["unload"] = "not_loaded"
            };
            if (error != null)
                reply["error"] = error;
            return reply;
        }
    }
}
