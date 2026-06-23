#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SceneManagerAddon
{
    [Tool]
    public partial class GraphView : HSplitContainer
    {
        private GraphEdit _graph;
        private RichTextLabel _infoLabel;
        private Dictionary<string, GraphNode> _nodes;
        private Dictionary<string, SceneDefInfo> _lookup;

        private List<ModSceneInfo> _mods;

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            _graph = new GraphEdit
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                RightDisconnects = true,
                ConnectionLinesThickness = 2f,
                ConnectionLinesAntialiased = true,
                MinimapEnabled = true,
                ShowGrid = true,
                SnappingEnabled = true,
                SnappingDistance = 20
            };
            _graph.ConnectionRequest += (a, b, c, d) => { };
            _graph.DisconnectionRequest += (a, b, c, d) => { };
            _graph.NodeSelected += OnNodeSelected;
            _graph.NodeDeselected += OnNodeDeselected;
            AddChild(_graph);

            var panel = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
            AddChild(panel);

            panel.AddChild(new Label
            {
                Text = "[ Select a node to see details ]",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });

            _infoLabel = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollFollowing = true,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _infoLabel.MetaClicked += OnLinkClicked;
            panel.AddChild(_infoLabel);
        }

        public void Populate(List<ModSceneInfo> mods)
        {
            _mods = mods;
            Clear();
            _nodes = new Dictionary<string, GraphNode>();
            _lookup = new Dictionary<string, SceneDefInfo>();

            int x = 40, y = 40;

            foreach (var mod in mods)
            {
                foreach (var scene in mod.Scenes)
                {
                    var key = scene.Name.ToLowerInvariant();
                    if (_nodes.ContainsKey(key)) continue;

                    var isStart = scene.Name == "start_scene";
                    var bgCount = scene.DefaultBackgrounds?.Count ?? 0;

                    var node = new GraphNode
                    {
                        Title = scene.Name,
                        Size = new Vector2(240, 90),
                        Name = "gn_" + scene.Name.Replace(".", "_").Replace(" ", "_"),
                        PositionOffset = new Vector2(x, y)
                    };

                    node.AddChild(new Label
                    {
                        Text = $"Mod: {mod.ModId}\nScenarios: {scene.Scenarios.Count}" +
                               (bgCount > 0 ? $"\nBg: {scene.DefaultBackgrounds.First()}" +
                               (bgCount > 1 ? $" +{bgCount - 1}" : "") : "")
                    });

                    var color = Colors.White;
                    if (isStart) color = new Color(0.3f, 1f, 0.3f);
                    else if (scene.HasErrors) color = new Color(1f, 0.3f, 0.3f);
                    node.SetSlot(0, false, 0, Colors.White, true, 0, color);

                    _graph.AddChild(node);
                    _nodes[key] = node;
                    _lookup[key] = scene;

                    x += 280;
                    if (x > 1000) { x = 40; y += 160; }
                }
            }

            foreach (var mod in mods)
                foreach (var scene in mod.Scenes)
                    foreach (var sc in scene.Scenarios)
                        foreach (var target in sc.SwitchSceneTargets)
                        {
                            var srcK = scene.Name.ToLowerInvariant();
                            var dstK = target.ToLowerInvariant();
                            if (!_nodes.ContainsKey(srcK) || !_nodes.ContainsKey(dstK)) continue;
                            _graph.ConnectNode(_nodes[srcK].Name, 0, _nodes[dstK].Name, 0);
                        }
        }

        private void Clear()
        {
            foreach (var c in _graph.GetChildren())
                if (c is GraphNode) c.QueueFree();
            _infoLabel.Text = "";
        }

        private void OnNodeSelected(Node n)
        {
            if (n is not GraphNode gn) return;
            var key = gn.Title.ToString().ToLowerInvariant();
            if (!_lookup.TryGetValue(key, out var scene)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Scene: [b]{scene.Name}[/b]");
            sb.AppendLine($"Mod: {scene.ModId}");
            sb.AppendLine($"File: {scene.FilePath}");
            sb.AppendLine($"Backgrounds: {string.Join(", ", scene.DefaultBackgrounds)}");
            sb.AppendLine();
            sb.AppendLine($"Scenarios ({scene.Scenarios.Count}):");

            foreach (var sc in scene.Scenarios.OrderBy(s => s.Priority))
            {
                var url = System.Uri.EscapeDataString($"{scene.ModId}|{scene.Name}|{sc.Name}");
                sb.AppendLine($"  [{sc.Priority}] [url={url}]{sc.Name}[/url]");
                sb.AppendLine($"      {sc.FilePath}");
                if (!string.IsNullOrEmpty(sc.Condition))
                    sb.AppendLine($"      if: {sc.Condition}");
            }
            _infoLabel.Text = sb.ToString();
        }

        private void OnNodeDeselected(Node n) => _infoLabel.Text = "";

        public event ScenarioLinkHandler ScenarioLinkClicked;

        private void OnLinkClicked(Variant meta)
        {
            var parts = meta.AsString().Split('|');
            if (parts.Length == 3)
                ScenarioLinkClicked?.Invoke(parts[0], parts[1], parts[2]);
        }
    }
}
#endif
