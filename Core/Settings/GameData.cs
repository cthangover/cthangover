using Cthangover.Core.Localization;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    public partial class GameData : Node
    {
        public static GameData Instance { get; private set; }

        public GameData()
        {
            GameLogger.Init();
        }

        public override void _EnterTree()
        {
            Instance = this;
            GameLogger.Init();
        }

        public SettingsData Settings { get; private set; } = new();
        public RuntimeData Runtime { get; private set; } = new();

        public override void _Ready()
        {
            Settings.Load();
            LocaleLoader.LoadCurrentLanguage();
            GameLogger.Log("GAME", $"Game initialized, version={ProjectSettings.GetSetting("application/config/version")}");
        }
    }
}
