using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Shared factory for the launcher buttons that tools contribute to the main
    /// menu's tool block. Keeps the standard presentation (localized caption),
    /// the translation-key metadata used for live language switching, and the
    /// window-opening action in one place, so <see cref="IToolProvider.AddMenuButton"/>
    /// implementations stay focused while remaining free to append extra controls.
    /// </summary>
    public static class ToolMenuButton
    {
        /// <summary>
        /// Creates a button labelled with the tool's <see cref="IToolProvider.LocaleKey"/>,
        /// wires it to open a fresh window via <see cref="ToolWindow.ShowWindow"/>, and adds
        /// it to <paramref name="container"/>. Returns the created button so a tool can
        /// decorate it further (icon, tooltip, extra handlers).
        /// </summary>
        public static Button Attach(Container container, IToolProvider tool)
        {
            var button = new Button
            {
                Text = TranslationServer.Translate(tool.LocaleKey),
            };
            button.SetMeta(Menu.MainMenuBuilder.LocaleMetaKey, tool.LocaleKey);
            button.Pressed += () => ToolWindow.ShowWindow(tool.CreateWindow());
            container.AddChild(button);
            return button;
        }
    }
}
