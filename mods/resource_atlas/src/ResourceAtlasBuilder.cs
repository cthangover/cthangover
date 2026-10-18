using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cthangover.Core.Mods;
using Cthangover.Validator;

namespace Cthangover.ResourceAtlas
{
    /// <summary>
    /// Builds the cross-mod resource map. Keys follow the runtime resolvers so
    /// the atlas answers the same question the game does — "which mod serves
    /// this key?":
    /// <list type="bullet">
    /// <item>validator graph — entities, scene definitions, interactives and
    /// the background/avatar/music/sound/effect assets, plus every reference
    /// found in scenarios and data;</item>
    /// <item>textures — basename across <c>ModConfig.TextureGroups</c> (one
    /// global namespace, like <c>TextureResolver</c>);</item>
    /// <item>shaders (<c>shaders/</c> path without extension), <c>.tscn</c>
    /// scenes (path under <c>scenes/</c>), locale keys per language, scenario
    /// actions and entity kinds registered by mod code;</item>
    /// <item>requests from C# literals (<c>PlaySound</c>, <c>PlayMusic</c>,
    /// <c>PlayAmbient</c>, <c>Textures.Resolve</c>, <c>Shaders.Resolve</c>),
    /// <c>res://mods/&lt;id&gt;/</c> paths, scenario <c>action</c> commands and
    /// entity data needing a kind;</item>
    /// <item>patches — <c>patches/scenes/**.tscn</c> and <c>patches/&lt;kind&gt;.json</c>.</item>
    /// </list>
    /// A link between two mods is "declared" when the dependant has the other
    /// mod in its transitive <c>depends</c>; for shared keys the later mod in
    /// load order wins, which is only deterministic along a depends path.
    /// </summary>
    public static class ResourceAtlasBuilder
    {
        private static readonly Regex ResModReference = new(@"res://mods/([A-Za-z0-9_]+)/([^""'\s)\]>,;]+)", RegexOptions.Compiled);
        private static readonly Regex ScenarioAction = new(@"^\s*action\s+([a-z_]+\.[a-z_]+)", RegexOptions.Compiled);
        private static readonly Regex CSharpActionName = new(@"Name\s*=>\s*""([a-z_]+\.[a-z_]+)""", RegexOptions.Compiled);
        private static readonly Regex GdActionName = new(@"func\s+get_name\s*\(\s*\)\s*:\s*\n\s*return\s+""([a-z_]+\.[a-z_]+)""", RegexOptions.Compiled);
        private static readonly Regex EntityKindAttribute = new(@"EntityKind\(""([a-z_]+)""", RegexOptions.Compiled);
        private static readonly Regex PatchTarget = new(@"\[ext_resource[^\]]*type=""PackedScene""[^\]]*path=""([^""]+)""[^\]]*id=""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex RootInstance = new(@"\[node name=""[^""]+""\s+instance=ExtResource\(""([^""]+)""\)\]", RegexOptions.Compiled);

        private static readonly (string Kind, Regex Pattern)[] CodeRequests =
        {
            ("sound", new Regex(@"PlaySound\(\s*""([^""]+)""(?:\s*,\s*(\d+))?", RegexOptions.Compiled)),
            ("music", new Regex(@"Play(?:Music|Ambient)\(\s*""([^""]+)""", RegexOptions.Compiled)),
            ("texture", new Regex(@"Textures\.Resolve[A-Za-z]*\(\s*""([^""]+)""", RegexOptions.Compiled)),
            ("shader", new Regex(@"Shaders\.Resolve[A-Za-z]*\(\s*""([^""]+)""", RegexOptions.Compiled)),
            ("locale_key", new Regex(@"\b(?:Translate|Tr)\(\s*""([a-z_]+(?:/[A-Za-z0-9_]+)+)""", RegexOptions.Compiled)),
            ("locale_key", new Regex(@"\bLocale\s*=\s*""([a-z_]+(?:/[A-Za-z0-9_]+)+)""", RegexOptions.Compiled)),
        };

        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".tscn", ".tres", ".json", ".scenario", ".gd", ".cs", ".gdshader",
        };

