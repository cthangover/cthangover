#if TOOLS
using Cthangover.Core.Characters;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.Battle
{
    public partial class BattleTestSetup : Node
    {
        public override void _Ready()
        {
            var runtime = GameData.Instance.Runtime;

            runtime.CharacterData.AddCharacterToParty(CharacterType.Murakami);

            var background = ResourceLoader.Load<Texture2D>("res://Resources/backgrounds/00006-4133256041.png");
            var data = BattleData.InitBattle(background, "MainMenu", "wolf_1", "wolf_2", "werewolf_girl_1");
            runtime.BattleData = data;
        }
    }
}
#endif
