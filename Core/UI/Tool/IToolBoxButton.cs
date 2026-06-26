namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Contract for dynamically-discovered toolbar buttons. ToolId links to
    /// a ToolFactory entry; IconPath provides the icon texture; LocaleKey
    /// enables translation; IsVisible() allows buttons to conditionally appear
    /// (e.g. dev tools that only show in debug builds).
    /// </summary>
    public interface IToolBoxButton
    {
        string ToolId { get; }
        string IconPath { get; }
        string LocaleKey { get; }
        bool IsVisible();
    }
}
