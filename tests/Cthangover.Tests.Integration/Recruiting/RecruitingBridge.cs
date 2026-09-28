#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Reflection bridge between the core integration tests and the recruiting
    /// mod. The roster types live in the <c>recruiting</c> mod assembly, which the
    /// core test project cannot reference at compile time — so the persistence
    /// test manipulates the roster through this helper. Keeping the reflection in
    /// one place keeps the test itself readable.
    /// </summary>
    internal static class RecruitingBridge
    {
        private const string ModAssemblyName = "recruiting";
        private const string RosterTypeName = "Cthangover.Core.Relationship.RecruitingData";
        private const string RecruitTypeName = "Cthangover.Core.Relationship.Recruit";

        /// <summary>True when the recruiting mod assembly is loaded.</summary>
        public static bool IsLoaded => FindAssembly() != null;

        private static Assembly FindAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == ModAssemblyName);
        }

        private static object GetRoster()
        {
            var type = FindAssembly()?.GetType(RosterTypeName);
            return type?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        /// <summary>Number of entries in the session roster, or -1 when the mod is not loaded.</summary>
        public static int Count()
        {
            var roster = GetRoster();
            var data = roster?.GetType().GetProperty("Data")?.GetValue(roster) as System.Collections.IList;
            return data?.Count ?? -1;
        }

        /// <summary>Adds a recruit through the production factory method.</summary>
        public static void Add(string id, string characterId)
        {
            var roster = GetRoster();
            roster?.GetType().GetMethod("Add")?.Invoke(roster, new object[] { id, characterId });
        }

        /// <summary>Character IDs of all current recruits, in roster order.</summary>
        public static List<string> CharacterIds()
        {
            var result = new List<string>();
            var roster = GetRoster();
            if (roster?.GetType().GetProperty("Data")?.GetValue(roster) is not System.Collections.IEnumerable data)
                return result;

            var characterIdProperty = FindAssembly()?.GetType(RecruitTypeName)?.GetProperty("CharacterID");
            foreach (var recruit in data)
            {
                if (recruit != null && characterIdProperty != null)
                    result.Add(characterIdProperty.GetValue(recruit) as string);
            }

            return result;
        }
    }
}
#endif
