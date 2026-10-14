namespace Cthangover.Core.Bootstrap
{
    /// <summary>
    /// A state owner that can return to its fresh "new game" state. Registered
    /// in <see cref="AppServices"/> (core owners via the explicit list there,
    /// mod owners through <see cref="AppServices.RegisterResettable"/> from an
    /// <c>IModInitializer</c>), so <c>GameData.StartNewGame</c> resets
    /// everything through one loop instead of hand-maintained calls and
    /// scattered <c>NewGameStartedEvent</c> subscriptions.
    /// </summary>
    public interface IResettable
    {
        /// <summary>Stable owner name for logs.</summary>
        string Id { get; }

        /// <summary>
        /// Discards playthrough state so the next session starts fresh.
        /// Must not publish <see cref="Cthangover.Core.Events.NewGameStartedEvent"/>
        /// itself — the publisher is <c>GameData.StartNewGame</c>.
        /// </summary>
        void ResetForNewGame();
    }
}
