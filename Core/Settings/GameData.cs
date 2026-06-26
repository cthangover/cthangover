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
			if (Instance != null && GodotObject.IsInstanceValid(Instance))
			{
				var scene = GetTree()?.CurrentScene?.Name ?? "?";
				var existingPath = Instance.GetPath().ToString();
				var myPath = GetPath().ToString();
				GameLogger.Log("DUPLICATE", $"GameData._EnterTree: Instance ALREADY SET by '{existingPath}', overwriting with duplicate at '{myPath}' on scene '{scene}'", LogLevel.Error);
			}
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
