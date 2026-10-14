#if TOOLS
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Prepares a deterministic battle encounter for <c>BattleTestScene</c>.
    /// The node is placed as the first sibling before the instantiated
    /// <c>battle.tscn</c>, so Godot runs its <c>_Ready</c> before the battle
    /// scene reads the pending encounter and the battle core registry. Mods are
    /// guaranteed to be loaded before any scene _Ready by the boot pipeline
    /// (SceneManager autoload runs Boot.RunSync). No starter-party initializer
    /// runs in this direct scene launch, so the party consists of exactly the
    /// characters added here.
    /// <para/>
    /// The encounter lives in the turn-battle mod assembly, so the bridge prepares
    /// it through reflection: build BattleData with a caller-supplied background,
    /// select the active core, and store it in the mod's BattleRequest slot.
    /// </summary>
    public partial class BattleTestSetup : Node
    {
        /// <summary>
        /// Battle speed applied to the run. The battle speed control restores
        /// <see cref="Settings.SettingsData.BattleSpeed"/> into <c>Engine.TimeScale</c>
        /// while the battle scene is being readied (after this node), so writing
        /// the setting here is the supported way to accelerate the whole test:
        /// battle timers and action animations both scale from <c>Engine.TimeScale</c>.
        /// The value is intentionally not persisted to disk.
        /// </summary>
        public const int MaxBattleSpeed = 8;
        public const string DefaultBattleCore = "card_battle";

        private const string PartyMember = "Murakami";
        private const string ReturnScene = "MainMenu";

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.

            GameData.Instance.Settings.BattleSpeed = MaxBattleSpeed;

            var core = ParseBattleCore();
            if (!TurnBattleBridge.HasCore(core))
                GameLogger.Log("BATTLE_TEST", $"BattleTestSetup: battle core '{core}' is not registered, scene will fall back to the first core", LogLevel.Warning);

            var runtime = GameData.Instance.Runtime;
            runtime.CharacterData.AddCharacterToParty(PartyMember);

            var data = TurnBattleBridge.CreateBattleData(
                Textures.Resolve("empty"), ReturnScene, core, "wolf_1", "wolf_2", "werewolf_girl_1");
            TurnBattleBridge.SetPendingBattle(data);

            GameLogger.Log("BATTLE_TEST", $"BattleTestSetup: core={core}, party=[{PartyMember}], enemies=3, speed=x{MaxBattleSpeed}");

            var battleScene = TscnScenes.LoadAndBuild("scenes/battle/battle.tscn");
            if (battleScene == null)
            {
                GameLogger.Log("BATTLE_TEST", "BattleTestSetup: failed to build mod battle scene 'scenes/battle/battle.tscn'", LogLevel.Error);
                GetTree().Quit(1);
                return;
            }

            // _Ready runs while the test root is still setting up its children,
            // so a direct AddChild is rejected; defer until the tree is free.
            GetParent().CallDeferred(Node.MethodName.AddChild, battleScene);
            GameLogger.Log("BATTLE_TEST", $"BattleTestSetup: battle scene built, deferred attach ({battleScene.GetType().Name})");
        }

        /// <summary>
        /// Reads <c>--core=</c> so one test scene covers both shipped battle
        /// cores: card_battle (default) and ff_battle. The value is passed to the
        /// mod's BattleData.ActiveBattleCore, which the battle scene resolves
        /// through the core registry before Init.
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