        /// <param name="runtimeActions">Registered scenario actions with the
        /// name of the assembly that declares them: a mod assembly is named by
        /// its mod id, anything else is the kernel and always available.</param>
        /// <param name="outsideMods">Installed mods that are not loaded (the
        /// atlas runs inside a game); with <paramref name="outsideGraph"/> they
        /// only contribute provisions, used to explain keys the game misses.</param>
        public static ResourceAtlasResult Build(
            IReadOnlyList<IModInfo> modsInLoadOrder,
            ValidationGraph graph,
            IReadOnlyCollection<(string Name, string Assembly)> runtimeActions = null,
            IReadOnlyList<IModInfo> outsideMods = null,
            ValidationGraph outsideGraph = null)
        {
            var result = new ResourceAtlasResult();
            foreach (var mod in modsInLoadOrder)
            {
                result.LoadOrder.Add(mod.Id);
                result.Depends[mod.Id] = (mod as ModInfo)?.Manifest?.DependencyIds ?? new List<string>();
            }

            foreach (var id in result.LoadOrder)
                result.Closures[id] = Closure(id, result.Depends);

            foreach (var declaration in graph.Declarations)
                result.Provisions.Add(new AtlasProvision { Kind = declaration.Kind, Id = declaration.Id, ModId = declaration.ModId, File = declaration.File });

            foreach (var reference in graph.References)
            {
                if (reference.Kind == ValidationKind.ScenePath)
                    continue; // manifest scene_path is always mod-relative
                result.Requests.Add(new AtlasRequest
                {
                    Kind = reference.Kind, Id = reference.TargetId, ModId = reference.ModId,
                    File = reference.File, Line = reference.Line, Via = "data",
                });
            }

            foreach (var mod in modsInLoadOrder)
                ScanMod(mod, result, result.Provisions, collectRequests: true);

            foreach (var mod in outsideMods ?? Array.Empty<IModInfo>())
                ScanMod(mod, result, result.OutsideProvisions, collectRequests: false);
            foreach (var declaration in outsideGraph?.Declarations ?? Enumerable.Empty<ValidationDeclaration>())
                result.OutsideProvisions.Add(new AtlasProvision { Kind = declaration.Kind, Id = declaration.Id, ModId = declaration.ModId, File = declaration.File });

            foreach (var (name, assembly) in runtimeActions ?? Array.Empty<(string, string)>())
            {
                if (result.Depends.ContainsKey(assembly ?? string.Empty))
                    Provide(result, "action", name, assembly, "<runtime>");
                else
                    result.KernelKeys.Add(("action", name));
            }

            Analyze(result);
            return result;
        }

