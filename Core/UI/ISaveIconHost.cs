namespace Cthangover.Core.UI
{
    /// <summary>
    /// Kernel seam for the shell's save indicator (the interface engine mod's
    /// ToolBox/SaveIcon pair). SceneManager refreshes the indicator after a
    /// scene's event set changes; the kernel never references the implementing
    /// mod type.
    /// </summary>
    public interface ISaveIconHost
    {
        /// <summary>Re-evaluates the save-allowed state of the current scene and updates the indicator.</summary>
        void UpdateSaveIconVisibility();
    }
}
