using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>
    /// Converts a <see cref="ParsedScene"/> into a live Godot Node tree.
    /// Build order: sub-resources → nodes (recursive PackedScene instantiation) →
    /// properties (via <see cref="PropertyResolverRegistry"/>) → node-path vars →
    /// signals → entry-point. Entry-point type is resolved across ScriptTypeRegistry,
    /// Type.GetType(), and all AppDomain assemblies as fallback.
    /// </summary>
    public class SceneBuilder
    {
        private readonly SubResourceFactory _subResourceFactory = new();
        private readonly Dictionary<string, Node> _builtNodes = new();
        private readonly List<(Node Node, string Key, NodePath Path)> _nodePathAssignments = new();
        private Node _rootNode;

        /// <summary>
        /// Builds a live Node tree from a parsed scene.
        /// <paramref name="instanceLoader"/> is called for PackedScene references (both node instances and exported properties).
        /// <paramref name="extResourceResolver"/> is a fallback for non-Script/non-PackedScene resource types.
        /// Returns the root Node, or null on failure.
        /// </summary>
        public Node Build(ParsedScene scene, Func<string, Node> instanceLoader, Func<string, Resource> extResourceResolver)
        {
            _builtNodes.Clear();
            _nodePathAssignments.Clear();
            _rootNode = null;

            _subResourceFactory.BuildAll(scene.SubResources, id => _subResourceFactory.Get(id));

            foreach (var parsedNode in scene.Nodes)
            {
                Node node = CreateNode(parsedNode, scene, instanceLoader, extResourceResolver);
                if (node == null) continue;

                node.Name = parsedNode.Name;
                if (parsedNode.Groups != null)
                    foreach (var group in parsedNode.Groups)
                        node.AddToGroup(group, false);

                ApplyProperties(node, parsedNode, scene, extResourceResolver, instanceLoader);
                ApplyNodePathVars(node, parsedNode.NodePathVars);

                var parent = ResolveParent(parsedNode);
                if (parent != null) parent.AddChild(node, false);
                else if (_rootNode == null) _rootNode = node;

                _builtNodes[parsedNode.Name] = node;
            }

            ResolveNodePathAssignments();

            foreach (var conn in scene.Connections)
                ApplyConnection(conn);

            if (!string.IsNullOrEmpty(scene.EntryPointClass))
            {
                var entryType = ScriptTypeRegistry.ResolveByName(scene.EntryPointClass)
                    ?? Type.GetType(scene.EntryPointClass)
                    ?? Reflections.LoadedAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.FullName == scene.EntryPointClass || t.Name == scene.EntryPointClass);
                if (entryType != null && typeof(ITscnSceneEntryPoint).IsAssignableFrom(entryType))
                    (Activator.CreateInstance(entryType) as ITscnSceneEntryPoint)?.OnSceneBuilt(_rootNode);
            }

            return _rootNode;
        }

        private Node CreateNode(ParsedNode parsedNode, ParsedScene scene, Func<string, Node> instanceLoader, Func<string, Resource> extResourceResolver)
        {
            if (!string.IsNullOrEmpty(parsedNode.InstanceRef))
            {
                var refId = ExtractResourceId(parsedNode.InstanceRef);
                var extRes = scene.ExtResources.FirstOrDefault(r => r.Id == refId);
                if (extRes != null && instanceLoader != null)
                {
                    var instanceNode = instanceLoader(extRes.Path);
                    if (instanceNode != null) { instanceNode.Name = parsedNode.Name; return instanceNode; }
                }
                var typeName = parsedNode.Type ?? "Node";
                return ClassDB.Instantiate(typeName).Obj as Node;
            }

            if (!string.IsNullOrEmpty(parsedNode.ScriptRef))
            {
                var refId = ExtractResourceId(parsedNode.ScriptRef);
                var extRes = scene.ExtResources.FirstOrDefault(r => r.Id == refId);
                if (extRes != null)
                {
                    var scriptType = ScriptTypeRegistry.Resolve(extRes.Path);
                    if (scriptType != null && typeof(Node).IsAssignableFrom(scriptType))
                        return (Node)Activator.CreateInstance(scriptType);
                }
            }

            var fallbackType = parsedNode.Type ?? "Node";
            return ClassDB.Instantiate(fallbackType).Obj as Node;
        }

        private Node ResolveParent(ParsedNode parsedNode)
        {
            var parentRef = parsedNode.Parent;
            if (string.IsNullOrEmpty(parentRef) || parentRef == "." || parentRef == "..")
                return _rootNode;

            var pathParts = parentRef.Split('/');
            var current = _rootNode;

            foreach (var part in pathParts)
            {
                if (string.IsNullOrEmpty(part) || part == ".") continue;
                if (current == null) { if (!_builtNodes.TryGetValue(part, out current)) return null; }
                else { current = current.GetNodeOrNull(part); if (current == null) return null; }
            }
            return current;
        }

        private static void ApplyProperties(Node node, ParsedNode parsedNode, ParsedScene scene, Func<string, Resource> extResourceResolver, Func<string, Node> instanceLoader)
        {
            var ctx = new PropertyContext(scene, instanceLoader, extResourceResolver);
            foreach (var kvp in parsedNode.OrderedProperties)
            {
                try
                {
                    var r = PropertyResolverRegistry.Find(kvp.Key, kvp.Value);
                    r?.Apply(node, kvp.Key, kvp.Value, ctx);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("TSCN_BUILD", $"SceneBuilder: failed to apply '{kvp.Key}' on '{node.Name}': {ex.Message}", LogLevel.Warning);
                }
            }
        }

        private void ApplyNodePathVars(Node node, Dictionary<string, string> nodePathVars)
        {
            if (nodePathVars == null || nodePathVars.Count == 0) return;
            foreach (var kvp in nodePathVars)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;
                var parsed = TscnPropertyParser.Parse(kvp.Value);
                if (parsed is ParsedGodotValue pgv && pgv.TypeName == "NodePath")
                    _nodePathAssignments.Add((node, kvp.Key, new NodePath((string)pgv.Arg(0))));
            }
        }

        /// <summary>
        /// Resolves exported Node-typed properties (declared in a tscn node's
        /// <c>node_paths</c> header) once the whole tree exists. Godot's native
        /// loader turns these NodePaths into the referenced nodes; the runtime
        /// builder must do the same, otherwise <c>[Export] private Control foo</c>
        /// fields stay null. Resolution is deferred because a node's children are
        /// added only after its own properties have been applied.
        /// </summary>
        private void ResolveNodePathAssignments()
        {
            foreach (var (node, key, path) in _nodePathAssignments)
            {
                if (node == null || !GodotObject.IsInstanceValid(node))
                    continue;

                var target = node.GetNodeOrNull(path);
                if (target == null)
                {
                    GameLogger.Log("TSCN_BUILD", $"SceneBuilder: node_paths '{key}' -> '{path}' not found on '{node.Name}'", LogLevel.Warning);
                    continue;
                }

                node.Set(key, target);
            }

            _nodePathAssignments.Clear();
        }

        private void ApplyConnection(ParsedConnection conn)
        {
            Node fromNode = conn.From == "." ? _rootNode : (_builtNodes.TryGetValue(conn.From, out var fn) ? fn : null);
            Node toNode = conn.To == "." ? _rootNode : (_builtNodes.TryGetValue(conn.To, out var tn) ? tn : null);
            if (fromNode == null || toNode == null || !GodotObject.IsInstanceValid(fromNode) || !GodotObject.IsInstanceValid(toNode))
            {
                GameLogger.Log("TSCN_BUILD", $"SceneBuilder: cannot apply connection '{conn.Signal}' from '{conn.From}' to '{conn.To}'", LogLevel.Warning);
                return;
            }
            if (!fromNode.HasSignal(conn.Signal)) { GameLogger.Log("TSCN_BUILD", $"SceneBuilder: signal '{conn.Signal}' not found on '{fromNode.Name}'", LogLevel.Warning); return; }
            if (!toNode.HasMethod(conn.Method)) { GameLogger.Log("TSCN_BUILD", $"SceneBuilder: method '{conn.Method}' not found on '{toNode.Name}'", LogLevel.Warning); return; }
            fromNode.Connect(conn.Signal, new Callable(toNode, conn.Method), (uint)conn.Flags);
        }

        private static string ExtractResourceId(string refString)
        {
            if (string.IsNullOrEmpty(refString)) return null;
            const string prefix = "ExtResource(";
            var start = refString.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) return refString.Trim('"');
            start += prefix.Length;
            var end = refString.IndexOf(')', start);
            if (end < 0) return refString.Trim('"');
            return refString.Substring(start, end - start).Trim('"');
        }
    }
}