        /// <summary>
        /// Indexes one mod. Provisions go to <paramref name="provisions"/>
        /// (loaded or outside list); requests and patches are collected only
        /// for loaded mods.
        /// </summary>
        private static void ScanMod(IModInfo mod, ResourceAtlasResult result, List<AtlasProvision> provisions, bool collectRequests)
        {
            var provider = mod.FileProvider;
            if (provider == null)
                return;

            var target = collectRequests ? result : new ResourceAtlasResult();
            void Provide(ResourceAtlasResult _, string kind, string id, string modId, string file)
                => provisions.Add(new AtlasProvision { Kind = kind, Id = id, ModId = modId, File = file });

            var textureExtensions = ModConfig.Instance.GetTextureExtensionSet();
            foreach (var group in ModConfig.Instance.TextureGroups)
            {
                foreach (var file in ModFileReader.ListFiles(provider, group, true))
                {
                    if (ModFileReader.HasExtension(file, textureExtensions))
                        Provide(result, "texture", Path.GetFileNameWithoutExtension(file), mod.Id, file);
                }
            }

            foreach (var file in ModFileReader.ListFiles(provider, "shaders", true))
            {
                if (file.EndsWith(".gdshader", StringComparison.OrdinalIgnoreCase))
                    Provide(result, "shader", StripExtension(file.Substring("shaders/".Length)), mod.Id, file);
            }

            foreach (var file in ModFileReader.ListFiles(provider, "scenes", true))
            {
                if (file.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                    Provide(result, "scene_file", file.Substring("scenes/".Length), mod.Id, file);
            }

            foreach (var file in ModFileReader.ListFiles(provider, "scenarios", true))
            {
                // Scenarios are identified by file name: a mod replaces another
                // mod's scenario by shipping the same name and depending on it.
                if (file.EndsWith(".scenario", StringComparison.OrdinalIgnoreCase))
                    Provide(result, "scenario", Path.GetFileNameWithoutExtension(file), mod.Id, file);
            }

            foreach (var file in ModFileReader.ListFiles(provider, "locale", true))
            {
                if (file.EndsWith(".properties", StringComparison.OrdinalIgnoreCase))
                    ScanLocale(provider, file, mod.Id, provisions);
            }

            foreach (var file in ModFileReader.ListFiles(provider, string.Empty, true))
            {
                if (!TextExtensions.Contains(Path.GetExtension(file)) || IsToolingPath(file))
                    continue;

                var text = provider.ReadFileText(file);
                if (string.IsNullOrEmpty(text))
                    continue;
                text = text.Replace("\r\n", "\n");
                var isCode = file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                if (isCode)
                    text = StripCSharpComments(text);

                ScanResReferences(text, file, mod.Id, target);

                if (isCode)
                    ScanCode(text, file, mod.Id, target, provisions);
                else if (file.EndsWith(".gd", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (Match match in GdActionName.Matches(text))
                        Provide(result, "action", match.Groups[1].Value, mod.Id, file);
                }
                else if (file.EndsWith(".scenario", StringComparison.OrdinalIgnoreCase))
                    ScanScenario(text, file, mod.Id, target);
                else if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    ScanJson(text, file, mod.Id, target);
                else if (file.StartsWith("patches/scenes/", StringComparison.Ordinal) && file.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                    ScanScenePatch(text, file, mod.Id, target);
            }
        }

        /// <summary>Tests and build output are not shipped content; their literals must not count as requests.</summary>
        private static bool IsToolingPath(string file)
            => file.StartsWith("tests/", StringComparison.Ordinal)
               || file.StartsWith("test_suites/", StringComparison.Ordinal) // mounted as their own mod (<host>__<suite>)
               || file.StartsWith("source_code/", StringComparison.Ordinal)
               || file.Contains("/bin/", StringComparison.Ordinal)
               || file.Contains("/obj/", StringComparison.Ordinal);

        private static void ScanLocale(IModFileProvider provider, string file, string modId, List<AtlasProvision> provisions)
        {
            var language = Path.GetFileNameWithoutExtension(file);
            var underscore = language.LastIndexOf('_');
            if (underscore >= 0)
                language = language.Substring(underscore + 1);

            var text = provider.ReadFileText(file) ?? string.Empty;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("!", StringComparison.Ordinal))
                    continue;
                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;
                var key = line.Substring(0, separator).Trim();
                provisions.Add(new AtlasProvision { Kind = "locale", Id = $"{language}:{key}", ModId = modId, File = file });
                // Language-agnostic key: what code asks for (Translate/Tr/Locale);
                // overrides are analysed per language on "locale" only.
                provisions.Add(new AtlasProvision { Kind = "locale_key", Id = key, ModId = modId, File = file });
            }
        }

        private static void ScanResReferences(string text, string file, string modId, ResourceAtlasResult result)
        {
            foreach (Match match in ResModReference.Matches(text))
            {
                result.Requests.Add(new AtlasRequest
                {
                    Kind = "file", Id = $"{match.Groups[1].Value}/{match.Groups[2].Value}", ModId = modId,
                    File = file, Line = LineOf(text, match.Index), Via = "res", ExplicitProvider = match.Groups[1].Value,
                });
            }
        }

        private static void ScanCode(string text, string file, string modId, ResourceAtlasResult result, List<AtlasProvision> provisions)
        {
            foreach (Match match in CSharpActionName.Matches(text))
                provisions.Add(new AtlasProvision { Kind = "action", Id = match.Groups[1].Value, ModId = modId, File = file });

            foreach (Match match in EntityKindAttribute.Matches(text))
                provisions.Add(new AtlasProvision { Kind = "entity_kind", Id = match.Groups[1].Value, ModId = modId, File = file });

            foreach (var (kind, pattern) in CodeRequests)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    // PlaySound(id, n) with n > 1 picks id_1..id_n at random (AudioService).
                    var variations = match.Groups.Count > 2 && int.TryParse(match.Groups[2].Value, out var n) ? n : 1;
                    var keys = variations > 1
                        ? Enumerable.Range(1, variations).Select(i => $"{match.Groups[1].Value}_{i}")
                        : new[] { match.Groups[1].Value };
                    foreach (var key in keys)
                    {
                        result.Requests.Add(new AtlasRequest
                        {
                            Kind = kind, Id = key, ModId = modId,
                            File = file, Line = LineOf(text, match.Index), Via = "code",
                        });
                    }
                }
            }
        }

