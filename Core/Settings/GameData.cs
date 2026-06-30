using Cthangover.Core.Localization;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Unity-style Godot autoload that serves as the single entry point
    /// for the game state subsystem. Holds the <see cref="SettingsData"/>
    /// (persisted user preferences) and <see cref="RuntimeData"/> (session
    /// state). On <c>_Ready</c> it loads saved settings, initialises the
    /// locale, and plugs the <see cref="TimeTickController"/> into the
    /// global event system so that in-game time advances each tick.
    /// </summary>
    public partial class GameData : Node
    {
        /// <summary>Global singleton, set once during <c>_EnterTree</c>.
        /// Duplicate instances log an error and overwrite, guaranteeing
        /// the last-loaded scene always owns the reference.</summary>
        public static GameData Instance { get; private set; }

        public GameData()
        {
            GameLogger.Init();
        }

        /// <summary>
        /// Registers this node as the global <see cref="Instance"/>.
        /// If a previous valid instance still exists (e.g. scene reload
        /// without proper cleanup), logs a diagnostic warning before
        /// overwriting.
        /// </summary>
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

        /// <summary>Drives in-game time by listening to timer tick events
        /// from the <see cref="Cthangover.Core.UI.Event.SceneEventController"/>.</summary>
        private readonly TimeTickController _timeTickController = new();

        /// <summary>Persisted user-facing settings (audio, display, language, etc.).</summary>
        public SettingsData Settings { get; private set; } = new();
        /// <summary>All mutable session state: time, characters, inventory, recipes, lamp.</summary>
        public RuntimeData Runtime { get; private set; } = new();

        /// <summary>
        /// Called once after entering the scene tree. Loads previously
        /// saved <see cref="SettingsData"/> from disk, activates the
        /// current locale, registers <see cref="_timeTickController"/>
        /// with the event system, and emits a boot log message.
        /// </summary>
        public override void _Ready()
        {
            Settings.Load();
            LocaleLoader.LoadCurrentLanguage();
            _timeTickController.EnsureRegistered(this);
            GameLogger.Log("GAME", $"Game initialized, version={ProjectSettings.GetSetting("application/config/version")}");
        }
    }
}
