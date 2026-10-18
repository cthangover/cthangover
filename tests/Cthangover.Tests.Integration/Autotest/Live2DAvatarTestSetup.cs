#if TOOLS
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	/// <summary>
	/// Bootstrap интеграционного теста Live2D-аватара. Сам аватар управляется
	/// сценой тестового набора live2d_base__avatar (набор мода live2d_base, монтируется
	/// флагом --itest=live2d_avatar; единственное место с компайл-тайм
	/// доступом к Live2D-обёртке), поэтому эта сцена только проверяет
	/// загрузку набора, инстанцирует мод-сцену через рантайм tscn-пайплайн и
	/// передаёт управление драйверу: он печатает [TEST_RESULT] и завершает
	/// дерево с кодом выхода.
	/// </summary>
	public partial class Live2DAvatarTestSetup : Node
	{
		public override void _Ready()
		{
			GameLogger.Log("TEST", "Live2DAvatarTestSetup: starting");

			if (ModManager.Instance?.GetMod("live2d_base__avatar") == null)
			{
				TryHealDisabledMod();
				FailBootstrap("test suite 'live2d_base__avatar' is not mounted (needs --itest=live2d_avatar)");
				return;
			}

			var driver = TscnScenes.LoadAndBuild("scenes/live2d_avatar_test.tscn");
			if (driver == null)
			{
				FailBootstrap("mod scene 'scenes/live2d_avatar_test.tscn' did not build");
				return;
			}

			GameLogger.Log("TEST", "Live2DAvatarTestSetup: driver instantiated, handing control over");
			AddChild(driver);
		}

		/// <summary>
		/// Мод live2d_base (хозяин набора live2d_base__avatar) может быть выключен
		/// в user://disabled_mods.json (например, после ручной проверки
		/// тумблера в UI модов). Загрузка модов к этому моменту уже завершена,
		/// поэтому текущий прогон падает, но перед выходом тест включает моды
		/// обратно — следующий запуск будет зелёным.
		/// </summary>
		private void TryHealDisabledMod()
		{
			var disabled = DisabledModsStore.Load();
			var healed = false;
			foreach (var id in new[] { "live2d_base" })
			{
				if (!disabled.Contains(id))
					continue;
				disabled.Remove(id);
				healed = true;
				GameLogger.Log("TEST", $"  [HEAL] mod '{id}' was disabled, enabling it — rerun the test to apply");
			}

			if (healed)
				DisabledModsStore.Save(disabled);
		}

		private void FailBootstrap(string reason)
		{
			GameLogger.Log("TEST", $"  [FAIL] {reason}", LogLevel.Error);
			GameLogger.Log("TEST", "[TEST_RESULT] name=live2d_avatar passed=0 failed=1");
			GetTree().Quit(1);
		}
	}
}
#endif
