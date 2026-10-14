namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published on the mod event bus once every mod has been loaded and all
    /// resources are ready — the "boot finished" signal for mods that must
    /// wait for the full content set (e.g. collections or cross-mod wiring)
    /// before acting. Carries no payload.
    /// </summary>
    [EventKey("mod_resources_ready")]
    public class ModResourcesReadyEvent : ModEvent { }
}
