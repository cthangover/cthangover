#if TOOLS
namespace SceneManagerAddon
{
    /// <summary>
    /// Raised by <see cref="SceneTreePanel"/> when the user selects a
    /// scene node in the tree. The handler receives the full
    /// <see cref="SceneDefInfo"/> object so it can display the raw JSON
    /// and any associated metadata.
    /// </summary>
    public delegate void SceneSelectedHandler(SceneDefInfo scene);
}
#endif
