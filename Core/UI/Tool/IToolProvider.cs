using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Contract for discoverable developer/modding tools. CreateWindow() returns
    /// a Godot Window that the tool system manages as a popup. Id is the registry
    /// key; LocaleKey provides localized display names.
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
    }
}
