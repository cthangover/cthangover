using Godot;

namespace Cthangover.Core.UI.Tool
{
    public interface IToolProvider
    {
        string Id { get; }
        string LocaleKey { get; }
        Window CreateWindow();
    }
}
