namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Godot;

    /// <summary>
    /// Key/value row of the "State" tab. Editable values (dialog variables,
    /// item counts, quest status) are written back when the field is submitted
    /// with Enter; the rest is shown read-only.
    /// </summary>
    public partial class StudioStateRow : HBoxContainer
    {
        private const int KeyWidth = 170;

        private readonly StudioStateEntry _entry;

        /// <summary>Raised with the entry and the submitted text.</summary>
        public event Action<StudioStateEntry, string> Submitted;

        public StudioStateRow(StudioStateEntry entry)
        {
            _entry = entry;
            MouseFilter = MouseFilterEnum.Ignore;
            AddThemeConstantOverride("separation", UiTokens.Space3);

            var key = StudioStyles.Text(entry.Key.Resolve(), UiTokens.FontBodySmall, UiTokens.ChipKey, mono: true);
            key.CustomMinimumSize = new Vector2(KeyWidth, 0);
            key.ClipText = true;
            key.VerticalAlignment = VerticalAlignment.Center;
            AddChild(key);

            var value = new LineEdit
            {
                Text = entry.Value.Resolve(),
                Editable = entry.Editor != StudioStateEditor.None,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            value.AddThemeFontOverride("font", StudioStyles.Mono);
            value.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            value.TextSubmitted += OnSubmitted;
            AddChild(value);
        }

        private void OnSubmitted(string text) => Submitted?.Invoke(_entry, text);
    }
}
