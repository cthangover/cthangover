#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Reflection bridge between <see cref="SubsystemsCoverageTestSetup"/> and
    /// the engine mods. Migrated subsystems live in mod assemblies that the
    /// core test code cannot reference at compile time, so every access goes
    /// through reflection here (same pattern as <c>TurnBattleBridge</c>).
    /// <para/>
    /// Registration contract for a migrated subsystem: its mod ships exactly
    /// one public static class named <c>SubsystemRegistration</c> exposing the
    /// subsystem id either as a static property or a public static field:
    /// <code>
    /// public static class SubsystemRegistration
    /// {
    ///     public static string SubsystemId => "audio";
    /// }
    /// </code>
    /// The registry/marker is the source of truth: a registration found in the
    /// loaded mod set declares the subsystem migrated, and the coverage test
    /// then requires a declared integration test for it.
    /// </summary>
    internal static class SubsystemsBridge
    {
        /// <summary>Exact simple name of the marker class every migrated subsystem ships.</summary>
        private const string RegistrationTypeName = "SubsystemRegistration";

        /// <summary>
        /// All <c>SubsystemRegistration</c> types found in the currently loaded
        /// assemblies. A registration with an unreadable <c>SubsystemId</c> is
        /// returned with a null id so the caller can fail with a clear message.
        /// </summary>
        public static List<Registration> FindRegistrations()
        {
            var registrations = new List<Registration>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                Type registrationType;
                try
                {
                    registrationType = assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == RegistrationTypeName);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("SUBSYSTEM_COVERAGE",
                        "SubsystemsBridge: failed to scan assembly '" + assembly.GetName().Name + "': " + ex.Message,
                        LogLevel.Warning);
                    continue;
                }

                if (registrationType == null)
                    continue;

                registrations.Add(new Registration
                {
                    AssemblyName = assembly.GetName().Name,
                    SubsystemId = ReadSubsystemId(registrationType),
                });
            }

            return registrations
                .OrderBy(r => r.SubsystemId, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Reads the SubsystemId member (static property or public static field), null when missing.</summary>
        private static string ReadSubsystemId(Type registrationType)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            var value = registrationType.GetProperty("SubsystemId", flags)?.GetValue(null)
                        ?? registrationType.GetField("SubsystemId", flags)?.GetValue(null);
            return value as string;
        }

        /// <summary>One discovered <c>SubsystemRegistration</c> marker in a mod assembly.</summary>
        public sealed class Registration
        {
            /// <summary>Assembly that ships the marker (diagnostics only).</summary>
            public string AssemblyName;

            /// <summary>Subsystem id, e.g. "audio". Null when the marker is broken.</summary>
            public string SubsystemId;
        }
    }
}
#endif
