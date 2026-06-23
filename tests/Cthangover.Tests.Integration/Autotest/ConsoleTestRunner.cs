#if TOOLS
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    public partial class ConsoleTestRunner : Node
    {
        public override void _Ready()
        {
            var args = OS.GetCmdlineArgs();
            var testName = "";

            foreach (var arg in args)
            {
                if (arg.StartsWith("--test="))
                {
                    testName = arg.Substring("--test=".Length).ToLower();
                    break;
                }
            }


            GameLogger.Log("TEST", $"ConsoleTestRunner: test='{testName}'");

            switch (testName)
            {
                case "battle":
                    SetupBattleTest();
                    break;
                case "dialog":
                    GameLogger.Log("TEST", "Dialog test will run via Test.tscn attached ExecutableMainEventChain");
                    break;
                case "empty":
                    break;
                default:
                    GameLogger.Log("TEST", $"Unknown test '{testName}', running default scene behavior");
                    break;
            }
        }

        private void SetupBattleTest()
        {
            var runtime = GameData.Instance.Runtime;
            runtime.CharacterData.AddCharacterToParty(CharacterType.Murakami);

            var background = ResourceLoader.Load<Texture2D>("res://Resources/backgrounds/00006-4133256041.png");
            var data = BattleData.InitBattle(background, "MainMenu", "wolf_1", "wolf_2", "werewolf_girl_1");
            runtime.BattleData = data;

            var sceneService = GetNodeOrNull<GodotSceneService>("/root/GodotSceneService");
            sceneService?.SwitchToBattle();
        }
    }
}
#endif
