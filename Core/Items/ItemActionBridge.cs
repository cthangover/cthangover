using System;
using System.Collections.Generic;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Core-side delivery bridge for GDScript-declared item actions
    /// (registered by <c>ModGDScriptLoader</c>, which runs before the items
    /// mod is ready). They are buffered until the items subsystem binds its
    /// sink — typically from <c>IModInitializer.OnModResourcesReady</c>; after
    /// <see cref="Bind"/> registrations are delivered straight through, so a
    /// bind is safe at any point in the boot pipeline. C# actions do not pass
    /// here: the items mod collects them from mod assemblies with its own
    /// assembly consumer.
    /// </summary>
    public static class ItemActionBridge
    {
        private static readonly object Gate = new();
        private static readonly List<IItemAction> PendingActions = new();
        private static Action<IItemAction> _actionSink;

        /// <summary>Delivers one action to the bound sink, or buffers it until <see cref="Bind"/>.</summary>
        public static void Register(IItemAction action)
        {
            if (action == null)
                return;

            lock (Gate)
            {
                if (_actionSink != null)
                    _actionSink(action);
                else
                    PendingActions.Add(action);
            }
        }

        /// <summary>
        /// Installs the sink (typically the items mod's action factory) and
        /// flushes everything buffered so far. Subsequent registrations go
        /// straight to the sink.
        /// </summary>
        public static void Bind(Action<IItemAction> actionSink)
        {
            lock (Gate)
            {
                _actionSink = actionSink;

                foreach (var action in PendingActions)
                    actionSink?.Invoke(action);
                PendingActions.Clear();
            }
        }
    }
}
