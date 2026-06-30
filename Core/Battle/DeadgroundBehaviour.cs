using Cthangover.Core.Audio;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Defeat screen widget. Minimal compared to WingroundBehaviour —
    /// only sets the deadground background texture (resolved from mods)
    /// and offers a Main Menu button that clears BattleData and reloads
    /// the menu scene. No loot, no EXP.
    /// </summary>
    public partial class DeadgroundBehaviour : TransitionWidget
    {
        /// <summary>
        /// Clears battle data and switches to the main menu scene.
        /// Bound to the "Main Menu" button on the defeat screen.
        /// </summary>
        public void ToMainMenuClick()
        {
            var sceneService = GetNode<GodotSceneService>("/root/GodotSceneService");
            sceneService?.SwitchToMenu();
            GameData.Instance.Runtime.BattleData = null;
        }

        /// <summary>
        /// Plays the defeat sound, resolves the deadground background
        /// texture from mods, and shows the transition widget.
        /// </summary>
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
