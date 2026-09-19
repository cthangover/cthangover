#if TOOLS
using Cthangover.Core.Battle;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Prepares a deterministic battle encounter for <c>BattleTestScene</c>.
    /// The node is placed as the first sibling before the instantiated
    /// <c>Battle.tscn</c>, so Godot runs its <c>_Ready</c> before the battle
    /// scene reads <see cref="RuntimeData.BattleData"/> and the battle core
    /// registry. Mods are initialized synchronously first — otherwise
    /// CharacterFactory and BattleCoreRegistry stay empty and the battle
    /// scene logs "No battle core available". No starter-party initializer
    /// runs in this direct scene launch, so the party consists of exactly
    /// the characters added here.
    /// </summary>
    public partial class BattleTestSetup : Node
    {
        /// <summary>
        /// Battle speed applied to the run. <c>BattleSpeedControl._Ready</c>
        /// restores <see cref="SettingsData.BattleSpeed"/> into
        /// <c>Engine.TimeScale</c> while the battle scene is being readied
        /// (after this node), so writing the setting here is the supported
        /// way to accelerate the whole test: battle timers and action
        /// animations both scale from <c>Engine.TimeScale</c>. The value is
        /// intentionally not persisted to disk.
        /// </summary>
        public const int MaxBattleSpeed = 8;
        public const string DefaultBattleCore = "card_battle";

        private const string PartyMember = "Murakami";
        private const string ReturnScene = "MainMenu";

        public override void _Ready()
        {
            if (!ModManager.Instance.IsInitialized)
                ModManager.Instance.Initialize();

            GameData.Instance.Settings.BattleSpeed = MaxBattleSpeed;

            var core = ParseBattleCore();
            if (!BattleCoreRegistry.Instance.HasCore(core))
                GameLogger.Log("BATTLE_TEST", $"BattleTestSetup: battle core '{core}' is not registered, scene will fall back to the first core", LogLevel.Warning);

            var runtime = GameData.Instance.Runtime;
            runtime.CharacterData.AddCharacterToParty(PartyMember);

            var data = BattleData.InitBattle(Textures.Resolve("empty"), ReturnScene, "wolf_1", "wolf_2", "werewolf_girl_1");
            data.ActiveBattleCore = core;
            runtime.BattleData = data;

            GameLogger.Log("BATTLE_TEST", $"BattleTestSetup: core={core}, party=[{PartyMember}], enemies={data.EnemiesCards.Count}, speed=x{MaxBattleSpeed}");
        }

        /// <summary>
        /// Reads <c>--core=</c> so one test scene covers both shipped battle
        /// cores: card_battle (default) and ff_battle. The value is passed to
        /// <see cref="BattleData.ActiveBattleCore"/>, which the battle scene
        /// resolves through <see cref="BattleCoreRegistry"/> before Init.
        /// </summary>
        private static string ParseBattleCore()
        {
            foreach (var arg in OS.GetCmdlineArgs())
            {
                if (arg.StartsWith("--core="))
                    return arg.Substring("--core=".Length).Trim();
            }
            return DefaultBattleCore;
        }
    }
}
#endif
