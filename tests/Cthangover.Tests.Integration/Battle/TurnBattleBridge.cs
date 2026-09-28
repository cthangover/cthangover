#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Reflection bridge between the core integration tests and the turn-battle
    /// mod. The battle platform (BattleData, BattleCoreRegistry, BattleRequest,
    /// BattleSceneContext, IBattleAutoPlayable) now lives in the
    /// <c>turn_battle_base</c> mod assembly, which the core test project cannot
    /// reference at compile time — so every access goes through reflection here.
    /// Keeping the reflection in one place lets the test setup and driver read
    /// almost like plain code and keeps the odd member names out of the tests.
    /// </summary>
    internal static class TurnBattleBridge
    {
        /// <summary>Assembly name of the mod that owns the battle platform (matches its manifest id).</summary>
        private const string BattleAssemblyName = "turn_battle_base";

        private static Assembly FindBattleAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == BattleAssemblyName);
        }

        private static Type FindType(string fullName)
        {
            return FindBattleAssembly()?.GetType(fullName);
        }

        private static object GetRegistryInstance()
        {
            var type = FindType("Cthangover.Core.Battle.BattleCoreRegistry");
            return type?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        /// <summary>Checks whether a battle core id was registered by the loaded mods.</summary>
        public static bool HasCore(string coreId)
        {
            var instance = GetRegistryInstance();
            if (instance == null)
            {
                GameLogger.Log("BATTLE_TEST",
                    $"TurnBattleBridge.HasCore('{coreId}'): registry instance not found, assembly={FindBattleAssembly()?.GetName().Name ?? "NULL"}",
                    LogLevel.Warning);
                return false;
            }

            return (bool?)instance.GetType().GetMethod("HasCore")?.Invoke(instance, new object[] { coreId }) ?? false;
        }

        /// <summary>Selects the active battle core by id (throws when unknown, same as production).</summary>
        public static void SetActiveCore(string coreId)
        {
            var instance = GetRegistryInstance();
            instance?.GetType().GetMethod("SetActive")?.Invoke(instance, new object[] { coreId });
        }

        /// <summary>Registered core ids, sorted, for the coverage check.</summary>
        public static IReadOnlyList<string> CoreIds()
        {
            var instance = GetRegistryInstance();
            var ids = instance?.GetType().GetProperty("CoreIds")?.GetValue(instance) as IReadOnlyList<string>;
            return ids ?? Array.Empty<string>();
        }

        /// <summary>Registered <see cref="Type"/> of a core so tooling can check its interfaces.</summary>
        public static Type GetCoreType(string coreId)
        {
            var instance = GetRegistryInstance();
            return instance?.GetType().GetMethod("GetCoreType")?.Invoke(instance, new object[] { coreId }) as Type;
        }

        /// <summary>The mod's IBattleAutoPlayable contract type, or null when the mod is not loaded.</summary>
        public static Type AutoPlayableType()
        {
            return FindType("Cthangover.Core.Battle.IBattleAutoPlayable");
        }

        /// <summary>
        /// Builds a pending encounter exactly like BattleService does, but with a
        /// caller-supplied background so the test does not depend on scene state.
        /// Returns the mod's BattleData instance (typed as object).
        /// </summary>
        public static object CreateBattleData(Texture2D background, string returnScene, string coreId, params string[] enemies)
        {
            var dataType = FindType("Cthangover.Core.Settings.BattleData");
            var init = dataType?.GetMethod("InitBattle", new[] { typeof(Texture2D), typeof(string), typeof(string[]) });
            var data = init?.Invoke(null, new object[] { background, returnScene, enemies });
            if (data == null)
            {
                GameLogger.Log("BATTLE_TEST",
                    $"TurnBattleBridge: failed to create BattleData via reflection (asm={FindBattleAssembly()?.GetName().Name ?? "NULL"}, dataType={dataType != null}, init={init != null})",
                    LogLevel.Error);
                return null;
            }

            dataType.GetProperty("ActiveBattleCore")?.SetValue(data, coreId);
            return data;
        }

        /// <summary>Stores a prepared encounter in the mod's static BattleRequest slot.</summary>
        public static void SetPendingBattle(object data)
        {
            var requestType = FindType("Cthangover.Core.Battle.BattleRequest");
            requestType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, data);
        }

        /// <summary>The active <see cref="BattleSceneContext"/> node, or null before it enters the tree.</summary>
        public static Node GetSceneContext()
        {
            var type = FindType("Cthangover.Core.Battle.BattleSceneContext");
            return type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Node;
        }

        /// <summary>True once the battle scene flushed victory or defeat state.</summary>
        public static bool IsDestroyed(Node battle)
        {
            return (bool?)battle?.GetType().GetProperty("IsDestroyed")?.GetValue(battle) ?? false;
        }

        /// <summary>The live battle core instance driving the scene.</summary>
        public static object GetActiveCore(Node battle)
        {
            return battle?.GetType().GetProperty("ActiveBattleCore")?.GetValue(battle);
        }
    }
}
#endif
