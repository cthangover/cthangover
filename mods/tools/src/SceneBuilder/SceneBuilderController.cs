using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Mods;
using Godot;

namespace Cthangover.Core.UI.Tool.SceneBuilder
{
    /// <summary>
    /// Controller for the scene builder tool. Loads Godot <c>.tscn</c> scenes into
    /// a <see cref="SubViewport"/>, builds an interactive node hierarchy tree, and
    /// executes user-supplied C# code via <see cref="ModCompiler"/>. The code is
    /// wrapped in a template (selected from mod-provided wrappers) that replaces
    /// <c>{{USER_CODE}}</c> with the user's input, then compiled at runtime and
    /// invoked through a static <c>Run(Node root)</c> method. Scene list is sourced
    /// from <see cref="Scenes.ModScenes.CollectTscnFiles"/>.
    /// </summary>
    public class SceneBuilderController
    {
        private Node _loadedRoot;
        private ColorRect _highlight;
        private readonly Dictionary<Node, TreeItem> _nodeToItem = new();
        private readonly Dictionary<TreeItem, Node> _itemToNode = new();
        private string _currentScenePath;

        /// <summary>Returns all available <c>.tscn</c> scene files as (Name, Path) tuples.</summary>
        public List<(string Name, string Path)> GetSceneList()
        {
            return Scenes.ModScenes.CollectTscnFiles();
        }

        /// <summary>Returns all code wrapper templates registered by mods, each with a display name and content.</summary>
        public List<(string DisplayName, string Content)> GetWrappers()
        {
            var result = new List<(string, string)>();
            var modWrappers = ModManager.Instance.CollectWrapperTemplates();
            if (modWrappers != null)
            {
                foreach (var w in modWrappers)
                    result.Add(($"[{w.ModId}] {w.Name}", w.Content));
            }
            return result;
        }

        /// <summary>
        /// Loads a packed scene into <paramref name="viewport"/>, adds an orange
        /// highlight <see cref="ColorRect"/> for node selection visual feedback,
        /// and stores the instance as the current root.
        /// </summary>
        public Node LoadScene(string path, SubViewport viewport)
        {
            UnloadScene();

            var packed = GD.Load<PackedScene>(path);
            if (packed == null)
                return null;

            var instance = packed.Instantiate();
            viewport.AddChild(instance);

            _highlight = new ColorRect();
            _highlight.Color = new Color(1f, 0.7f, 0f, 0.35f);
            _highlight.Visible = false;
            _highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
            _highlight.ZIndex = 4096;
            instance.AddChild(_highlight);
            _highlight.Owner = instance;

            _loadedRoot = instance;
            _currentScenePath = path;
            return instance;
        }

        /// <summary>Frees the currently loaded scene root and clears all node-item mappings.</summary>
        public void UnloadScene()
        {
            _nodeToItem.Clear();
            _itemToNode.Clear();
            _highlight = null;

            if (_loadedRoot != null && GodotObject.IsInstanceValid(_loadedRoot))
            {
                _loadedRoot.QueueFree();
            }

            _loadedRoot = null;
            _currentScenePath = null;
        }

        /// <summary>
        /// Recursively walks the loaded scene tree and populates the given
        /// <see cref="Tree"/> control with nodes labelled <c>Name (Type)</c>.
        /// Skips the internal highlight rect.
        /// </summary>
        public void BuildHierarchy(Tree tree)
        {
            tree.Clear();
            _nodeToItem.Clear();
            _itemToNode.Clear();

            if (_loadedRoot == null)
                return;

            var treeRoot = tree.CreateItem();
            tree.HideRoot = true;
            AddNodeToTree(tree, treeRoot, _loadedRoot);
        }

        private void AddNodeToTree(Tree tree, TreeItem parent, Node node)
        {
            var item = tree.CreateItem(parent);
            var typeName = node.GetType().Name;
            item.SetText(0, $"{node.Name} ({typeName})");
            item.SetSelectable(0, true);

            _nodeToItem[node] = item;
            _itemToNode[item] = node;

            for (int i = 0; i < node.GetChildCount(); i++)
            {
                var child = node.GetChild(i);
                if (child != _highlight)
                    AddNodeToTree(tree, item, child);
            }
        }

