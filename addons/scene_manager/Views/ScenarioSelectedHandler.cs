#if TOOLS
namespace SceneManagerAddon
{
    /// <summary>
    /// Raised by <see cref="SceneTreePanel"/> when the user selects
    /// a scenario leaf in the tree. The <paramref name="scenario"/>
    /// carries all parsed metadata and references, while
    /// <paramref name="modId"/> identifies the owning mod so the
    /// handler can resolve paths and display the scenario text.
    /// </summary>
    public delegate void ScenarioSelectedHandler(ScenarioDefInfo scenario, string modId);
}
#endif
