using System.Collections.Generic;
using Cthangover.Core.Mods;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Kernel seam for the shell toolbar (the interface engine mod's ToolBox).
    /// UiBuilder hands the pre-sorted panel entries to the toolbar once per
    /// shell without a compile-time reference to the implementing mod type.
    /// </summary>
    public interface IToolbarHost
    {
        /// <summary>Materializes a toolbar button for every panel entry (arrived pre-sorted from the UiRegistry).</summary>
        void ApplyPanelButtons(IReadOnlyList<UiEntry> entries);
    }
}
