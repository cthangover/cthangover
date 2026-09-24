namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published by <see cref="Settings.GameData.StartNewGame"/> right after the
    /// runtime state is wiped for a "New Game". Mods use it to re-seed per-session
    /// data — the core mod's starter party, for example — because
    /// <c>IModInitializer.OnModResourcesReady</c> runs only once at boot and cannot
    /// restore state that <c>ResetRuntime</c> has just discarded.
    /// </summary>
    [EventKey("new_game_started")]
    public class NewGameStartedEvent : ModEvent
    {
    }
}
