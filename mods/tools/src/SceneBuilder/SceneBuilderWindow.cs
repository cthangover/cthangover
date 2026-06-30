using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.UI.Tool.SceneBuilder
{
    /// <summary>
    /// Developer tool window for interactively inspecting Godot scenes and
    /// running ad-hoc C# scripts against them. Features a scene selector, a
    /// wrapper template picker, a live viewport with a selectable node hierarchy
    /// tree, a code editor with monospace font, and an output panel for compilation
    /// or runtime results. Uses <see cref="SceneBuilderController"/> for all logic.
    /// </summary>
    public partial class SceneBuilderWindow : ToolWindow
    {
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

        /// <summary>Constructs the window, creates the controller, builds UI, and populates scene/wrapper dropdowns.</summary>
        public SceneBuilderWindow() : base("tools/scene_builder/title")
        {
            _controller = new SceneBuilderController();
            BuildUI();
            PopulateSceneList();
            PopulateWrapperList();
        }

        private void BuildUI()
        {
            var outerVBox = CreateFillContainer();
            AddChild(outerVBox);

            var toolbar = CreateToolbar();
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

            var leftVBox = new VBoxContainer();
            var leftPanel = CreateSidebar(leftVBox);
            mainHBox.AddChild(leftPanel);

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

            var rightVBox = new VBoxContainer();
            var rightPanel = CreateSidebar(rightVBox, 380);
            mainHBox.AddChild(rightPanel);

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
