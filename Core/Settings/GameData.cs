using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Unity-style Godot autoload that serves as the single entry point
    /// for the game state subsystem. Holds the <see cref="SettingsData"/>
    /// (persisted user preferences) and <see cref="RuntimeData"/> (session
    /// state). On <c>_Ready</c> it loads saved settings. In-game time
    /// advancement is owned by the time_base engine mod, which registers
    /// its clock tick controller on boot. The current locale and the
    /// session <see cref="RuntimeData"/> are created by the unified boot
    /// pipeline (see <see cref="Boot"/>), which runs after this autoload.
    /// </summary>
    public partial class GameData : Node
    {
        static GameData()
        {
            GameEnvironment.IsRuntime = true;
            GameEnvironment.MarkMainThread();
        }

        /// <summary>Global singleton, set once during <c>_EnterTree</c>.</summary>
        public static GameData Instance { get; private set; }

        /// <summary>
        /// Constructs the game data root and performs the earliest
        /// initialization pass: <see cref="GameLogger.Init"/> must run before
        /// any subsystem logs, and this constructor is the first point where
        /// that is guaranteed.
        /// </summary>
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

        /// <summary>Persisted user-facing settings (audio, display, language, etc.).</summary>
        public SettingsData Settings { get; private set; } = new();

        private RuntimeData _runtime;

        /// <summary>
        /// Set when <see cref="Runtime"/> is first touched before
        /// <see cref="Boot.ModsReady"/>. Such an instance seeded its starter
        /// inventory against an empty registry — the skips are permanent — so
        /// <see cref="Boot.EnsureRuntime"/> recreates it via
        /// <see cref="ConsumePrematureRuntimeCreation"/> once the mod set is final.
        /// </summary>
        private bool _runtimeCreatedPrematurely;

        /// <summary>
        /// All mutable session state: time, characters, inventory, recipes, lamp.
        /// Created by the boot pipeline after the mod set is final, so constructing
        /// it seeds the starter inventory through fully loaded mod factories. The
        /// getter stays lazy only as a safety net for tests and tools that touch
        /// <see cref="Runtime"/> outside the boot window: in a real runtime such a
        /// first touch before boot finished logs a loud error, because the starter
        /// inventory would then depend on who got there first instead of on the
        /// boot ordering. (xUnit tests run with GameEnvironment.IsRuntime == false
        /// and stay silent.)
        /// </summary>
        public RuntimeData Runtime
        {
            get
            {
                if (_runtime == null)
                {
                    if (GameEnvironment.IsRuntime && !Boot.ModsReady)
                    {
                        _runtimeCreatedPrematurely = true;
                        GameLogger.Log("GAME",
                            $"RuntimeData created before boot finished (phase={Boot.Phase}) — starter inventory may be empty; the boot pipeline is supposed to create it first",
                            LogLevel.Error);
                    }
                    _runtime = new RuntimeData();
                    GameLogger.Log("GAME", "RuntimeData created");
                }
                return _runtime;
            }
        }

        /// <summary>
        /// Reports whether <see cref="Runtime"/> was created before the boot
        /// pipeline had mods ready, resetting the flag. One-shot: the caller
        /// (boot) recreates the instance so the starter inventory seeds from
        /// fully loaded factories.
        /// </summary>
        public bool ConsumePrematureRuntimeCreation()
        {
            if (!_runtimeCreatedPrematurely)
                return false;
            _runtimeCreatedPrematurely = false;
            return true;
        }

        /// <summary>
        /// Discards the current <see cref="RuntimeData"/> and creates a
        /// fresh one. Must be called when starting a new game: the
        /// autoload (and therefore <see cref="Runtime"/>) survives
        /// returning to the main menu, so without a reset, state from a
        /// previous playthrough — including completed one-run scenario
        /// history — would leak into the new game.
        /// </summary>
        public void ResetRuntime(string reason = "new game")
        {
            _runtime = new RuntimeData();
            GameLogger.Log("GAME", $"RuntimeData reset ({reason})");
        }

        /// <summary>
        /// Begins a "New Game": wipes the runtime state and resets every
        /// registered <see cref="IResettable"/> state owner through
        /// <see cref="AppServices.ResetForNewGame"/> — quests, world map
        /// open/hidden overrides, and any mod-registered owner. The reset
        /// loop logs each owner and isolates its exceptions, so one broken
        /// reset cannot abort the others. Afterwards publishes
        /// <see cref="Events.NewGameStartedEvent"/>: the event stays the public
        /// contract for mod listeners that re-seed per-session data on top of
        /// the fresh state — the core mod restores the starter party there,
        /// because <see cref="ResetRuntime"/> discards it and
        /// <c>IModInitializer.OnModResourcesReady</c> only runs once at boot.
        /// State owners must not reset themselves through that event; they
        /// register an <see cref="IResettable"/> instead. Call this instead of
        /// <see cref="ResetRuntime"/> whenever a fresh game starts.
        /// </summary>
        public void StartNewGame()
        {
            ResetRuntime();
            AppServices.Current.ResetForNewGame();
            Events.ModEventBus.Publish(new Events.NewGameStartedEvent());
        }

        /// <summary>
        /// Called once after entering the scene tree. Loads previously
        /// saved <see cref="SettingsData"/> from disk and emits a boot log
        /// message. In-game time is driven by the time_base engine mod,
        /// which registers its own tick controller with the event system
        /// on <c>OnModResourcesReady</c>. The current locale is activated by
        /// the boot pipeline (phase <c>Locale</c>, after the mod set is
        /// final), in both launcher and sync modes.
        /// </summary>
        public override void _Ready()
        {
            Settings.Load();
            GameLogger.Log("GAME", $"Game initialized, version={ProjectSettings.GetSetting("application/config/version")}");
        }
    }
}
