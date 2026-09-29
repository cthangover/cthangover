#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

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
        private const string PartyBridgeTypeName = "Cthangover.Core.Relationship.RecruitPartyBridge";

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

        private static object GetRecruit(string id)
        {
            var roster = GetRoster();
            return roster?.GetType().GetMethod("GetByID")?.Invoke(roster, new object[] { id });
        }

        private static Type GetPartyBridgeType()
        {
            return FindAssembly()?.GetType(PartyBridgeTypeName);
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

        /// <summary>Removes a recruit from the roster by instance id.</summary>
        public static void Remove(string id)
        {
            var roster = GetRoster();
            roster?.GetType().GetMethod("Remove")?.Invoke(roster, new object[] { id });
        }

        /// <summary>True when an enemy of this template was already captured.</summary>
        public static bool HasCharacterId(string characterId)
        {
            var roster = GetRoster();
            var result = roster?.GetType().GetMethod("HasCharacterID")?.Invoke(roster, new object[] { characterId });
            return result is true;
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

        /// <summary>Adds the recruit to the core battle party through the mod bridge.</summary>
        public static void AddToSquad(string id)
        {
            InvokeParty("AddToSquad", id);
        }

        /// <summary>Removes the recruit from the core battle party through the mod bridge.</summary>
        public static void RemoveFromSquad(string id)
        {
            InvokeParty("RemoveFromSquad", id);
        }

        /// <summary>Pushes the recruit's own health onto its core placeholder card.</summary>
        public static void SyncToCard(string id)
        {
            InvokeParty("SyncToCard", id);
        }

        /// <summary>True when the recruit currently occupies a slot in the active party.</summary>
        public static bool IsInSquad(string id)
        {
            var bridgeType = GetPartyBridgeType();
            var result = bridgeType?.GetMethod("IsInSquad")?.Invoke(null, new[] { GetRecruit(id) });
            return result is true;
        }

        /// <summary>Writes a health value into the recruit's property bag.</summary>
        public static void SetHealth(string id, int health)
        {
            var recruit = GetRecruit(id);
            if (recruit?.GetType().GetProperty("Properties")?.GetValue(recruit) is PropertyData properties)
                properties.SetInt("Health", health);
        }

        /// <summary>Reads the recruit's own health, or -1 when the recruit is missing.</summary>
        public static int GetHealth(string id)
        {
            var recruit = GetRecruit(id);
            if (recruit?.GetType().GetProperty("Properties")?.GetValue(recruit) is PropertyData properties)
                return properties.GetInt("Health");
            return -1;
        }

        private static void InvokeParty(string method, string id)
        {
            var recruit = GetRecruit(id);
            if (recruit == null)
                return;
            GetPartyBridgeType()?.GetMethod(method)?.Invoke(null, new[] { recruit });
        }
    }
}
#endif
