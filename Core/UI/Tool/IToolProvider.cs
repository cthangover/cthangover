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
        string Id { get; }
        string LocaleKey { get; }
        Window CreateWindow();
    }
}
