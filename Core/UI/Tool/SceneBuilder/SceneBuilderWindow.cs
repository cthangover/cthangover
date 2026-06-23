using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.UI.Tool.SceneBuilder
{
    public partial class SceneBuilderWindow : Window
    {
        public static SceneBuilderWindow Open()
        {
            var window = new SceneBuilderWindow();
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            tree?.Root.AddChild(window);
            window.PopupCentered(new Vector2I(1400, 900));
            return window;
        }

        private SceneBuilderController _controller;

        private OptionButton _sceneDropdown;
        private OptionButton _wrapperDropdown;
        private Tree _hierarchyTree;
        private SubViewport _viewport;
        private TextEdit _codeEditor;
        private Button _runBtn;
        private RichTextLabel _outputLabel;

        private List<(string Name, string Path)> _scenes;
        private List<(string DisplayName, string Content)> _wrappers;
        private int _selectedWrapperIndex;

        public SceneBuilderWindow()
        {
            Title = TranslationServer.Translate("tools/scene_builder/title");
            Unresizable = false;
            Size = new Vector2I(1400, 900);
            CloseRequested += QueueFree;

            _controller = new SceneBuilderController();
            BuildUI();
            PopulateSceneList();
            PopulateWrapperList();
        }

        private void BuildUI()
        {
            var outerVBox = new VBoxContainer();
            outerVBox.AnchorRight = 1f;
            outerVBox.AnchorBottom = 1f;
            outerVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            outerVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            AddChild(outerVBox);

            var toolbar = new HBoxContainer();
            toolbar.AddThemeConstantOverride("separation", 8);
            outerVBox.AddChild(toolbar);

            toolbar.AddChild(new Label { Text = TranslationServer.Translate("tools/scene_builder/scene") });

            _sceneDropdown = new OptionButton();
            _sceneDropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _sceneDropdown.ItemSelected += OnSceneSelected;
            toolbar.AddChild(_sceneDropdown);

            toolbar.AddChild(new Label { Text = TranslationServer.Translate("tools/scene_builder/wrapper") });

            _wrapperDropdown = new OptionButton();
            _wrapperDropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _wrapperDropdown.ItemSelected += OnWrapperSelected;
            toolbar.AddChild(_wrapperDropdown);

            var mainHBox = new HBoxContainer();
            mainHBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            outerVBox.AddChild(mainHBox);

            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(280, 0);
            leftPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainHBox.AddChild(leftPanel);

            var leftVBox = new VBoxContainer();
            leftPanel.AddChild(leftVBox);

            leftVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scene_builder/hierarchy") });

            _hierarchyTree = new Tree();
            _hierarchyTree.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _hierarchyTree.HideRoot = true;
            _hierarchyTree.CellSelected += OnTreeCellSelected;
            leftVBox.AddChild(_hierarchyTree);

            var previewContainer = new PanelContainer();
            previewContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            previewContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainHBox.AddChild(previewContainer);

            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(1920, 1080);

            var viewportContainer = new SubViewportContainer();
            viewportContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            viewportContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            viewportContainer.Stretch = true;
            viewportContainer.StretchShrink = 1;
            viewportContainer.AddChild(_viewport);
            previewContainer.AddChild(viewportContainer);

            var rightPanel = new PanelContainer();
            rightPanel.CustomMinimumSize = new Vector2(380, 0);
            rightPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainHBox.AddChild(rightPanel);

            var rightVBox = new VBoxContainer();
            rightPanel.AddChild(rightVBox);

            rightVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scene_builder/code") });

            _codeEditor = new TextEdit();
            _codeEditor.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _codeEditor.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _codeEditor.WrapMode = TextEdit.LineWrappingMode.None;
            _codeEditor.AddThemeFontOverride("font", GetMonospaceFont());
            rightVBox.AddChild(_codeEditor);

            _runBtn = new Button();
            _runBtn.Text = TranslationServer.Translate("tools/scene_builder/run");
            _runBtn.Pressed += OnRunPressed;
            rightVBox.AddChild(_runBtn);

            var outputHeader = new HBoxContainer();
            var outputLabel = new Label { Text = TranslationServer.Translate("tools/scene_builder/output") };
            outputLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            outputHeader.AddChild(outputLabel);

            var clearOutputBtn = new Button { Text = TranslationServer.Translate("tools/scene_builder/clear") };
            clearOutputBtn.Pressed += () => _outputLabel.Text = "";
            outputHeader.AddChild(clearOutputBtn);

            rightVBox.AddChild(outputHeader);

            _outputLabel = new RichTextLabel();
            _outputLabel.BbcodeEnabled = true;
            _outputLabel.FitContent = false;
            _outputLabel.ScrollActive = true;
            _outputLabel.ScrollFollowing = true;
            _outputLabel.CustomMinimumSize = new Vector2(0, 80);
            _outputLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            rightVBox.AddChild(_outputLabel);
        }

        private static Font GetMonospaceFont()
        {
            try
            {
                if (ResourceLoader.Exists("res://assets/fonts/RobotoMono.ttf"))
                    return ResourceLoader.Load<FontFile>("res://assets/fonts/RobotoMono.ttf");
            }
            catch { }
            return ThemeDB.FallbackFont;
        }

        private void PopulateSceneList()
        {
            _sceneDropdown.Clear();
            _sceneDropdown.AddItem(TranslationServer.Translate("tools/scene_builder/select_scene"));
            _sceneDropdown.Selected = 0;

            _scenes = _controller.GetSceneList();
            foreach (var s in _scenes)
                _sceneDropdown.AddItem(s.Name);
        }

        private void PopulateWrapperList()
        {
            _wrapperDropdown.Clear();
            _wrappers = _controller.GetWrappers();

            foreach (var w in _wrappers)
                _wrapperDropdown.AddItem(w.DisplayName);

            if (_wrappers.Count > 0)
            {
                _wrapperDropdown.Selected = 0;
                _selectedWrapperIndex = 0;
            }
        }

        private void OnSceneSelected(long index)
        {
            if (index == 0)
            {
                _controller.UnloadScene();
                _hierarchyTree.Clear();
                return;
            }

            var sceneIndex = (int)index - 1;
            if (sceneIndex < 0 || sceneIndex >= _scenes.Count)
                return;

            var scene = _scenes[sceneIndex];
            _controller.LoadScene(scene.Path, _viewport);
            _controller.BuildHierarchy(_hierarchyTree);
        }

        private void OnWrapperSelected(long index)
        {
            _selectedWrapperIndex = (int)index;
        }

        private void OnTreeCellSelected()
        {
            var item = _hierarchyTree.GetSelected();
            if (item != null)
                _controller.SelectNode(item);
        }

        private void OnRunPressed()
        {
            var userCode = _codeEditor.Text;

            if (_wrappers == null || _selectedWrapperIndex < 0 || _selectedWrapperIndex >= _wrappers.Count)
            {
                ShowOutput(TranslationServer.Translate("tools/scene_builder/error_no_wrapper"), true);
                return;
            }

            var wrapperContent = _wrappers[_selectedWrapperIndex].Content;

            var scenePath = _currentScenePath();
            if (scenePath == null)
            {
                ShowOutput(TranslationServer.Translate("tools/scene_builder/error_no_scene"), true);
                return;
            }

            _controller.LoadScene(scenePath, _viewport);
            _controller.BuildHierarchy(_hierarchyTree);

            var result = _controller.RunCode(userCode, wrapperContent);
            ShowOutput(result.Message, result.IsError);
        }

        private string _currentScenePath()
        {
            var idx = (int)_sceneDropdown.Selected - 1;
            if (idx < 0 || idx >= (_scenes?.Count ?? 0))
                return null;
            return _scenes[idx].Path;
        }

        private void ShowOutput(string text, bool isError)
        {
            if (_outputLabel == null)
                return;

            var color = isError ? "red" : "green";
            _outputLabel.Text = $"[color={color}]{text}[/color]";
        }
    }
}
