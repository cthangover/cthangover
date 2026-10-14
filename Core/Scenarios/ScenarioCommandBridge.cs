using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Core-side delivery bridge for scenario command strategies. The strategy
    /// factory itself (and the strategies) live in the dialog_base engine mod;
    /// hot-loaded mod assemblies (fanned out by <c>ModAssemblyLoader</c>)
    /// arrive here, which buffers them until the dialog subsystem binds its
    /// sink — typically from <c>IModInitializer.OnModResourcesReady</c>. After
    /// <see cref="Bind"/> registrations are delivered straight through and no
    /// longer buffered; a bind is therefore safe at any point in the boot
    /// pipeline.
    /// </summary>
    public static class ScenarioCommandBridge
    {
        private static readonly object Gate = new();
        private static readonly List<Assembly> PendingAssemblies = new();
        private static Action<Assembly> _assemblySink;

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
        /// Installs the sink (the dialog mod's strategy factory) and flushes
        /// everything buffered so far. Subsequent registrations go straight
        /// to the sink.
        /// </summary>
        public static void Bind(Action<Assembly> assemblySink)
        {
            lock (Gate)
            {
                _assemblySink = assemblySink;

                foreach (var assembly in PendingAssemblies)
                    assemblySink?.Invoke(assembly);
                PendingAssemblies.Clear();
            }
        }
    }
}