        /// <summary>
        /// Highlights the selected node by positioning the highlight rect over it.
        /// For <see cref="Control"/> nodes, uses <c>GetGlobalRect()</c>; for others,
        /// approximates the position via parent chain traversal. Hides the highlight
        /// if the root node itself is selected.
        /// </summary>
        public void SelectNode(TreeItem item)
        {
            if (_highlight == null || item == null)
            {
                HideHighlight();
                return;
            }

            if (!_itemToNode.TryGetValue(item, out var node))
            {
                HideHighlight();
                return;
            }

            if (node == _loadedRoot)
            {
                HideHighlight();
                return;
            }

            var control = node as Control;
            if (control != null)
            {
                var rect = control.GetGlobalRect();
                _highlight.Position = rect.Position;
                _highlight.Size = rect.Size;
                _highlight.Visible = true;
            }
            else
            {
                var pos = new Vector2(0, 0);
                var current = node;
                while (current != _loadedRoot && current != null)
                {
                    if (current is Control c)
                    {
                        pos += c.GetGlobalRect().Position;
                        break;
                    }
                    current = current.GetParent();
                }

                _highlight.Position = pos;
                _highlight.Size = new Vector2(20, 20);
                _highlight.Color = new Color(1f, 0.3f, 0.3f, 0.5f);
                _highlight.Visible = true;
            }
        }

        /// <summary>Hides the node highlight overlay and resets its colour.</summary>
        public void HideHighlight()
        {
            if (_highlight != null)
                _highlight.Visible = false;
            _highlight.Color = new Color(1f, 0.7f, 0f, 0.35f);
        }

        /// <summary>
        /// Compiles and executes user code on the loaded scene. Substitutes
        /// <c>{{USER_CODE}}</c> in the wrapper template, compiles with
        /// <see cref="ModCompiler.CompileString"/>, then invokes the static
        /// <c>Run(Node)</c> method of the resulting assembly's
        /// <c>SceneBuilderScript</c> type. Returns error details on any
        /// compilation or runtime failure.
        /// </summary>
        /// <param name="userCode">The C# code entered by the user.</param>
        /// <param name="wrapperContent">The wrapper template containing <c>{{USER_CODE}}</c>.</param>
        /// <returns>A tuple with <c>IsError</c> flag and a localised message.</returns>
        public (bool IsError, string Message) RunCode(string userCode, string wrapperContent)
        {
            if (_loadedRoot == null)
                return (true, TranslationServer.Translate("tools/scene_builder/error_no_scene"));

            if (string.IsNullOrWhiteSpace(userCode))
                return (true, TranslationServer.Translate("tools/scene_builder/error_no_code"));

            if (string.IsNullOrWhiteSpace(wrapperContent))
                return (true, TranslationServer.Translate("tools/scene_builder/error_no_wrapper"));

            var wrappedCode = wrapperContent.Replace("{{USER_CODE}}", userCode);

            CompileResult compileResult;
            try
            {
                compileResult = ModCompiler.CompileString(wrappedCode, "SceneBuilderScript");
            }
            catch (Exception ex)
            {
                return (true, string.Format(TranslationServer.Translate("tools/scene_builder/compilation_exception"), ex.Message));
            }

            if (!compileResult.Success)
            {
                var errors = string.Join("\n", compileResult.Errors ?? Enumerable.Empty<string>());
                return (true, string.Format(TranslationServer.Translate("tools/scene_builder/compilation_failed"), errors));
            }

            try
            {
                var type = compileResult.Assembly.GetType("SceneBuilderScript");
                if (type == null)
                    return (true, TranslationServer.Translate("tools/scene_builder/error_type_not_found"));

                var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                    return (true, TranslationServer.Translate("tools/scene_builder/error_method_not_found"));

                method.Invoke(null, new object[] { _loadedRoot });
                return (false, TranslationServer.Translate("tools/scene_builder/execution_ok"));
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                return (true, string.Format(TranslationServer.Translate("tools/scene_builder/runtime_error"), inner.Message));
            }
            catch (Exception ex)
            {
                return (true, string.Format(TranslationServer.Translate("tools/scene_builder/runtime_error"), ex.Message));
            }
        }
    }
}
