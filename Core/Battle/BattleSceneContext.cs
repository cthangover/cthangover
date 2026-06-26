using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Lights;
using Cthangover.Core.Characters;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Singleton scene-root for battle, wired as a self-registering Node
    /// (sets Instance on _EnterTree, clears on _ExitTree). On _Ready it
    /// assembles player characters from CharacterData.BattleSet, enemy
    /// cards from Runtime.BattleData, resolves the active IBattleCore
    /// via BattleCoreRegistry, and defers Init+Start to the next frame
    /// so the scene tree is fully built before core logic runs.
    /// Tracks PlayerExpGained (per-character, split evenly among survivors)
    /// and DefeatedEnemies for the victory/loot screen. Exposes
    /// OnCharacterDied and OnBattleCleared events so external systems
    /// (e.g. quests, the action machine) can react without coupling.
    /// ShowWinground / ShowDeadground are mutually exclusive end-of-battle
    /// paths — both set IsDestroyed to prevent double-triggering and
    /// reset battle speed before clearing state.
    /// </summary>
    public partial class BattleSceneContext : Node
	{
		public static BattleSceneContext Instance { get; private set; }

		[Export] private TextureRect battleBg;

		public Actions.BattleActionMachine BattleActionMachine { get; private set; }

		private BattleSpeedControl _battleSpeedControl;
		private Widget deadground;
		private Widget winground;

		public Dictionary<string, int> PlayerExpGained { get; private set; } = new();
		public List<Character> DefeatedEnemies { get; private set; } = new();

		public bool IsDestroyed { get; private set; }

		public bool IsWait { get; set; }

		public event System.Action<Character> OnCharacterDied;
		public event System.Action OnBattleCleared;

		public void NotifyCharacterDied(Character character)
		{
			OnCharacterDied?.Invoke(character);
		}

		public override void _EnterTree()
		{
			if (Instance != null && GodotObject.IsInstanceValid(Instance))
			{
				var scene = GetTree()?.CurrentScene?.Name ?? "?";
				var existingPath = Instance.GetPath().ToString();
				var myPath = GetPath().ToString();
				GameLogger.Log("DUPLICATE", $"BattleSceneContext._EnterTree: Instance ALREADY SET by '{existingPath}', overwriting with duplicate at '{myPath}' on scene '{scene}'", LogLevel.Error);
			}
			Instance = this;
		}

		public override void _ExitTree()
		{
			base._ExitTree();
			if (Instance == this)
				Instance = null;
		}

		private List<Character> pendingPlayerCards;
		private List<Character> pendingEnemyCards;
		private IBattleCore activeBattleCore;

		public override void _Ready()
		{
			battleBg = GetNodeOrNull<TextureRect>("BattleBg");

			if (battleBg == null)
			{
				GameLogger.Log("BATTLE", "battleBg not found!", LogLevel.Error);
				return;
			}

			BattleActionMachine = GetNodeOrNull<Actions.BattleActionMachine>("ToolPanel/BattleActionMachine");
			_battleSpeedControl = GetNodeOrNull<BattleSpeedControl>("BattleSpeedControl");
			deadground = GetNodeOrNull<Widget>("Deadground");

			winground = GetNodeOrNull<Widget>("Winground");

			var data = GameData.Instance.Runtime.BattleData;
			if (data == null)
			{
				GameLogger.Log("BATTLE", "BattleData is null!", LogLevel.Error);
				return;
			}

			GameLogger.Log("BATTLE", $"set battle background: battleBg={(battleBg != null ? "found" : "NULL")}, texture={(data.Background != null ? "has" : "NULL")}", battleBg == null || data.Background == null ? LogLevel.Error : LogLevel.Debug);

			if (data.Background != null)
			{
				battleBg.Texture = data.Background;
				GameLogger.Log("BATTLE", $"  battleBg.Texture assigned: width={battleBg.Texture.GetWidth()}, height={battleBg.Texture.GetHeight()}", LogLevel.Debug);
			}
			else
			{
				GameLogger.Log("BATTLE", $"  data.Background is NULL — no background texture set", LogLevel.Error);
			}

			var timedShader = ModManager.Instance.ResolveShader("timed_sprite");
			if (timedShader != null)
			{
				var bgMaterial = new ShaderMaterial { Shader = timedShader };
				battleBg.Material = bgMaterial;

				if (data.DepthMap != null)
					bgMaterial.SetShaderParameter("depth_mask", data.DepthMap);
				if (data.AlbedoMap != null)
					bgMaterial.SetShaderParameter("albedo_map", data.AlbedoMap);

				var lights = GetNodeOrNull<UiLightController>("Lights");
				if (lights != null)
				{
					var viewportSize = GetViewport().GetVisibleRect().Size;
					lights.SetPlayerLight(viewportSize / 2, 500f, 1f);
					lights.RegisterMaterial(bgMaterial);
				}
				GameLogger.Log("BATTLE", $"battleBg shader: timed_sprite applied, depthMap={(data.DepthMap != null ? "has" : "NULL")}, albedoMap={(data.AlbedoMap != null ? "has" : "NULL")}, lightsCtrl={(lights != null ? "found" : "NULL")}", lights == null ? LogLevel.Error : LogLevel.Debug);
			}
			else
			{
				GameLogger.Log("BATTLE", "battleBg shader: timed_sprite NOT FOUND", LogLevel.Error);
			}

			pendingPlayerCards = new List<Character>();
			foreach (var characterType in GameData.Instance.Runtime.CharacterData.BattleSet)
			{
				var characterInfo = GameData.Instance.Runtime.CharacterData.Characters[characterType];
				var template = CharacterFactory.Instance.Get(characterInfo.ID);
				var character = template != null ? template.Copy() : new Character { ID = characterInfo.ID };
				character.Attributes = characterInfo.Attributes ?? new CharacterAttributes();
				character.Level = characterInfo.Level;
				character.Exp = characterInfo.Exp;
				pendingPlayerCards.Add(character);
			}

			pendingEnemyCards = data.EnemiesCards?.Where(c => c != null).ToList() ?? new List<Character>();

			if (!string.IsNullOrEmpty(data.ActiveBattleCore) && BattleCoreRegistry.Instance.HasCore(data.ActiveBattleCore))
				BattleCoreRegistry.Instance.SetActive(data.ActiveBattleCore);

			try
			{
				activeBattleCore = BattleCoreRegistry.Instance.GetActive();
			}
			catch
			{
				activeBattleCore = null;
			}

			if (activeBattleCore == null)
			{
				GameLogger.Log("BATTLE", "No battle core available", LogLevel.Error);
				return;
			}

			CallDeferred(nameof(DeferredInit));
		}

		private void DeferredInit()
		{
			if (IsDestroyed || activeBattleCore == null)
				return;

			var ctx = new BattleContextImpl(this);
			var playerArray = pendingPlayerCards?.ToArray() ?? new Character[0];
			var enemyArray = pendingEnemyCards?.ToArray() ?? new Character[0];

			activeBattleCore.Init(playerArray, enemyArray, ctx);
			activeBattleCore.Start();
		}

		public void ShowDeadground()
		{
			if (IsDestroyed)
				return;
			IsDestroyed = true;
			_battleSpeedControl?.ResetToNormal();
			ClearBattle();
			deadground?.Show();
		}

		public void ShowWinground()
		{
			if (IsDestroyed)
				return;
			IsDestroyed = true;
			_battleSpeedControl?.ResetToNormal();
			ClearBattle();
			winground?.Show();
		}

		public void RecordEnemyDefeated(Character enemy)
		{
			if (enemy == null)
				return;
			DefeatedEnemies.Add(enemy);

			var enemyExp = enemy.Level * enemy.Exp;
			var alivePlayers = 0;
			foreach (var c in pendingPlayerCards)
			{
				if (c?.Attributes?.Health?.Value > 0)
					alivePlayers++;
			}
			if (alivePlayers == 0)
				return;

			var expPerPlayer = enemyExp / alivePlayers;
			foreach (var c in pendingPlayerCards)
			{
				if (c?.Attributes?.Health?.Value <= 0)
					continue;
				var id = c.ID;
				if (id == null)
					continue;
				PlayerExpGained.TryGetValue(id, out var current);
				PlayerExpGained[id] = current + expPerPlayer;
			}
		}

		private void ClearBattle()
		{
			BattleActionMachine?.StopMachine();
			pendingPlayerCards?.Clear();
			pendingEnemyCards?.Clear();
			OnBattleCleared?.Invoke();
		}
	}
}
