using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// Applies scene patches to a parsed target scene. A patch is an inherited scene
    /// (see <see cref="ScenePatch"/>): nodes without <c>type</c>/<c>instance</c> override
    /// existing nodes by full path, nodes with a type are additions, and the reserved
    /// <c>metadata/_patch_*</c> keys (see <see cref="PatchMetadata"/>) express the deletions
    /// and bulk edits Godot's inheritance format cannot.
    /// <para/>
    /// <b>Purity and reuse:</b> the applier never mutates the cached patch parse — each layer is
    /// deep-cloned first, so the same patch can be applied to many scene loads and to many
    /// different base scenes. The target <see cref="ParsedScene"/> is mutated in place and is
    /// expected to already contain the results of previous layers, which is what makes the mod
    /// load order a cascade.
    /// <para/>
    /// <b>Layer order:</b> reserved-key extraction → resource merge/remap → deletions with
    /// cascade → overrides → additions → index re-sort → connections → resource removals.
    /// Resource garbage collection runs once after every layer, only when some layer requested
    /// it, so a resource that a later layer re-references is not collected.
    /// </summary>
    public class ScenePatchApplier
    {
        /// <summary>Category used for every diagnostic this class emits.</summary>
        private const string LogCategory = "TSCN";

        /// <summary>
        /// Applies all layers to <paramref name="target"/> in order and runs final resource GC
        /// when at least one layer requested it. Returns false only for hard failures (invalid
        /// patch root, attempt to delete the root); soft authoring errors are logged and skipped.
        /// </summary>
        public bool ApplyAll(ParsedScene target, IReadOnlyList<ScenePatch> patches)
        {
            if (target == null)
                return false;
            if (patches == null || patches.Count == 0)
                return true;

            var ok = true;
            var gcRequested = false;

            foreach (var patch in patches)
            {
                var layer = CloneScene(patch.Scene);
                if (!ApplyLayer(target, layer, patch, out var layerGc))
                    ok = false;
                gcRequested |= layerGc;
            }

            if (gcRequested)
                CollectGarbage(target);

            return ok;
        }

        private bool ApplyLayer(ParsedScene target, ParsedScene layer, ScenePatch patch, out bool gcRequested)
        {
            gcRequested = false;

            var root = layer.Nodes.FirstOrDefault(n => n.Parent == null);
            if (root == null)
            {
                GameLogger.Log(LogCategory, $"ScenePatchApplier: patch '{patch.SourcePath}' has no root node", LogLevel.Error);
                return false;
            }

            var nodeOps = new Dictionary<ParsedNode, NodePatchOperations>();
            foreach (var node in layer.Nodes)
                ExtractOperations(node, nodeOps, ref gcRequested);

            var subDeletes = new List<SubResourceDef>();
            foreach (var sub in layer.SubResources)
            {
                if (TakeBool(sub, PatchMetadata.Delete))
                    subDeletes.Add(sub);
            }

            if (nodeOps.TryGetValue(root, out var rootOps) && rootOps.Delete)
            {
                GameLogger.Log(LogCategory, $"ScenePatchApplier: patch '{patch.SourcePath}' tries to delete the scene root — layer rejected", LogLevel.Error);
                return false;
            }

            var extMap = MergeExtResources(target, layer, patch.TargetExtResourceId);
            var subMap = MergeSubResources(target, layer, subDeletes);
            RemapLayerReferences(layer, extMap, subMap);

            var exactDeletes = nodeOps.Where(kv => kv.Value.Delete).Select(kv => NodePathOf(kv.Key)).ToList();
            var maskDeletes = rootOps?.DeleteNodes ?? new List<string>();
            var deletedPaths = CollectDeletions(target, exactDeletes, maskDeletes, patch.SourcePath);
            RemoveDeletedNodes(target, deletedPaths);
            CleanupNodePathReferences(target, deletedPaths);

            var targetMap = BuildPathMap(target);
            foreach (var node in layer.Nodes)
            {
                if (ReferenceEquals(node, root))
                    continue;

                nodeOps.TryGetValue(node, out var ops);
                ops ??= new NodePatchOperations();

                if (ops.Delete)
                    continue;

                var path = NodePathOf(node);
                var isAddition = !string.IsNullOrEmpty(node.Type) || !string.IsNullOrEmpty(node.InstanceRef);

                if (isAddition)
                {
                    if (string.IsNullOrEmpty(node.Parent))
                    {
                        GameLogger.Log(LogCategory, $"ScenePatchApplier: '{patch.SourcePath}' adds second root '{path}' — skipped", LogLevel.Warning);
                        continue;
                    }

                    if (targetMap.ContainsKey(path))
                    {
                        GameLogger.Log(LogCategory, $"ScenePatchApplier: '{patch.SourcePath}' adds '{path}' but it already exists — skipped", LogLevel.Warning);
                        continue;
                    }

                    target.Nodes.Add(node);
                    targetMap[path] = node;
                    continue;
                }

                if (MaskMatcher.HasMask(path))
                {
                    GameLogger.Log(LogCategory, $"ScenePatchApplier: node selector '{path}' uses a mask — unsupported for overrides, skipped", LogLevel.Warning);
                    continue;
                }

                if (!targetMap.TryGetValue(path, out var targetNode))
                {
                    GameLogger.Log(LogCategory, $"ScenePatchApplier: '{patch.SourcePath}' overrides unknown node '{path}' — skipped", LogLevel.Warning);
                    continue;
                }

                ApplyOverride(targetNode, node, ops);
            }

            var targetRoot = target.Nodes.FirstOrDefault(n => n.Parent == null);
            if (targetRoot != null)
                ApplyOverride(targetRoot, root, rootOps ?? new NodePatchOperations());

            ReorderByIndex(target);

            ApplyConnections(target, rootOps, layer, patch.SourcePath);
            RemoveResources(target, rootOps, subDeletes);

            if (!string.IsNullOrEmpty(layer.EntryPointClass))
                target.EntryPointClass = layer.EntryPointClass;

            return true;
        }

        private static void ExtractOperations(ParsedNode node, Dictionary<ParsedNode, NodePatchOperations> nodeOps, ref bool gcRequested)
        {
            var ops = new NodePatchOperations
            {
                Delete = TakeBool(node, PatchMetadata.Delete),
                Remove = NormalizeStringList(TakeProperty(node, PatchMetadata.Remove)),
                AddGroups = NormalizeStringList(TakeProperty(node, PatchMetadata.AddGroups)),
                RemoveGroups = NormalizeStringList(TakeProperty(node, PatchMetadata.RemoveGroups)),
                DeleteNodes = NormalizeStringList(TakeProperty(node, PatchMetadata.DeleteNodes)),
                RemoveConnections = NormalizeStringList(TakeProperty(node, PatchMetadata.RemoveConnections)),
                RemoveExtResources = NormalizeStringList(TakeProperty(node, PatchMetadata.RemoveExtResources)),
            };

            if (TakeBool(node, PatchMetadata.Gc))
                gcRequested = true;

            nodeOps[node] = ops;
        }

        private static void ApplyOverride(ParsedNode target, ParsedNode source, NodePatchOperations ops)
        {
            RemoveProperties(target, ops.Remove);
            MergeProperties(target, source);

            if (source.Groups != null)
                target.Groups = (string[])source.Groups.Clone();

            ApplyGroupOperations(target, ops.AddGroups, ops.RemoveGroups);

            if (!string.IsNullOrEmpty(source.ScriptRef))
                target.ScriptRef = source.ScriptRef;

            if (source.Index.HasValue)
                target.Index = source.Index;

            MergeNodePathVars(target, source);
        }

        private static void MergeProperties(ParsedNode target, ParsedNode source)
        {
            foreach (var kvp in source.OrderedProperties)
            {
                if (!target.Properties.ContainsKey(kvp.Key))
                    target.PropertyOrder.Add(kvp.Key);
                target.Properties[kvp.Key] = kvp.Value;
            }
        }

        private static void MergeNodePathVars(ParsedNode target, ParsedNode source)
        {
            if (source.NodePathVars == null || source.NodePathVars.Count == 0)
                return;

            target.NodePathVars ??= new Dictionary<string, string>();
            foreach (var kvp in source.NodePathVars)
                target.NodePathVars[kvp.Key] = kvp.Value;
        }

        private static void RemoveProperties(ParsedNode node, List<string> masks)
        {
            if (masks == null || masks.Count == 0)
                return;

            foreach (var key in new List<string>(node.PropertyOrder))
            {
                if (!MatchesAny(masks, key))
                    continue;
                node.Properties.Remove(key);
                node.PropertyOrder.Remove(key);
            }
        }

        private static void ApplyGroupOperations(ParsedNode target, List<string> add, List<string> remove)
        {
            if (add != null)
            {
                var groups = new List<string>(target.Groups ?? Array.Empty<string>());
                foreach (var group in add)
                {
                    if (!groups.Contains(group))
                        groups.Add(group);
                }
                target.Groups = groups.ToArray();
            }

            if (remove != null && target.Groups != null)
                target.Groups = target.Groups.Where(g => !remove.Contains(g)).ToArray();
        }

        /// <summary>
        /// Resolves exact and masked node deletes against the target and expands them into the
        /// full set of removed paths (descendants cascade). Masks are relative to the root and
        /// never match the root itself; exact paths that do not exist are authoring warnings.
        /// The root is skipped by construction because its path is empty.
        /// </summary>
        private static HashSet<string> CollectDeletions(ParsedScene target, List<string> exactPaths, List<string> masks, string sourcePath)
        {
            var deleted = new HashSet<string>(StringComparer.Ordinal);
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var map = BuildPathMap(target);

            foreach (var path in exactPaths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                if (MaskMatcher.HasMask(path))
                    SelectMask(map.Keys, path, selected);
                else if (map.ContainsKey(path))
                    selected.Add(path);
                else
                    GameLogger.Log(LogCategory, $"ScenePatchApplier: '{sourcePath}' deletes unknown node '{path}'", LogLevel.Warning);
            }

            foreach (var mask in masks)
            {
                if (string.IsNullOrEmpty(mask))
                    continue;
                SelectMask(map.Keys, mask, selected);
            }

            foreach (var key in map.Keys)
            {
                foreach (var selectedPath in selected)
                {
                    if (key.Equals(selectedPath, StringComparison.Ordinal)
                        || key.StartsWith(selectedPath + "/", StringComparison.Ordinal))
                    {
                        deleted.Add(key);
                        break;
                    }
                }
            }

            return deleted;
        }

        private static void SelectMask(IEnumerable<string> keys, string mask, HashSet<string> selected)
        {
            foreach (var key in keys)
            {
                if (!MaskMatcher.IsMatch(mask, key))
                    continue;
                selected.Add(key);
                GameLogger.Log(LogCategory, $"ScenePatchApplier: mask '{mask}' matched '{key}'", LogLevel.Debug);
            }
        }

        private static void RemoveDeletedNodes(ParsedScene target, HashSet<string> deletedPaths)
        {
            if (deletedPaths.Count == 0)
                return;

            target.Nodes.RemoveAll(n => deletedPaths.Contains(NodePathOf(n)));

            target.Connections.RemoveAll(c =>
                deletedPaths.Contains(c.From ?? "") || deletedPaths.Contains(c.To ?? ""));
        }

        /// <summary>
        /// Clears NodePath-valued properties and <c>node_paths</c> bindings on surviving nodes
        /// when they resolve into the deleted subtree, otherwise the builder would log dangling
        /// references for paths the patch intentionally removed. Absolute paths are left alone
        /// because they may point outside the scene.
        /// </summary>
        private static void CleanupNodePathReferences(ParsedScene target, HashSet<string> deletedPaths)
        {
            if (deletedPaths.Count == 0)
                return;

            foreach (var node in target.Nodes)
            {
                var ownerPath = NodePathOf(node);

                if (node.NodePathVars != null && node.NodePathVars.Count > 0)
                {
                    foreach (var key in new List<string>(node.NodePathVars.Keys))
                    {
                        var resolved = ResolveNodePath(ownerPath, ExtractNodePathValue(node.NodePathVars[key], true));
                        if (resolved != null && deletedPaths.Contains(resolved))
                            node.NodePathVars.Remove(key);
                    }
                }

                foreach (var key in new List<string>(node.PropertyOrder))
                {
                    if (!node.Properties.TryGetValue(key, out var value))
                        continue;

                    var resolved = ResolveNodePath(ownerPath, ExtractNodePathValue(value, false));
                    if (resolved == null || !deletedPaths.Contains(resolved))
                        continue;

                    node.Properties.Remove(key);
                    node.PropertyOrder.Remove(key);
                }
            }
        }

        /// <summary>
        /// Extracts the path text from a NodePath-typed value. Only <c>NodePath(...)</c> wrappers
        /// are recognised in properties (<paramref name="allowRawString"/> false) so ordinary
        /// strings that happen to look like paths are never treated as bindings; <c>node_paths</c>
        /// values are stored raw, so there a plain string or quoted string is accepted too.
        /// </summary>
        private static string ExtractNodePathValue(object value, bool allowRawString)
        {
            if (value is ParsedGodotValue pgv && pgv.TypeName == "NodePath")
                return pgv.Arg(0) as string;

            if (!allowRawString || !(value is string raw) || raw.Length == 0)
                return null;

            var parsed = TscnPropertyParser.Parse(raw);
            switch (parsed)
            {
                case ParsedGodotValue parsedPgv when parsedPgv.TypeName == "NodePath":
                    return parsedPgv.Arg(0) as string;
                case string text:
                    return text;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Resolves a NodePath written relative to <paramref name="ownerPath"/> into a canonical
        /// scene path. Returns null for absolute paths (<c>/root/...</c>, <c>res://...</c>).
        /// </summary>
        private static string ResolveNodePath(string ownerPath, string nodePath)
        {
            if (string.IsNullOrEmpty(nodePath))
                return null;

            if (nodePath.StartsWith("/", StringComparison.Ordinal)
                || nodePath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return null;

            var parts = ownerPath.Length == 0
                ? new List<string>()
                : new List<string>(ownerPath.Split('/'));

            foreach (var segment in nodePath.Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                    continue;
                if (segment == "..")
                {
                    if (parts.Count > 0)
                        parts.RemoveAt(parts.Count - 1);
                    continue;
                }
                parts.Add(segment);
            }

            return string.Join("/", parts);
        }

        /// <summary>
        /// Applies Godot's <c>index</c> ordering to the flat node list. Only runs when at least
        /// one node carries an index. Within each sibling group an indexed node is inserted at
        /// its position (shifting the rest), nodes without an index keep their relative order,
        /// and groups are emitted parents-before-children so the builder can resolve them.
        /// </summary>
        private static void ReorderByIndex(ParsedScene target)
        {
            if (!target.Nodes.Any(n => n.Index.HasValue))
                return;

            var root = target.Nodes.FirstOrDefault(n => n.Parent == null);
            var groups = new Dictionary<string, List<ParsedNode>>(StringComparer.Ordinal);
            var groupOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            var nextGroup = 0;

            foreach (var node in target.Nodes)
            {
                if (ReferenceEquals(node, root))
                    continue;

                var parent = ParentPathOf(node);
                if (!groups.TryGetValue(parent, out var list))
                {
                    list = new List<ParsedNode>();
                    groups[parent] = list;
                    groupOrder[parent] = nextGroup++;
                }
                list.Add(node);
            }

            var result = new List<ParsedNode>(target.Nodes.Count);
            if (root != null)
                result.Add(root);

            foreach (var key in groups.Keys
                .OrderBy(DepthOf)
                .ThenBy(k => groupOrder[k]))
            {
                result.AddRange(ApplyIndexOrder(groups[key]));
            }

            target.Nodes = result;
        }

        private static List<ParsedNode> ApplyIndexOrder(List<ParsedNode> nodes)
        {
            var provisional = new List<ParsedNode>(nodes.Count);
            foreach (var node in nodes)
            {
                if (!node.Index.HasValue)
                    provisional.Add(node);
            }

            foreach (var node in nodes.Where(n => n.Index.HasValue).OrderBy(n => n.Index.Value))
                provisional.Insert(Math.Clamp(node.Index.Value, 0, provisional.Count), node);

            return provisional;
        }

        private static int DepthOf(string parentPath)
        {
            return parentPath.Length == 0 ? 0 : parentPath.Split('/').Length;
        }

        private static void ApplyConnections(ParsedScene target, NodePatchOperations rootOps, ParsedScene layer, string sourcePath)
        {
            if (rootOps?.RemoveConnections != null)
            {
                foreach (var raw in rootOps.RemoveConnections)
                {
                    if (!ConnectionSelector.TryParse(raw, out var selector, out var error))
                    {
                        GameLogger.Log(LogCategory, $"ScenePatchApplier: '{sourcePath}' invalid connection selector '{raw}': {error}", LogLevel.Error);
                        continue;
                    }

                    var removed = target.Connections.RemoveAll(c => selector.Matches(c));
                    GameLogger.Log(LogCategory, $"ScenePatchApplier: '{sourcePath}' selector '{raw}' removed {removed} connection(s)", LogLevel.Debug);
                }
            }

            foreach (var connection in layer.Connections)
            {
                var duplicate = target.Connections.Any(c =>
                    string.Equals(c.Signal, connection.Signal, StringComparison.Ordinal)
                    && string.Equals(c.From, connection.From, StringComparison.Ordinal)
                    && string.Equals(c.To, connection.To, StringComparison.Ordinal)
                    && string.Equals(c.Method, connection.Method, StringComparison.Ordinal)
                    && c.Flags == connection.Flags);

                if (!duplicate)
                    target.Connections.Add(connection);
            }
        }

        private static void RemoveResources(ParsedScene target, NodePatchOperations rootOps, List<SubResourceDef> subDeletes)
        {
            var references = CollectReferences(target);

            if (rootOps?.RemoveExtResources != null)
            {
                foreach (var id in rootOps.RemoveExtResources)
                {
                    if (string.IsNullOrEmpty(id) || references.Ext.Contains(id))
                        continue;
                    var removed = target.ExtResources.RemoveAll(e => e?.Id == id);
                    if (removed > 0)
                        GameLogger.Log(LogCategory, $"ScenePatchApplier: removed unreferenced ext_resource '{id}'");
                }
            }

            foreach (var sub in subDeletes)
            {
                if (sub?.Id == null || references.Sub.Contains(sub.Id))
                    continue;
                var removed = target.SubResources.RemoveAll(s => s.Id == sub.Id);
                if (removed > 0)
                    GameLogger.Log(LogCategory, $"ScenePatchApplier: removed unreferenced sub_resource '{sub.Id}'");
            }
        }

        /// <summary>
        /// Final-pass garbage collection: removes every ext/sub_resource that no surviving node
        /// references. Runs once after all layers so a resource that only a later layer
        /// re-references survives; each removal is logged for patch authors.
        /// </summary>
        private static void CollectGarbage(ParsedScene target)
        {
            var references = CollectReferences(target);

            var removedExt = target.ExtResources.RemoveAll(e => e?.Id == null || !references.Ext.Contains(e.Id));
            var removedSub = target.SubResources.RemoveAll(s => s.Id == null || !references.Sub.Contains(s.Id));

            if (removedExt > 0 || removedSub > 0)
                GameLogger.Log(LogCategory, $"ScenePatchApplier: GC removed {removedExt} ext_resource(s), {removedSub} sub_resource(s)");
        }

        private static (HashSet<string> Ext, HashSet<string> Sub) CollectReferences(ParsedScene target)
        {
            var ext = new HashSet<string>(StringComparer.Ordinal);
            var sub = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in target.Nodes)
            {
                CollectRefString(node.ScriptRef, ext);
                CollectRefString(node.InstanceRef, ext);
                foreach (var value in node.Properties.Values)
                    CollectRefValue(value, ext, sub);
            }

            foreach (var def in target.SubResources)
            {
                foreach (var value in def.Properties.Values)
                    CollectRefValue(value, ext, sub);
            }

            return (ext, sub);
        }

        private static void CollectRefValue(object value, HashSet<string> ext, HashSet<string> sub)
        {
            switch (value)
            {
                case TscnPropertyValue pv when pv.IsExtResourceRef:
                    if (!string.IsNullOrEmpty(pv.RefId)) ext.Add(pv.RefId);
                    return;
                case TscnPropertyValue pv when pv.IsSubResourceRef:
                    if (!string.IsNullOrEmpty(pv.RefId)) sub.Add(pv.RefId);
                    return;
                case string s:
                    CollectRefString(s, ext);
                    return;
                case string[] strings:
                    foreach (var item in strings) CollectRefString(item, ext);
                    return;
                case ParsedGodotValue[] godotValues:
                    foreach (var pgv in godotValues)
                        foreach (var arg in pgv.Args)
                            CollectRefString(arg as string, ext);
                    return;
            }
        }

        private static void CollectRefString(string raw, HashSet<string> ext)
        {
            if (string.IsNullOrEmpty(raw))
                return;

            const string prefix = "ExtResource(";
            var index = 0;
            while (true)
            {
                var start = raw.IndexOf(prefix, index, StringComparison.Ordinal);
                if (start < 0)
                    return;
                start += prefix.Length;
                var end = raw.IndexOf(')', start);
                if (end < 0)
                    return;
                var id = raw.Substring(start, end - start).Trim().Trim('"');
                if (id.Length > 0)
                    ext.Add(id);
                index = end;
            }
        }

        private static Dictionary<string, string> MergeExtResources(ParsedScene target, ParsedScene layer, string targetExtResourceId)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var ext in layer.ExtResources)
            {
                if (ext?.Id == null || ext.Id == targetExtResourceId)
                    continue;

                var same = target.ExtResources.FirstOrDefault(e =>
                    e != null
                    && string.Equals(e.Type, ext.Type, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ScenePatchTargetResolver.Normalize(e.Path), ScenePatchTargetResolver.Normalize(ext.Path), StringComparison.OrdinalIgnoreCase));

                if (same != null)
                {
                    map[ext.Id] = same.Id;
                    continue;
                }

                if (target.ExtResources.All(e => e?.Id != ext.Id))
                {
                    target.ExtResources.Add(CloneExtResource(ext, ext.Id));
                    map[ext.Id] = ext.Id;
                    continue;
                }

                var newId = MakeUniqueId(ext.Id, id => target.ExtResources.Any(e => e?.Id == id));
                target.ExtResources.Add(CloneExtResource(ext, newId));
                map[ext.Id] = newId;
            }

            return map;
        }

        private static Dictionary<string, string> MergeSubResources(ParsedScene target, ParsedScene layer, List<SubResourceDef> subDeletes)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var sub in layer.SubResources)
            {
                if (sub?.Id == null || subDeletes.Contains(sub))
                    continue;

                if (target.SubResources.All(s => s.Id != sub.Id))
                {
                    target.SubResources.Add(CloneSubResource(sub, sub.Id));
                    map[sub.Id] = sub.Id;
                    continue;
                }

                var newId = MakeUniqueId(sub.Id, id => target.SubResources.Any(s => s.Id == id));
                target.SubResources.Add(CloneSubResource(sub, newId));
                map[sub.Id] = newId;
            }

            return map;
        }

        private static string MakeUniqueId(string baseId, Func<string, bool> taken)
        {
            for (var i = 1; ; i++)
            {
                var candidate = $"{baseId}_p{i}";
                if (!taken(candidate))
                    return candidate;
            }
        }

        private static void RemapLayerReferences(ParsedScene layer, Dictionary<string, string> extMap, Dictionary<string, string> subMap)
        {
            foreach (var node in layer.Nodes)
            {
                node.ScriptRef = RemapRefString(node.ScriptRef, "ExtResource", extMap);
                node.InstanceRef = RemapRefString(node.InstanceRef, "ExtResource", extMap);

                foreach (var key in new List<string>(node.PropertyOrder))
                {
                    if (node.Properties.TryGetValue(key, out var value))
                        node.Properties[key] = RemapValue(value, extMap, subMap);
                }
            }

            foreach (var sub in layer.SubResources)
            {
                foreach (var key in new List<string>(sub.PropertyOrder))
                {
                    if (sub.Properties.TryGetValue(key, out var value))
                        sub.Properties[key] = RemapValue(value, extMap, subMap);
                }
            }
        }

        private static object RemapValue(object value, Dictionary<string, string> extMap, Dictionary<string, string> subMap)
        {
            switch (value)
            {
                case TscnPropertyValue pv when pv.IsExtResourceRef:
                    return extMap.TryGetValue(pv.RefId, out var extId)
                        ? TscnPropertyValue.FromExtResource(extId)
                        : pv;
                case TscnPropertyValue pv when pv.IsSubResourceRef:
                    return subMap.TryGetValue(pv.RefId, out var subId)
                        ? TscnPropertyValue.FromSubResource(subId)
                        : pv;
                case string s:
                    var remapped = RemapRefString(s, "ExtResource", extMap);
                    return RemapRefString(remapped, "SubResource", subMap);
                default:
                    return value;
            }
        }

        private static string RemapRefString(string raw, string kind, Dictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(raw) || map.Count == 0)
                return raw;

            var prefix = kind + "(";
            var start = raw.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
                return raw;

            start += prefix.Length;
            var end = raw.IndexOf(')', start);
            if (end < 0)
                return raw;

            var id = raw.Substring(start, end - start).Trim().Trim('"');
            if (!map.TryGetValue(id, out var mapped) || string.Equals(mapped, id, StringComparison.Ordinal))
                return raw;

            return raw.Substring(0, start) + "\"" + mapped + "\"" + raw.Substring(end);
        }

        private static ParsedScene CloneScene(ParsedScene scene)
        {
            var clone = new ParsedScene
            {
                Format = scene.Format,
                EntryPointClass = scene.EntryPointClass,
                Nodes = new List<ParsedNode>(scene.Nodes.Count),
                ExtResources = new List<ExtResourceRef>(scene.ExtResources.Count),
                SubResources = new List<SubResourceDef>(scene.SubResources.Count),
                Connections = new List<ParsedConnection>(scene.Connections.Count),
            };

            foreach (var node in scene.Nodes)
            {
                clone.Nodes.Add(new ParsedNode
                {
                    Name = node.Name,
                    Type = node.Type,
                    Parent = node.Parent,
                    InstanceRef = node.InstanceRef,
                    ScriptRef = node.ScriptRef,
                    Index = node.Index,
                    Groups = node.Groups == null ? null : (string[])node.Groups.Clone(),
                    NodePathVars = node.NodePathVars == null ? new Dictionary<string, string>() : new Dictionary<string, string>(node.NodePathVars),
                    Properties = CloneProperties(node.Properties),
                    PropertyOrder = new List<string>(node.PropertyOrder),
                });
            }

            foreach (var ext in scene.ExtResources)
                clone.ExtResources.Add(new ExtResourceRef { Type = ext.Type, Path = ext.Path, Id = ext.Id });

            foreach (var sub in scene.SubResources)
            {
                clone.SubResources.Add(new SubResourceDef
                {
                    Type = sub.Type,
                    Id = sub.Id,
                    Properties = CloneProperties(sub.Properties),
                    PropertyOrder = new List<string>(sub.PropertyOrder),
                });
            }

            foreach (var connection in scene.Connections)
            {
                clone.Connections.Add(new ParsedConnection
                {
                    Signal = connection.Signal,
                    From = connection.From,
                    To = connection.To,
                    Method = connection.Method,
                    Flags = connection.Flags,
                });
            }

            return clone;
        }

        private static ExtResourceRef CloneExtResource(ExtResourceRef ext, string id)
        {
            return new ExtResourceRef { Type = ext.Type, Path = ext.Path, Id = id };
        }

        private static SubResourceDef CloneSubResource(SubResourceDef sub, string id)
        {
            return new SubResourceDef
            {
                Type = sub.Type,
                Id = id,
                Properties = CloneProperties(sub.Properties),
                PropertyOrder = new List<string>(sub.PropertyOrder),
            };
        }

        private static Dictionary<string, object> CloneProperties(Dictionary<string, object> properties)
        {
            var clone = new Dictionary<string, object>(properties.Count);
            foreach (var kvp in properties)
                clone[kvp.Key] = CloneValue(kvp.Value);
            return clone;
        }

        private static object CloneValue(object value)
        {
            switch (value)
            {
                case TscnPropertyValue pv:
                    return new TscnPropertyValue { Type = pv.Type, Variant = pv.Variant, RefId = pv.RefId };
                case ParsedGodotValue pgv:
                    return new ParsedGodotValue { TypeName = pgv.TypeName, Args = new List<object>(pgv.Args) };
                case string[] strings:
                    return (string[])strings.Clone();
                case int[] ints:
                    return (int[])ints.Clone();
                case long[] longs:
                    return (long[])longs.Clone();
                case float[] floats:
                    return (float[])floats.Clone();
                case double[] doubles:
                    return (double[])doubles.Clone();
                case byte[] bytes:
                    return (byte[])bytes.Clone();
                case ParsedGodotValue[] godotValues:
                    var copy = new ParsedGodotValue[godotValues.Length];
                    for (var i = 0; i < godotValues.Length; i++)
                    {
                        var pgv = godotValues[i];
                        copy[i] = new ParsedGodotValue { TypeName = pgv.TypeName, Args = new List<object>(pgv.Args) };
                    }
                    return copy;
                default:
                    return value;
            }
        }

        private static Dictionary<string, ParsedNode> BuildPathMap(ParsedScene scene)
        {
            var map = new Dictionary<string, ParsedNode>(StringComparer.Ordinal);
            foreach (var node in scene.Nodes)
            {
                var path = NodePathOf(node);
                if (path.Length == 0)
                    continue;
                if (!map.ContainsKey(path))
                    map[path] = node;
            }
            return map;
        }

        /// <summary>Full path of a node relative to the scene root; the root itself yields an empty string.</summary>
        private static string NodePathOf(ParsedNode node)
        {
            if (node == null)
                return "";
            if (string.IsNullOrEmpty(node.Parent))
                return "";
            if (node.Parent == "." || node.Parent == "..")
                return node.Name ?? "";
            return node.Parent.TrimEnd('/') + "/" + node.Name;
        }

        private static string ParentPathOf(ParsedNode node)
        {
            var parent = node.Parent;
            if (string.IsNullOrEmpty(parent) || parent == "." || parent == "..")
                return "";
            return parent.TrimEnd('/');
        }

        private static bool MatchesAny(List<string> masks, string value)
        {
            foreach (var mask in masks)
            {
                if (MaskMatcher.IsMatch(mask, value))
                    return true;
            }
            return false;
        }

        private static bool TakeBool(ParsedNode node, string key)
        {
            return TakeProperty(node, key) is bool value && value;
        }

        private static List<string> NormalizeStringList(object value)
        {
            switch (value)
            {
                case string[] strings:
                    return strings.Where(s => !string.IsNullOrEmpty(s)).ToList();
                case string text:
                    var trimmed = text.Trim();
                    if (trimmed.Length == 0)
                        return null;

                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                    {
                        var result = new List<string>();
                        foreach (var part in TscnPropertyParser.SplitArgs(trimmed.Substring(1, trimmed.Length - 2)))
                        {
                            var item = part.Trim();
                            if (item.Length >= 2 && item.StartsWith("\"", StringComparison.Ordinal) && item.EndsWith("\"", StringComparison.Ordinal))
                                item = item.Substring(1, item.Length - 2);
                            if (item.Length > 0)
                                result.Add(item);
                        }
                        return result;
                    }

                    return new List<string> { text };
                default:
                    return null;
            }
        }

        private static object TakeProperty(ParsedNode node, string key)
        {
            if (node?.Properties == null || !node.Properties.TryGetValue(key, out var value))
                return null;

            node.Properties.Remove(key);
            node.PropertyOrder.Remove(key);
            return value;
        }

        private static bool TakeBool(SubResourceDef sub, string key)
        {
            if (sub?.Properties == null || !sub.Properties.TryGetValue(key, out var value))
                return false;

            sub.Properties.Remove(key);
            sub.PropertyOrder.Remove(key);
            return value is bool flag && flag;
        }
    }
}