        private static void ScanScenario(string text, string file, string modId, ResourceAtlasResult result)
        {
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var match = ScenarioAction.Match(lines[i]);
                if (match.Success)
                {
                    result.Requests.Add(new AtlasRequest
                    {
                        Kind = "action", Id = match.Groups[1].Value, ModId = modId,
                        File = file, Line = i + 1, Via = "scenario",
                    });
                }
            }
        }

        private static void ScanJson(string text, string file, string modId, ResourceAtlasResult result)
        {
            if (file == "manifest.json" || file.StartsWith("collections/", StringComparison.Ordinal))
                return;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException)
            {
                return;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return;

                var isPatch = file.StartsWith("patches/", StringComparison.Ordinal);
                if (isPatch)
                {
                    ScanEntityPatch(root, file, modId, result);
                    return;
                }

                if (root.TryGetProperty("Kind", out var kind) && kind.ValueKind == JsonValueKind.String)
                {
                    result.Requests.Add(new AtlasRequest
                    {
                        Kind = "entity_kind", Id = kind.GetString(), ModId = modId, File = file, Line = 0, Via = "entity_data",
                    });
                }
            }
        }

        /// <summary><c>patches/&lt;kind&gt;.json</c> or <c>patches/&lt;kind&gt;/*.json</c>: each item id (or <c>*</c>) patches entities of that kind.</summary>
        private static void ScanEntityPatch(JsonElement root, string file, string modId, ResourceAtlasResult result)
        {
            var relative = file.Substring("patches/".Length);
            var kind = relative.Contains('/') ? relative.Substring(0, relative.IndexOf('/')) : StripExtension(relative);
            if (!root.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                var id = JsonReadHelper.GetString(item, "Id", "ID", "id");
                if (!string.IsNullOrEmpty(id))
                    result.Patches.Add(new AtlasPatch { Kind = kind, Id = id, ModId = modId, File = file });
            }
        }

        /// <summary>A scene patch is an inherited scene: the root instance names the patched target.</summary>
        private static void ScanScenePatch(string text, string file, string modId, ResourceAtlasResult result)
        {
            var root = RootInstance.Match(text);
            if (!root.Success)
                return;

            foreach (Match match in PatchTarget.Matches(text))
            {
                if (match.Groups[2].Value != root.Groups[1].Value)
                    continue;

                var target = match.Groups[1].Value;
                if (target.StartsWith("res://", StringComparison.Ordinal))
                    target = target.Substring("res://".Length);

                string owner = null;
                if (target.StartsWith("mods/", StringComparison.Ordinal))
                {
                    var parts = target.Split('/', 3);
                    owner = parts.Length > 1 ? parts[1] : null;
                    target = parts.Length > 2 ? parts[2] : string.Empty;
                }

                if (target.StartsWith("scenes/", StringComparison.Ordinal))
                    target = target.Substring("scenes/".Length);

                result.Patches.Add(new AtlasPatch { Kind = "scene_file", Id = target, ModId = modId, File = file, ExplicitOwner = owner });
            }
        }

        private static void Analyze(ResourceAtlasResult result)
        {
            var loadIndex = result.LoadOrder.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index, StringComparer.Ordinal);
            var providers = result.Provisions
                .Where(p => loadIndex.ContainsKey(p.ModId))
                .GroupBy(p => (p.Kind, p.Id))
                .ToDictionary(g => g.Key, g => g.Select(p => p.ModId).Distinct().OrderBy(m => loadIndex[m]).ToList());

            foreach (var request in result.Requests)
                AnalyzeRequest(result, request, providers, loadIndex);

            foreach (var pair in providers.Where(p => p.Value.Count > 1 && p.Key.Kind != "locale_key"))
                AnalyzeOverride(result, pair.Key.Kind, pair.Key.Id, pair.Value);

            foreach (var patch in result.Patches)
                AnalyzePatch(result, patch, providers, loadIndex);
        }

        private static void AnalyzeRequest(
            ResourceAtlasResult result,
            AtlasRequest request,
            Dictionary<(string, string), List<string>> providers,
            Dictionary<string, int> loadIndex)
        {
            List<string> candidates;
            if (request.ExplicitProvider != null)
                candidates = loadIndex.ContainsKey(request.ExplicitProvider) ? new List<string> { request.ExplicitProvider } : new List<string>();
            else if (!providers.TryGetValue((request.Kind, request.Id), out candidates))
                candidates = new List<string>();

            if (candidates.Contains(request.ModId) || result.KernelKeys.Contains((request.Kind, request.Id)))
                return;

            if (candidates.Count == 0)
            {
                var outside = result.OutsideProvisions
                    .Where(p => p.Kind == request.Kind && p.Id == request.Id)
                    .Select(p => p.ModId).Distinct().ToList();
                if (request.ExplicitProvider != null && !loadIndex.ContainsKey(request.ExplicitProvider)
                    && result.OutsideProvisions.Any(p => p.ModId == request.ExplicitProvider))
                    outside = new List<string> { request.ExplicitProvider };
                if (outside.Count > 0)
                {
                    result.Findings.Add(new AtlasFinding
                    {
                        Kind = AtlasFindingKind.OutsideGame, Severity = AtlasSeverity.Error, ModId = request.ModId,
                        OtherMods = outside,
                        ResourceKind = request.Kind, ResourceId = request.Id, File = request.File, Line = request.Line,
                        Message = $"ресурс есть только в моде {string.Join("/", outside)}, который не входит в игру — добавьте мод в коллекцию или перенесите ресурс",
                    });
                    return;
                }

                result.Findings.Add(new AtlasFinding
                {
                    Kind = AtlasFindingKind.Unresolved, Severity = AtlasSeverity.Warning, ModId = request.ModId,
                    ResourceKind = request.Kind, ResourceId = request.Id, File = request.File, Line = request.Line,
                    Message = request.ExplicitProvider != null
                        ? $"ссылается на мод '{request.ExplicitProvider}', который не загружен"
                        : "ни один загруженный мод не предоставляет этот ключ",
                });
                return;
            }

            var closure = result.Closures[request.ModId];
            var declared = candidates.Any(closure.Contains);
            result.Findings.Add(new AtlasFinding
            {
                Kind = declared ? AtlasFindingKind.DeclaredUse : AtlasFindingKind.UndeclaredUse,
                Severity = declared ? AtlasSeverity.Info : AtlasSeverity.Error,
                ModId = request.ModId,
                OtherMods = candidates,
                ResourceKind = request.Kind, ResourceId = request.Id, File = request.File, Line = request.Line,
                Message = declared
                    ? $"использует ресурс мода {string.Join("/", candidates.Where(closure.Contains))} ({request.Via})"
                    : $"использует ресурс мода {string.Join("/", candidates)} ({request.Via}), но его нет в depends — добавьте зависимость или перенесите ресурс",
            });
        }

        /// <summary>
        /// The last provider in load order wins. A loser is overridden on purpose
        /// only when the winner depends on it; scene definitions merge instead
        /// (the first provider is the base, later ones extend it), which is
        /// equally order-sensitive.
        /// </summary>
        private static void AnalyzeOverride(ResourceAtlasResult result, string kind, string id, List<string> ordered)
        {
            var merges = kind == ValidationKind.Scene;
            var winner = merges ? ordered[0] : ordered[^1];
            foreach (var other in ordered.Where(m => m != winner))
            {
                var later = merges ? other : winner;
                var earlier = merges ? winner : other;
                var declared = result.Closures[later].Contains(earlier);
                result.Findings.Add(new AtlasFinding
                {
                    Kind = declared ? AtlasFindingKind.DeclaredOverride : AtlasFindingKind.UnorderedOverride,
                    Severity = declared ? AtlasSeverity.Info : AtlasSeverity.Error,
                    ModId = later,
                    OtherMods = new List<string> { earlier },
                    ResourceKind = kind, ResourceId = id,
                    File = result.Provisions.First(p => p.Kind == kind && p.Id == id && p.ModId == later).File,
                    Message = (merges ? $"расширяет определение мода {earlier}" : $"переопределяет ресурс мода {earlier}")
                        + (declared ? string.Empty : " без зависимости между ними — результат зависит от случайного порядка загрузки; переименуйте ключ или объявите depends"),
                });
            }
        }

        private static void AnalyzePatch(
            ResourceAtlasResult result,
            AtlasPatch patch,
            Dictionary<(string, string), List<string>> providers,
            Dictionary<string, int> loadIndex)
        {
            List<string> owners;
            if (patch.ExplicitOwner != null)
                owners = loadIndex.ContainsKey(patch.ExplicitOwner) ? new List<string> { patch.ExplicitOwner } : new List<string>();
            else if (patch.Id == "*")
                owners = providers.Where(p => p.Key.Item1 == patch.Kind).SelectMany(p => p.Value).Distinct().ToList();
            else if (!providers.TryGetValue((patch.Kind, patch.Id), out owners))
                owners = new List<string>();

            owners = owners.Where(o => o != patch.ModId).ToList();
            if (owners.Count == 0)
            {
                result.Findings.Add(new AtlasFinding
                {
                    Kind = AtlasFindingKind.PatchTargetMissing, Severity = AtlasSeverity.Warning, ModId = patch.ModId,
                    ResourceKind = patch.Kind, ResourceId = patch.Id, File = patch.File,
                    Message = "патч не находит цель ни в одном другом загруженном моде",
                });
                return;
            }

            var closure = result.Closures[patch.ModId];
            var undeclared = owners.Where(o => !closure.Contains(o)).ToList();
            result.Findings.Add(new AtlasFinding
            {
                Kind = undeclared.Count == 0 ? AtlasFindingKind.DeclaredPatch : AtlasFindingKind.UndeclaredPatch,
                Severity = undeclared.Count == 0 ? AtlasSeverity.Info : AtlasSeverity.Error,
                ModId = patch.ModId,
                OtherMods = owners,
                ResourceKind = patch.Kind, ResourceId = patch.Id, File = patch.File,
                Message = undeclared.Count == 0
                    ? $"патчит ресурс мода {string.Join("/", owners)}"
                    : $"патчит ресурс мода {string.Join("/", undeclared)} без зависимости от него — порядок применения патча не гарантирован",
            });
        }

        private static void Provide(ResourceAtlasResult result, string kind, string id, string modId, string file)
            => result.Provisions.Add(new AtlasProvision { Kind = kind, Id = id, ModId = modId, File = file });

        private static HashSet<string> Closure(string modId, Dictionary<string, List<string>> depends)
        {
            var visible = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(depends.TryGetValue(modId, out var direct) ? direct : new List<string>());
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (visible.Add(id) && depends.TryGetValue(id, out var next))
                {
                    foreach (var dependency in next)
                        pending.Push(dependency);
                }
            }

            return visible;
        }

        private static string StripExtension(string path)
        {
            var dot = path.LastIndexOf('.');
            var slash = path.LastIndexOf('/');
            return dot > slash ? path.Substring(0, dot) : path;
        }

        /// <summary>
        /// Blanks C# comments while keeping line breaks (line numbers stay
        /// valid) and string/char literals intact — a naive <c>//</c> cut would
        /// truncate <c>"res://..."</c>.
        /// </summary>
        private static string StripCSharpComments(string text)
        {
            var chars = text.ToCharArray();
            var i = 0;
            while (i < chars.Length)
            {
                var c = chars[i];
                var next = i + 1 < chars.Length ? chars[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < chars.Length && chars[i] != '\n')
                        chars[i++] = ' ';
                }
                else if (c == '/' && next == '*')
                {
                    while (i < chars.Length && !(chars[i] == '*' && i + 1 < chars.Length && chars[i + 1] == '/'))
                    {
                        if (chars[i] != '\n')
                            chars[i] = ' ';
                        i++;
                    }
                    if (i < chars.Length)
                    {
                        chars[i++] = ' ';
                        chars[i++] = ' ';
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    var verbatim = c == '"' && i > 0 && (chars[i - 1] == '@' || (chars[i - 1] == '$' && i > 1 && chars[i - 2] == '@'));
                    i++;
                    while (i < chars.Length && chars[i] != c && chars[i] != '\n')
                    {
                        if (!verbatim && chars[i] == '\\')
                            i++;
                        else if (verbatim && chars[i] == '"' && i + 1 < chars.Length && chars[i + 1] == '"')
                            i++;
                        i++;
                    }
                    i++;
                }
                else
                {
                    i++;
                }
            }

            return new string(chars);
        }

        private static int LineOf(string text, int index)
        {
            var line = 1;
            for (var i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }
    }
}
