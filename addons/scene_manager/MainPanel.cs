#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SceneManagerAddon
{
    [Tool]
    public partial class MainPanel : VBoxContainer
    {
        private TabContainer _tabs;
        private SceneTreePanel _treePanel;
        private ScenarioTextPanel _textPanel;
        private GraphView _graphView;
        private ValidationPanel _validationPanel;
        private Label _statusLabel;

        private List<ModSceneInfo> _mods;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(500, 400);
            BuildUi();
        }

        public void Refresh()
        {
            _mods = SceneDataLoader.LoadAll();
            SceneValidator.Validate(_mods);

            _treePanel.Populate(_mods);
            _graphView.Populate(_mods);
            _validationPanel.Populate(_mods);

            var scenes = _mods.Sum(m => m.Scenes.Count);
            var scenarios = _mods.Sum(m => m.Scenes.Sum(s => s.Scenarios.Count));
            _statusLabel.Text = $"Loaded {scenes} scenes, {scenarios} scenarios from {_mods.Count} mod(s)";
        }

        private void BuildUi()
        {
            var toolbar = new HBoxContainer();
            AddChild(toolbar);

            var refresh = new Button { Text = "Refresh" };
            refresh.Pressed += Refresh;
            toolbar.AddChild(refresh);

            _statusLabel = new Label { Text = "Not loaded" };
            toolbar.AddChild(_statusLabel);

            _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            AddChild(_tabs);

            BuildScenesTab();
            BuildGraphTab();
            BuildValidationTab();
        }

        private void BuildScenesTab()
        {
            var outer = new VBoxContainer();
            _tabs.AddChild(outer);
            _tabs.SetTabTitle(_tabs.GetTabCount() - 1, "Scenes");

            var split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            outer.AddChild(split);

            _treePanel = new SceneTreePanel();
            _treePanel.SceneSelected += scene => _textPanel.ShowSceneJson(scene);
            _treePanel.ScenarioSelected += (sc, modId) => _textPanel.ShowScenario(sc, modId);
            split.AddChild(_treePanel);

            _textPanel = new ScenarioTextPanel();
            split.AddChild(_textPanel);
        }

        private void BuildGraphTab()
        {
            _graphView = new GraphView();
            _graphView.ScenarioLinkClicked += (modId, sceneName, scName) =>
            {
                _tabs.CurrentTab = 0;
                _treePanel.SelectItem(modId, sceneName, scName);
            };
            _tabs.AddChild(_graphView);
            _tabs.SetTabTitle(_tabs.GetTabCount() - 1, "Graph");
        }

        private void BuildValidationTab()
        {
            _validationPanel = new ValidationPanel();
            _validationPanel.ErrorSelected += filePath =>
            {
                _tabs.CurrentTab = 0;
                foreach (var mod in _mods)
                {
                    foreach (var scene in mod.Scenes)
                    {
                        foreach (var sc in scene.Scenarios)
                        {
                            if (sc.FilePath == filePath)
                            {
                                _textPanel.ShowScenario(sc, mod.ModId);
                                return;
                            }
                        }
                        if (scene.FilePath == filePath)
                        {
                            _textPanel.ShowSceneJson(scene);
                            return;
                        }
                    }
                }
            };
            _tabs.AddChild(_validationPanel);
            _tabs.SetTabTitle(_tabs.GetTabCount() - 1, "Validation");
        }
    }
}
#endif
