#if TOOLS
namespace SceneManagerAddon
{
    /// <summary>
    /// Raised by <see cref="GraphView"/> when the user clicks a
    /// scenario hyperlink in the rich-text info panel. The three
    /// arguments together uniquely identify a scenario —
    /// <paramref name="modId"/> is the owning mod's directory,
    /// <paramref name="sceneName"/> is the target scene, and
    /// <paramref name="scName"/> is the scenario file name.
    /// </summary>
    public delegate void ScenarioLinkHandler(string modId, string sceneName, string scName);
}
#endif
