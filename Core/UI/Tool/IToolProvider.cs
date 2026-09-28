using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Contract for discoverable developer/modding tools. CreateWindow() returns
    /// a Godot Window that the tool system manages as a popup. Id is the registry
    /// key; LocaleKey provides localized display names. AddMenuButton() lets the
    /// tool append its own launcher button to the main menu's tool block, so the
    /// menu stays unaware of concrete tools.
    /// </summary>
    public interface IToolProvider
    {
        /// <summary>Unique identifier used as the lookup key in <see cref="ToolFactory"/>.</summary>
        string Id { get; }

        /// <summary>Translation key for the tool's display name, resolved via <c>TranslationServer.Translate</c>.</summary>
        string LocaleKey { get; }

        /// <summary>
        /// Creates a new <see cref="Window"/> instance for this tool. Callers are responsible
        /// for adding it to the scene tree and displaying it.
        /// </summary>
        Window CreateWindow();

        /// <summary>
        /// Appends this tool's launcher button to the main menu's tool block. Called once
        /// per main-menu instantiation, so each call must add a fresh button to
        /// <paramref name="container"/>. Use <see cref="ToolMenuButton.Attach"/> for the
        /// standard presentation; it also marks the button so the menu retranslates it
        /// together with its built-in buttons.
        /// </summary>
        void AddMenuButton(Container container);
    }
}
