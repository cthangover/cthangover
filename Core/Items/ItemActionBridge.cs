using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Core-side delivery bridge for item actions. The core no longer owns
    /// an <c>ItemActionFactory</c>: GDScript-declared actions (registered by
    /// <c>ModGDScriptLoader</c>) and hot-loaded mod assemblies (fanned out by
    /// <c>ModAssemblyLoader</c>) arrive here, which buffers them until the
    /// items subsystem binds its own sinks — typically from
    /// <c>IModInitializer.OnModResourcesReady</c>. After <see cref="Bind"/>
    /// registrations are delivered straight through and no longer buffered;
    /// a bind is therefore safe at any point in the boot pipeline.
    /// </summary>
    public static class ItemActionBridge
    {
        private static readonly object Gate = new();
        private static readonly List<IItemAction> PendingActions = new();
        private static readonly List<Assembly> PendingAssemblies = new();
        private static Action<IItemAction> _actionSink;
        private static Action<Assembly> _assemblySink;

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

        /// <summary>Delivers one assembly to the bound sink, or buffers it until <see cref="Bind"/>.</summary>
        public static void RegisterAssembly(Assembly assembly)
        {
            if (assembly == null)
                return;

            lock (Gate)
            {
                if (_assemblySink != null)
                    _assemblySink(assembly);
                else
                    PendingAssemblies.Add(assembly);
            }
        }

        /// <summary>
        /// Installs the sinks (typically the items mod's action factory) and
        /// flushes everything buffered so far — actions first, then
        /// assemblies, matching the delivery order of the previous core
        /// fan-out. Subsequent registrations go straight to the sinks.
        /// </summary>
        public static void Bind(Action<IItemAction> actionSink, Action<Assembly> assemblySink)
        {
            lock (Gate)
            {
                _actionSink = actionSink;
                _assemblySink = assemblySink;

                foreach (var action in PendingActions)
                    actionSink?.Invoke(action);
                PendingActions.Clear();

                foreach (var assembly in PendingAssemblies)
                    assemblySink?.Invoke(assembly);
                PendingAssemblies.Clear();
            }
        }
    }
}
