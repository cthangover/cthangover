using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Tools.Services;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Tool.ScenarioEditor
{
    /// <summary>
    /// Multi-tab scenario script editor with syntax highlighting, file tree browser,
    /// background thumbnail previews, metadata/parsed-info inspection, and integrated
    /// test-play. Scans all <c>.scenario</c> files across installed mods via
    /// <see cref="ScenarioFileService.ScanScenarioFiles"/> and displays them in a
    /// tree grouped by mod and directory. Tracks per-tab dirty state and supports
    /// save, open-in-external-editor, and one-click playtest that temporarily swaps
    /// the running scene to the scenario's target scene.
    /// </summary>
    public partial class ScenarioEditorWindow : ToolWindow
    {
        private sealed class TabData
        {
            public string FilePath;
            public string FileName;
            public string SavedText;
            public bool IsDirty;
        }

        private Control _editLayout;
        private Control _playLayout;

        private Tree _fileTree;
        private TabBar _tabBar;
        private TextEdit _textEdit;
        private Button _saveBtn;
        private Button _openFileBtn;
        private Button _playBtn;
        private Label _filePathLabel;
        private Label _statusLabel;
        private ScrollContainer _thumbScroll;
        private HBoxContainer _thumbContainer;

        private Label _infoScene;
        private Label _infoPriority;
        private Label _infoCondition;
        private Label _infoSwitchTargets;
        private Label _infoLocaleKeys;
        private Label _infoQuestRefs;

        private readonly List<TabData> _tabs = new();
        private int _activeTabIndex = -1;
        private bool _suppressTextChanged;
        private bool _playActive;
        private CanvasLayer _stopOverlay;

        private readonly Dictionary<string, string> _fileItemToPath = new();

        /// <summary>Constructs the editor window, builds the full UI, and populates the file tree from all mods.</summary>
        public ScenarioEditorWindow() : base("tools/scenario_editor/title")
        {
            BuildUI();
            PopulateFileTree();
        }

        protected override void Cleanup()
        {
            _fileItemToPath.Clear();
            _tabs.Clear();
        }

        public override void _Process(double delta)
        {
            if (_playActive && !SceneManager.IsTestPlayActive)
            {
                ReturnFromPlay();
            }
        }

        private void BuildUI()
        {
            _editLayout = new Control
            {
                AnchorRight = 1f,
                AnchorBottom = 1f,
                Visible = true
            };
            AddChild(_editLayout);

            var outerVBox = CreateFillContainer();
            _editLayout.AddChild(outerVBox);

            var toolbar = CreateToolbar();
            outerVBox.AddChild(toolbar);

            _saveBtn = new Button
            {
                Text = TranslationServer.Translate("tools/scenario_editor/save"),
                Disabled = true
            };
            _saveBtn.Pressed += OnSavePressed;
            toolbar.AddChild(_saveBtn);

            _openFileBtn = new Button
            {
                Text = TranslationServer.Translate("tools/scenario_editor/open_file"),
                Disabled = true
            };
            _openFileBtn.Pressed += OnOpenFilePressed;
            toolbar.AddChild(_openFileBtn);

            _playBtn = new Button
            {
                Text = TranslationServer.Translate("tools/scenario_editor/play"),
                TooltipText = TranslationServer.Translate("tools/scenario_editor/play_tooltip"),
                Disabled = true
            };
            _playBtn.Pressed += OnPlayPressed;
            toolbar.AddChild(_playBtn);

            _filePathLabel = new Label { Text = "" };
            _filePathLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            toolbar.AddChild(_filePathLabel);

            var mainSplit = new HSplitContainer();
            mainSplit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            outerVBox.AddChild(mainSplit);

            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(340, 0);
            leftPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainSplit.AddChild(leftPanel);

            var leftVBox = new VBoxContainer();
            leftPanel.AddChild(leftVBox);

            var tabsContainer = new TabContainer();
            tabsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            tabsContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            leftVBox.AddChild(tabsContainer);

            var filesTab = new VBoxContainer();
            filesTab.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            filesTab.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            tabsContainer.AddChild(filesTab);
            tabsContainer.SetTabTitle(tabsContainer.GetTabCount() - 1, TranslationServer.Translate("tools/scenario_editor/tab_files"));

            _fileTree = new Tree();
            _fileTree.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _fileTree.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _fileTree.HideRoot = true;
            _fileTree.CellSelected += OnFileTreeCellSelected;
            filesTab.AddChild(_fileTree);

            var infoTab = new VBoxContainer();
            infoTab.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            infoTab.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            infoTab.AddThemeConstantOverride("separation", 6);
            tabsContainer.AddChild(infoTab);
            tabsContainer.SetTabTitle(tabsContainer.GetTabCount() - 1, TranslationServer.Translate("tools/scenario_editor/tab_info"));

            var infoScroll = new ScrollContainer();
            infoScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            infoScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            infoTab.AddChild(infoScroll);

            var infoVBox = new VBoxContainer();
            infoVBox.AddThemeConstantOverride("separation", 8);
            infoScroll.AddChild(infoVBox);

            infoVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scenario_editor/info_scene") + ":" });
            _infoScene = new Label { Text = "-" };
            infoVBox.AddChild(_infoScene);

            infoVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scenario_editor/info_priority") + ":" });
            _infoPriority = new Label { Text = "-" };
            infoVBox.AddChild(_infoPriority);

            infoVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scenario_editor/info_condition") + ":" });
            _infoCondition = new Label { Text = "-" };
            infoVBox.AddChild(_infoCondition);

            infoVBox.AddChild(new HSeparator());

            infoVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scenario_editor/info_switch_targets") + ":" });
            _infoSwitchTargets = new Label { Text = "-" };
            infoVBox.AddChild(_infoSwitchTargets);

            infoVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scenario_editor/info_locale_keys") + ":" });
            _infoLocaleKeys = new Label { Text = "-" };
            infoVBox.AddChild(_infoLocaleKeys);

            infoVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scenario_editor/info_quest_refs") + ":" });
            _infoQuestRefs = new Label { Text = "-" };
            infoVBox.AddChild(_infoQuestRefs);

            leftVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/scenario_editor/bg_previews") + ":" });

            _thumbScroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0, 110),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
                VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            leftVBox.AddChild(_thumbScroll);

            _thumbContainer = new HBoxContainer();
            _thumbScroll.AddChild(_thumbContainer);

            var rightVBox = new VBoxContainer();
            rightVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rightVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            var rightPanel = new PanelContainer();
            rightPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            rightPanel.AddChild(rightVBox);
            mainSplit.AddChild(rightPanel);

            _tabBar = new TabBar();
            _tabBar.TabCloseDisplayPolicy = TabBar.CloseButtonDisplayPolicy.ShowActiveOnly;
            _tabBar.TabSelected += OnTabSelected;
            _tabBar.TabClosePressed += OnTabClosePressed;
            rightVBox.AddChild(_tabBar);

            _textEdit = new TextEdit
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Editable = true,
                SyntaxHighlighter = ScenarioSyntaxService.CreateHighlighter(),
                WrapMode = TextEdit.LineWrappingMode.None
            };
            _textEdit.AddThemeFontOverride("font", GetMonospaceFont());
            _textEdit.TextChanged += OnTextChanged;
            rightVBox.AddChild(_textEdit);

            var statusBar = new HBoxContainer();
            outerVBox.AddChild(statusBar);

            _statusLabel = new Label { Text = "" };
            statusBar.AddChild(_statusLabel);

            _playLayout = new Control
            {
                AnchorRight = 1f,
                AnchorBottom = 1f,
                Visible = false
            };
            AddChild(_playLayout);

            var stopContainer = new HBoxContainer();
            stopContainer.Alignment = BoxContainer.AlignmentMode.Center;
            stopContainer.AnchorRight = 0.5f;
            stopContainer.AnchorLeft = 0.5f;
            stopContainer.OffsetTop = 8;
            _playLayout.AddChild(stopContainer);

            var stopBtn = new Button
            {
                Text = TranslationServer.Translate("tools/scenario_editor/stop"),
                CustomMinimumSize = new Vector2(120, 40)
            };
            stopBtn.Pressed += OnStopPressed;
            stopContainer.AddChild(stopBtn);
        }

        private TabData ActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;

        private void OnPlayPressed()
        {
            var tab = ActiveTab;
            if (tab == null)
                return;

            var text = _textEdit.Text;
            var metadata = ScenarioFileService.ParseMetadata(text);

            if (metadata.Scene == "-")
            {
                _statusLabel.Text = TranslationServer.Translate("tools/scenario_editor/no_scene_error");
                return;
            }

            try
            {
                ScenarioFileService.WriteAllText(tab.FilePath, text);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = string.Format(TranslationServer.Translate("tools/scenario_editor/save_error"), ex.Message);
                return;
            }

            tab.SavedText = text;
            if (tab.IsDirty)
            {
                tab.IsDirty = false;
                UpdateTabTitle(_activeTabIndex);
                UpdateGlobalDirty();
            }

            _editLayout.Visible = false;
            _playLayout.Visible = true;

            SceneManager.IsTestPlayActive = true;
            SceneManager.TestScenarioText = text;

            GameLogger.Log("SCENARIO_EDITOR", $"Test play START: scene='{metadata.Scene}', queue={_textEdit.Text.Split('\n').Length} lines");

            Visible = false;

            _stopOverlay = CreateStopOverlay();
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            tree?.Root.AddChild(_stopOverlay);

            var sceneManager = GetNodeOrNull<SceneManager>("/root/SceneManager");
            if (sceneManager != null)
            {
                sceneManager.Initialize();
                sceneManager.PendingSceneName = metadata.Scene;
            }

            var sceneService = GetNodeOrNull<GodotSceneService>("/root/GodotSceneService");
            sceneService?.LoadScene("res://Scenes/BaseScene.tscn");

            _playActive = true;
        }

        private void OnStopPressed()
        {
            SceneManager.IsTestPlayActive = false;
            ReturnFromPlay();
        }

        private void ReturnFromPlay()
        {
            _playActive = false;
            _playLayout.Visible = false;
            _editLayout.Visible = true;
            Visible = true;

            if (_stopOverlay != null)
            {
                _stopOverlay.QueueFree();
                _stopOverlay = null;
            }

            var sceneService = GetNodeOrNull<GodotSceneService>("/root/GodotSceneService");
            sceneService?.SwitchToMenu();
        }

        private CanvasLayer CreateStopOverlay()
        {
            var layer = new CanvasLayer { Layer = 128 };

            var btn = new Button
            {
                Text = TranslationServer.Translate("tools/scenario_editor/stop"),
                CustomMinimumSize = new Vector2(120, 40)
            };
            btn.Pressed += OnStopPressed;

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddChild(btn);

            layer.AddChild(margin);
            return layer;
        }

        private void OnTextChanged()
        {
            if (_suppressTextChanged)
                return;

            var tab = ActiveTab;
            if (tab == null)
                return;

            if (!tab.IsDirty)
            {
                tab.IsDirty = true;
                UpdateTabTitle(_activeTabIndex);
                UpdateGlobalDirty();
                _statusLabel.Text = TranslationServer.Translate("tools/scenario_editor/status_modified");
            }
        }

        private void UpdateTabTitle(int index)
        {
            if (index < 0 || index >= _tabs.Count)
                return;
            var tab = _tabs[index];
            _tabBar.SetTabTitle(index, tab.IsDirty ? tab.FileName + " *" : tab.FileName);
        }

        private void UpdateGlobalDirty()
        {
            var anyDirty = _tabs.Any(t => t.IsDirty);
            if (anyDirty != _dirty)
            {
                if (anyDirty)
                    SetDirty();
                else
                    MarkClean();
            }
        }

        private void OnTabSelected(long index)
        {
            SwitchToTab((int)index);
        }

        private void OnTabClosePressed(long index)
        {
            CloseTab((int)index);
        }

        private void SwitchToTab(int index)
        {
            if (index < 0 || index >= _tabs.Count || index == _activeTabIndex)
                return;

            if (_activeTabIndex >= 0 && _activeTabIndex < _tabs.Count)
                _tabs[_activeTabIndex].SavedText = _textEdit.Text;

            _activeTabIndex = index;
            _tabBar.CurrentTab = index;

            var tab = _tabs[index];

            _suppressTextChanged = true;
            _textEdit.Text = tab.SavedText;
            _suppressTextChanged = false;

            _filePathLabel.Text = ScenarioFileService.GetRelativePath(tab.FilePath);
            _saveBtn.Disabled = false;
            _openFileBtn.Disabled = false;
            _playBtn.Disabled = false;

            UpdateInfoPanel(tab.SavedText);
            LoadThumbnails(tab.SavedText);
            _statusLabel.Text = tab.IsDirty ? TranslationServer.Translate("tools/scenario_editor/status_modified") : "";
        }

        private void CloseTab(int index)
        {
            if (index < 0 || index >= _tabs.Count)
                return;

            if (_tabs[index].IsDirty)
            {
                var dialog = new ConfirmationDialog();
                dialog.Title = TranslationServer.Translate("tools/scenario_editor/unsaved_title");
                dialog.DialogText = TranslationServer.Translate("tools/scenario_editor/unsaved_switch_text");
                int capturedIndex = index;
                dialog.Confirmed += () =>
                {
                    dialog.QueueFree();
                    DoCloseTab(capturedIndex);
                };
                dialog.Canceled += () => dialog.QueueFree();
                AddChild(dialog);
                dialog.PopupCentered();
                return;
            }

            DoCloseTab(index);
        }

        private void DoCloseTab(int index)
        {
            _tabs.RemoveAt(index);
            _tabBar.RemoveTab(index);

            if (_tabs.Count == 0)
            {
                _activeTabIndex = -1;
                _suppressTextChanged = true;
                _textEdit.Text = "";
                _suppressTextChanged = false;
                _filePathLabel.Text = "";
                _saveBtn.Disabled = true;
                _openFileBtn.Disabled = true;
                _playBtn.Disabled = true;
                ClearThumbnails();
                UpdateInfoPanel("");
                _statusLabel.Text = "";
                UpdateGlobalDirty();
                return;
            }

            if (index == _activeTabIndex)
            {
                var newIndex = Math.Min(index, _tabs.Count - 1);
                _activeTabIndex = -1;
                SwitchToTab(newIndex);
            }
            else if (index < _activeTabIndex)
            {
                _activeTabIndex--;
                _tabBar.CurrentTab = _activeTabIndex;
            }

            UpdateGlobalDirty();
        }

        private void OpenFileInTab(string filePath)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].FilePath == filePath)
                {
                    SwitchToTab(i);
                    return;
                }
            }

            try
            {
                var text = ScenarioFileService.ReadAllText(filePath);
                var tab = new TabData
                {
                    FilePath = filePath,
                    FileName = Path.GetFileNameWithoutExtension(filePath),
                    SavedText = text,
                    IsDirty = false
                };

                _tabs.Add(tab);
                _tabBar.AddTab(tab.FileName);

                var newIndex = _tabs.Count - 1;
                SwitchToTab(newIndex);
            }
            catch (Exception ex)
            {
                GameLogger.Log("SCENARIO_EDITOR", $"Failed to load file: {ex.Message}", LogLevel.Error);
            }
        }

        private void PopulateFileTree()
        {
            _fileTree.Clear();
            _fileItemToPath.Clear();

            var root = _fileTree.CreateItem();
            var mods = ScenarioFileService.ScanScenarioFiles();

            foreach (var mod in mods)
            {
                var modItem = _fileTree.CreateItem(root);
                modItem.SetText(0, $"[{mod.ModId}]");
                modItem.SetSelectable(0, false);
                modItem.SetCustomColor(0, new Color(0.5f, 0.75f, 1f));

                foreach (var group in mod.Groups)
                {
                    TreeItem parentItem;
                    if (string.IsNullOrEmpty(group.DirectoryPath))
                    {
                        parentItem = modItem;
                    }
                    else
                    {
                        parentItem = _fileTree.CreateItem(modItem);
                        parentItem.SetText(0, group.DirectoryPath + "/");
                        parentItem.SetSelectable(0, false);
                    }

                    foreach (var file in group.Files)
                    {
                        var fileItem = _fileTree.CreateItem(parentItem);
                        fileItem.SetText(0, file.Name);
                        fileItem.SetMetadata(0, Variant.From("scenario"));
                        _fileItemToPath[GetItemKey(fileItem)] = file.AbsolutePath;
                    }
                }
            }
        }

        private static string GetItemKey(TreeItem item)
        {
            return item.GetInstanceId().ToString();
        }

        private void OnFileTreeCellSelected()
        {
            var item = _fileTree.GetSelected();
            if (item == null)
                return;

            var meta = item.GetMetadata(0);
            if (meta.VariantType != Variant.Type.String || meta.AsString() != "scenario")
                return;

            var key = GetItemKey(item);
            if (!_fileItemToPath.TryGetValue(key, out var filePath))
                return;

            OpenFileInTab(filePath);
        }

        private void UpdateInfoPanel(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _infoScene.Text = "-";
                _infoPriority.Text = "-";
                _infoCondition.Text = "-";
                _infoSwitchTargets.Text = "-";
                _infoLocaleKeys.Text = "-";
                _infoQuestRefs.Text = "-";
                return;
            }

            var metadata = ScenarioFileService.ParseMetadata(text);
            var refs = ScenarioFileService.ExtractReferences(text);

            _infoScene.Text = metadata.Scene;
            _infoPriority.Text = metadata.Priority;
            _infoCondition.Text = metadata.Condition;
            _infoSwitchTargets.Text = refs.SwitchTargets.Count > 0 ? string.Join(", ", refs.SwitchTargets.Distinct()) : "-";
            _infoLocaleKeys.Text = refs.LocaleKeys.Count > 0 ? string.Join(", ", refs.LocaleKeys.Distinct()) : "-";
            _infoQuestRefs.Text = refs.QuestRefs.Count > 0 ? string.Join(", ", refs.QuestRefs.Distinct()) : "-";
        }

        private void LoadThumbnails(string text)
        {
            ClearThumbnails();

            if (string.IsNullOrEmpty(text))
                return;

            var refs = ScenarioFileService.ExtractReferences(text);

            if (refs.BackgroundRefs.Count == 0)
            {
                _thumbContainer.AddChild(new Label
                {
                    Text = TranslationServer.Translate("tools/scenario_editor/no_backgrounds")
                });
                return;
            }

            foreach (var id in refs.BackgroundRefs.Distinct())
            {
                var tex = ModResourceService.LoadBackgroundTexture(id);

                if (tex != null)
                {
                    _thumbContainer.AddChild(new TextureRect
                    {
                        Texture = tex,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        CustomMinimumSize = new Vector2(120, 90),
                        TooltipText = id
                    });
                }
                else
                {
                    _thumbContainer.AddChild(new Label
                    {
                        Text = $"[? {id}]",
                        TooltipText = $"Not found: {id}"
                    });
                }
            }
        }

        private void ClearThumbnails()
        {
            foreach (var c in _thumbContainer.GetChildren())
                c.QueueFree();
        }

        private void OnSavePressed()
        {
            var tab = ActiveTab;
            if (tab == null)
                return;

            try
            {
                ScenarioFileService.WriteAllText(tab.FilePath, _textEdit.Text);
                tab.SavedText = _textEdit.Text;
                tab.IsDirty = false;
                UpdateTabTitle(_activeTabIndex);
                UpdateGlobalDirty();
                _statusLabel.Text = TranslationServer.Translate("tools/scenario_editor/status_saved");

                UpdateInfoPanel(_textEdit.Text);
                LoadThumbnails(_textEdit.Text);
                GameLogger.Log("SCENARIO_EDITOR", $"Saved: {tab.FilePath}");
            }
            catch (Exception ex)
            {
                GameLogger.Log("SCENARIO_EDITOR", $"Save failed: {ex.Message}", LogLevel.Error);
                _statusLabel.Text = string.Format(TranslationServer.Translate("tools/scenario_editor/save_error"), ex.Message);
            }
        }

        private void OnOpenFilePressed()
        {
            var tab = ActiveTab;
            if (tab == null)
                return;

            GD.Print($"[ScenarioEditor] Opening: {tab.FilePath}");
            var err = OS.ShellOpen(tab.FilePath);
            if (err != Error.Ok)
                GameLogger.Log("SCENARIO_EDITOR", $"ShellOpen error: {err}", LogLevel.Error);
        }
    }
}
