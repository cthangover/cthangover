#if TOOLS
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Manual launch harness for the card battle: builds a normal (non-autoplay)
    /// encounter with the party [Marao, Murakami] against three wolves and opens
    /// the battle scene, so the interface can be played by hand. Unlike
    /// <c>BattleTestSetup</c> it attaches no driver, so nothing clicks or ends
    /// turns for the player. The encounter is prepared through
    /// <see cref="TurnBattleBridge"/> because its types live in the battle mod.
    /// </summary>
    public partial class ManualCardBattleSetup : Node
    {
        public override void _Ready()
        {
            if (!ModManager.Instance.IsInitialized)
                ModManager.Instance.Initialize();

            var runtime = GameData.Instance.Runtime;
            runtime.CharacterData.AddCharacterToParty("Marao");
            runtime.CharacterData.AddCharacterToParty("Murakami");

            var data = TurnBattleBridge.CreateBattleData(
                Textures.Resolve("empty"), "MainMenu", "card_battle", "wolf_1", "wolf_2", "wolf_3");
            TurnBattleBridge.SetPendingBattle(data);

            GameLogger.Log("MANUAL_BATTLE",
                "party=[Marao, Murakami], enemies=[wolf_1, wolf_2, wolf_3], core=card_battle");

            var battleScene = TscnScenes.LoadAndBuild("scenes/battle/battle.tscn");
            if (battleScene == null)
            {
                GameLogger.Log("MANUAL_BATTLE", "failed to build battle scene", LogLevel.Error);
                GetTree().Quit(1);
                return;
            }
            GetParent().CallDeferred(Node.MethodName.AddChild, battleScene);
        }
    }
}
#endif
