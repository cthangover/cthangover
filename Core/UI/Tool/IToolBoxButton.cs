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
        /// <summary>Matches an <see cref="IToolProvider.Id"/> in <see cref="ToolFactory"/>, wiring the button to a specific tool.</summary>
        string ToolId { get; }

        /// <summary>Resource path to the icon texture loaded for this button.</summary>
        string IconPath { get; }

        /// <summary>Translation key for the button's tooltip or accessible label.</summary>
        string LocaleKey { get; }

        /// <summary>Return <c>false</c> to hide the button dynamically (e.g. dev-only buttons in release builds).</summary>
        bool IsVisible();
    }
}
