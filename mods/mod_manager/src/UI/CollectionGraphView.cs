using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Dependency graph of one <see cref="ModCollectionGraph"/> rendered with
    /// <c>GraphEdit</c>. Nodes are mods, arrows point from a dependency to the
    /// mod that needs it, and every mechanic gets its own <c>GraphFrame</c>
    /// whose title is the mechanic name — the collection is never shown as a
    /// flat list, the grouping is what tells the player which layer of the game
    /// each mod implements.
    /// <para/>
    /// The view is read-only: connections cannot be dragged, node positions are
    /// assigned deterministically (mechanics top-to-bottom, mods in rows of
    /// four) so the same collection always lays out the same way. Node colors
    /// follow <see cref="CollectionStateNames"/>; clicking a node reports it
    /// through <see cref="NodeSelected"/> and the screen fills the details
    /// panel, clicking empty space clears the selection.
    /// </summary>
    public partial class CollectionGraphView : Control
    {
        private const float OriginX = 40f;
        private const float OriginY = 40f;
        private const float NodeWidth = 240f;
        private const float NodeHeight = 110f;
        private const float NodeGapX = 40f;
        private const float RowGapY = 40f;
        private const float FramePaddingX = 30f;
        private const float FrameHeaderHeight = 48f;
        private const int NodesPerRow = 4;

        private GraphEdit _graph;
        private readonly Dictionary<string, ModCollectionNode> _nodesByGraphName = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised when the player clicks a mod node; the payload is the resolved graph node.</summary>
        public event Action<ModCollectionNode> NodeSelected;

        /// <summary>Raised when the selection is cleared by clicking empty space.</summary>
        public event Action SelectionCleared;

        /// <summary>Rebuilds the whole graph for the given resolved collection.</summary>
        public void Populate(ModCollectionGraph graph)
        {
            EnsureGraph();
            Clear();

            if (graph == null)
                return;

            var cursorY = OriginY;
            foreach (var mechanic in graph.Mechanics)
            {
                var members = CollectMembers(graph, mechanic.Id);
                if (members.Count == 0)
                    continue;

                var title = string.IsNullOrEmpty(mechanic.Name) ? mechanic.Id : mechanic.Name;
                if (mechanic.Optional)
                    title += " · " + TranslationServer.Translate("mod_manager/collection_optional");

                cursorY = AddFrame(title, members, cursorY);
            }

            var external = CollectExternal(graph);
            if (external.Count > 0)
                AddFrame(TranslationServer.Translate("mod_manager/collection_externals"), external, cursorY);

            ConnectEdges(graph);
        }

        /// <summary>Removes all frames, nodes and connections.</summary>
        public void Clear()
        {
            if (_graph == null)
                return;

            _graph.ClearConnections();
            foreach (var child in _graph.GetChildren())
            {
                if (child is GraphElement element)
                {
                    _graph.RemoveChild(element);
                    element.QueueFree();
                }
            }

            _nodesByGraphName.Clear();
        }

        private void EnsureGraph()
        {
            if (_graph != null && GodotObject.IsInstanceValid(_graph))
                return;

            _graph = GetNode<GraphEdit>("Graph");
            _graph.RightDisconnects = false;
            _graph.MinimapEnabled = true;
            _graph.ShowGrid = true;
            _graph.SnappingEnabled = true;
            _graph.SnappingDistance = 20;
            _graph.ConnectionLinesThickness = 2f;
            _graph.ConnectionLinesAntialiased = true;
            _graph.NodeSelected += OnGraphNodeSelected;
            _graph.NodeDeselected += OnGraphNodeDeselected;
        }

        /// <summary>
        /// Lays out one mechanic as a frame with its mods in rows of four and
        /// returns the Y coordinate below the frame. The frame autoshrinks
        /// around the attached nodes, so only the vertical cursor is computed here.
        /// </summary>
        private float AddFrame(string title, List<ModCollectionNode> members, float cursorY)
        {
            var frame = new GraphFrame
            {
                Title = title,
                AutoshrinkEnabled = true,
                AutoshrinkMargin = 24,
            };
            _graph.AddChild(frame);
            frame.PositionOffset = new Vector2(OriginX, cursorY);

            var x = OriginX + FramePaddingX;
            var y = cursorY + FrameHeaderHeight;
            var inRow = 0;

            foreach (var member in members)
            {
                var node = CreateNode(member);
                _graph.AddChild(node);
                node.PositionOffset = new Vector2(x, y);
                _graph.AttachGraphElementToFrame(node.Name, frame.Name);
                _nodesByGraphName[member.ModId] = member;

                inRow++;
                if (inRow >= NodesPerRow)
                {
                    inRow = 0;
                    x = OriginX + FramePaddingX;
                    y += NodeHeight + RowGapY;
                }
                else
                {
                    x += NodeWidth + NodeGapX;
                }
            }

            return y + NodeHeight + RowGapY + 24f;
        }

        private static GraphNode CreateNode(ModCollectionNode data)
        {
            var graphNode = new GraphNode
            {
                Name = data.ModId,
                Title = string.IsNullOrEmpty(data.Title) ? data.ModId : data.Title,
                Size = new Vector2(NodeWidth, NodeHeight),
            };

            graphNode.AddChild(new Label
            {
                Text = BuildSubtitle(data),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(NodeWidth - 24, 0),
            });

            var color = CollectionStateNames.Color(data.State);
            var titlebar = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Alpha(color, 0.3f));
            titlebar.CornerRadiusBottomLeft = 0;
            titlebar.CornerRadiusBottomRight = 0;
            graphNode.AddThemeStyleboxOverride("titlebar", titlebar);
            graphNode.SetSlot(0, true, 0, UiTokens.White, true, 0, color);
            graphNode.TooltipText = $"{graphNode.Title}\n{data.ModId}";

            return graphNode;
        }

        private static string BuildSubtitle(ModCollectionNode data)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(data.InstalledVersion))
                lines.Add($"v{data.InstalledVersion}");
            if (!string.IsNullOrEmpty(data.RequiredVersion))
                lines.Add(data.RequiredVersion);
            lines.Add(CollectionStateNames.Translate(data.State));
            return string.Join("\n", lines);
        }

        private void ConnectEdges(ModCollectionGraph graph)
        {
            foreach (var edge in graph.Edges)
            {
                if (!_nodesByGraphName.TryGetValue(edge.FromModId, out var from)
                    || !_nodesByGraphName.TryGetValue(edge.ToModId, out var to))
                {
                    continue;
                }

                if (string.Equals(from.ModId, to.ModId, StringComparison.OrdinalIgnoreCase))
                    continue;

                _graph.ConnectNode(from.ModId, 0, to.ModId, 0);
            }
        }

        private static List<ModCollectionNode> CollectMembers(ModCollectionGraph graph, string mechanicId)
        {
            var result = new List<ModCollectionNode>();
            foreach (var node in graph.Nodes)
            {
                if (string.Equals(node.MechanicId, mechanicId, StringComparison.OrdinalIgnoreCase))
                    result.Add(node);
            }

            return result;
        }

        private static List<ModCollectionNode> CollectExternal(ModCollectionGraph graph)
        {
            var result = new List<ModCollectionNode>();
            foreach (var node in graph.Nodes)
            {
                if (node.IsExternal)
                    result.Add(node);
            }

            return result;
        }

        private void OnGraphNodeSelected(Node node)
        {
            if (node is not GraphNode graphNode)
                return;

            if (_nodesByGraphName.TryGetValue(graphNode.Name.ToString(), out var data))
                NodeSelected?.Invoke(data);
        }

        private void OnGraphNodeDeselected(Node node)
        {
            SelectionCleared?.Invoke();
        }
    }
}
