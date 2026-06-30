#if TOOLS
using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;
using Godot;

namespace SceneManagerAddon
{
    /// <summary>
    /// The right-hand panel of the Scenes tab. Displays the raw text of
    /// the currently selected scene JSON or scenario script in a
    /// syntax-highlighted <see cref="TextEdit"/> control. Below the
    /// editor, a horizontal thumbnail strip renders previews of every
    /// background texture referenced by the selected scenario. An
    /// "Open file" button in the toolbar launches the system default
    /// text editor for the current scenario's
    /// <see cref="ScenarioDefInfo.AbsoluteFilePath"/>.
    /// </summary>
    [Tool]
    public partial class ScenarioTextPanel : VBoxContainer
    {
        private TextEdit _textEdit;
        private ScrollContainer _thumbScroll;
        private HBoxContainer _thumbContainer;
        private Label _thumbLabel;
        private Button _openBtn;
        private Label _fileLabel;

        private ScenarioDefInfo _currentScenario;

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var toolbar = new HBoxContainer();
            AddChild(toolbar);

            _openBtn = new Button { Text = "Open file", Disabled = true };
            _openBtn.Pressed += OnOpenPressed;
            toolbar.AddChild(_openBtn);

            _fileLabel = new Label { Text = "" };
            toolbar.AddChild(_fileLabel);

            _textEdit = new TextEdit
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                Editable = false,
                SyntaxHighlighter = CreateHighlighter()
            };
            AddChild(_textEdit);

            _thumbLabel = new Label { Text = "Background previews:" };
            AddChild(_thumbLabel);

            _thumbScroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0, 100),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
                VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            AddChild(_thumbScroll);

            _thumbContainer = new HBoxContainer();
            _thumbScroll.AddChild(_thumbContainer);
        }

        /// <summary>
        /// Loads the <see cref="SceneDefInfo.RawJson"/> text into the
        /// editor, disables the "Open file" button (scene JSON files
        /// are not intended for external editing), and clears any
        /// existing background thumbnails.
        /// </summary>
        public void ShowSceneJson(SceneDefInfo scene)
        {
            _textEdit.Text = scene.RawJson ?? $"{{ \"name\": \"{scene.Name}\" }}";
            _openBtn.Disabled = true;
            _fileLabel.Text = scene.FilePath;
            _currentScenario = null;
            ClearThumbnails();
        }

        /// <summary>
        /// Loads the <see cref="ScenarioDefInfo.RawText"/> into the
        /// editor, enables the "Open file" button, and populates the
        /// thumbnail strip by resolving each background reference in
        /// <see cref="ScenarioDefInfo.BackgroundRefs"/> through
        /// <see cref="Services.ResourceResolver.ResolveBackgroundFile"/>.
        /// </summary>
        public void ShowScenario(ScenarioDefInfo sc, string modId)
        {
            _textEdit.Text = sc.RawText ?? "";
            _openBtn.Disabled = false;
            _fileLabel.Text = $"{modId}/{sc.FilePath}";
            _currentScenario = sc;
            LoadThumbnails(sc.BackgroundRefs);
        }

        /// <summary>
        /// Resets the panel to its empty state: clears the text editor,
        /// disables the "Open file" button, and removes all thumbnail
        /// children.
        /// </summary>
        public void Clear()
        {
            _textEdit.Text = "";
            _openBtn.Disabled = true;
            _fileLabel.Text = "";
            _currentScenario = null;
            ClearThumbnails();
        }

        private void OnOpenPressed()
        {
            if (_currentScenario == null || string.IsNullOrEmpty(_currentScenario.AbsoluteFilePath))
                return;

            var path = _currentScenario.AbsoluteFilePath;
            if (!System.IO.File.Exists(path))
            {
                GameLogger.Log("SCENE", $"File not found: {path}", LogLevel.Error);
                return;
            }

            GD.Print($"[SceneManager] Opening: {path}");
            var err = OS.ShellOpen(path);
            if (err != Error.Ok)
                GameLogger.Log("SCENE", $"ShellOpen error: {err}", LogLevel.Error);
        }

        private void LoadThumbnails(List<string> refs)
        {
            ClearThumbnails();
            if (refs == null || refs.Count == 0) return;

            foreach (var id in refs)
            {
                var fp = ResourceResolver.ResolveBackgroundFile(id);
                if (fp == null || !System.IO.File.Exists(fp))
                {
                    _thumbContainer.AddChild(new Label
                    {
                        Text = $"[? {id}]",
                        TooltipText = $"Not found: {id}"
                    });
                    continue;
                }

                try
                {
                    var img = new Image();
                    var err = img.Load(fp);
                    if (err != Error.Ok)
                    {
                        _thumbContainer.AddChild(new Label { Text = $"[! {id}]", TooltipText = $"Load error: {err}" });
                        continue;
                    }

                    var tex = ImageTexture.CreateFromImage(img);
                    img.Dispose();
                    _thumbContainer.AddChild(new TextureRect
                    {
                        Texture = tex,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        CustomMinimumSize = new Vector2(120, 90),
                        TooltipText = id
                    });
                }
                catch (Exception ex)
                {
                    _thumbContainer.AddChild(new Label { Text = $"[! {id}]", TooltipText = ex.Message });
                }
            }
        }

        private void ClearThumbnails()
        {
            foreach (var c in _thumbContainer.GetChildren())
                c.QueueFree();
        }

        private static CodeHighlighter CreateHighlighter()
        {
            var hl = new CodeHighlighter();
            hl.AddColorRegion("#", null, new Color(0.45f, 0.75f, 0.45f), true);
            hl.AddColorRegion("\"", "\"", new Color(1f, 0.6f, 0.4f), false);
            hl.NumberColor = new Color(0.7f, 0.7f, 1f);
            hl.SymbolColor = new Color(0.8f, 0.8f, 0.8f);

            var cmdColor = new Color(0.4f, 0.7f, 1f);
            hl.KeywordColors = new Godot.Collections.Dictionary();
            foreach (var kw in new[] {
                "background", "text", "select", "option", "switch_scene", "end",
                "action", "foreground", "title", "music", "delay", "sound", "set",
                "goto", "effect", "animation", "hide_dialog", "show_dialog",
                "ptext", "empty", "prefab", "background_color", "background_show_hide",
                "light_use_time", "light_load_group", "music_pause", "music_play",
                "key", "first", "second", "hide_color", "wait", "speed", "hidden"
            }) hl.KeywordColors[kw] = cmdColor;

            return hl;
        }
    }
}
#endif
