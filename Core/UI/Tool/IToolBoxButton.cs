namespace Cthangover.Core.UI.Tool
{
    public interface IToolBoxButton
    {
        string ToolId { get; }
        string IconPath { get; }
        string LocaleKey { get; }
        bool IsVisible();
    }
}
