using System.Threading;
using Cthangover.Core.Events;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Single "freshness flag" of scenario condition results. Subscribes to every
    /// state-change event conditions depend on (quest tags/statuses, flags, save
    /// restore, interactives, map zones) and increments the monotonic Version.
    /// Consumers (ScenarioEvent caches its condition result) treat a cached result
    /// as valid while Version stays the same. Every increment is also published as
    /// <see cref="ConditionStateChangedEvent"/>, so event chains react to changes
    /// instead of polling Version. MarkStateChanged() is the public manual
    /// invalidator for mods with custom state outside quests; Notify&lt;T&gt; is
    /// public for unit tests without Godot.
    /// Caution: ModEventBus.ClearAll() drops the subscriptions; the game never calls
    /// it, but after such a call EnsureSubscribed() must be called again.
    /// </summary>
    public static class ConditionStateMonitor
    {
        private static long _version;
        private static readonly object Lock = new();
        private static bool _subscribed;

        /// <summary>
        /// Monotonically growing counter of state changes. Every event the monitor
        /// subscribes to increments it by 1; equal values mean "the state did not
        /// change since the last read".
        /// </summary>
        public static long Version => Interlocked.Read(ref _version);

        /// <summary>
        /// Manual invalidator: forces Version to grow outside the regular events
        /// (for mods with custom state that conditions read). Publishes the new
        /// version as <see cref="ConditionStateChangedEvent"/> — event chains
        /// subscribe to it instead of polling Version.
        /// </summary>
        public static void MarkStateChanged()
        {
            var version = Interlocked.Increment(ref _version);
            ModEventBus.Publish(new ConditionStateChangedEvent { Version = version });
        }

        /// <summary>Handler of every monitored event — bumps the version.</summary>
        public static void Notify<T>(T evt) where T : ModEvent
        {
            MarkStateChanged();
        }

        /// <summary>
        /// Ensures the monitor is subscribed to every state-change event.
        /// Idempotent — repeated calls do nothing. Called from the static
        /// constructor; calling it again is only needed after
        /// <c>ModEventBus.ClearAll()</c> dropped the static subscriptions.
        /// </summary>
        public static void EnsureSubscribed()
        {
            lock (Lock)
            {
                if (_subscribed)
                    return;
                _subscribed = true;
                ModEventBus.Subscribe<QuestTagChangedEvent>(Notify);
                ModEventBus.Subscribe<QuestStatusChangedEvent>(Notify);
                ModEventBus.Subscribe<QuestDataStatusChangedEvent>(Notify);
                ModEventBus.Subscribe<FlagChangedEvent>(Notify);
                ModEventBus.Subscribe<QuestStateRestoredEvent>(Notify);
                ModEventBus.Subscribe<InteractiveStateChangedEvent>(Notify);
                ModEventBus.Subscribe<MapZoneChangedEvent>(Notify);
            }
        }

        static ConditionStateMonitor()
        {
            EnsureSubscribed();
        }
    }
}
