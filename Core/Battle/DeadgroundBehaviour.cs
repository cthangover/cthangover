using Cthangover.Core.Audio;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Battle
{
    public partial class DeadgroundBehaviour : TransitionWidget
    {
        public void ToMainMenuClick()
        {
            var sceneService = GetNode<GodotSceneService>("/root/GodotSceneService");
            sceneService?.SwitchToMenu();
            GameData.Instance.Runtime.BattleData = null;
        }

        public override void Show()
        {
            GameLogger.Log("BATTLE", $"Deadground.Show() ENTER: Visible={Visible}, GodotVisible={base.Visible}", LogLevel.Debug);

            var audioService = GetNode<AudioService>("/root/AudioService");
            audioService?.PlaySound("battle/deadground", SoundType.UI);

            var bg = GetNodeOrNull<TextureRect>("TransitionBg");

            GameLogger.Log("BATTLE", $"Deadground.Show: TransitionBg={(bg != null ? "found" : "NULL")}, texture={(bg?.Texture != null ? "has" : "null")}", LogLevel.Error);

            if (bg != null && bg.Texture == null)
            {
                var tex = ModManager.Instance.ResolveTexture("deadground");
                GameLogger.Log("BATTLE", $"Deadground.Show: ResolveTexture('deadground') = {(tex != null ? "loaded" : "NULL")}", LogLevel.Error);
                bg.Texture = tex;
            }

			base.Show();

            GameLogger.Log("BATTLE", $"Deadground.Show() EXIT: Visible={Visible}, GodotVisible={base.Visible}", LogLevel.Debug);
        }
    }
}
