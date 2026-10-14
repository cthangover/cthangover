namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires on each timer tick (~1 second intervals) from SceneEventController.
    /// Named IOnTimeEvent in code despite the file name being IOnTimerEvent.
    /// Used by systems that need periodic updates slower than per-frame.
    /// <para/>
    /// <b>Boot-window contract (do not skip reading this):</b> the controller
    /// ticks from the very first frames of the app — <c>ProcessMode.Always</c>,
    /// <c>IsActive = true</c> by default — including the launcher boot window,
    /// while mods are still compiling in the background. <see cref="OnTimerTick"/>
    /// implementations therefore must NOT touch <see cref="Settings.GameData.Instance.Runtime"/>,
    /// <see cref="Mods.ModRegistry"/>, <see cref="Mods.ModManager"/> or any factory
    /// until <see cref="Bootstrap.Boot.ModsReady"/>: an early Runtime access creates
    /// <see cref="Settings.RuntimeData"/> before the starter inventory can be seeded
    /// through loaded mod factories (it stays empty for the whole session) and spams
    /// "Initialize refused" errors. Guard with
    /// <c>if (!Bootstrap.Boot.ModsReady) return;</c> — see
    /// <c>TimeTickController.OnTimerTick</c> in the time_base engine mod for the
    /// reference implementation and RecruitTickController in the recruiting mod for a mod-side one.
    /// </summary>
    public interface IOnTimeEvent : IEventPriority
    {
        /// <summary>Called every ~1s from the first frames of the app, boot window included; observe the boot-window contract documented on this interface.</summary>
        void OnTimerTick();
    }

}
