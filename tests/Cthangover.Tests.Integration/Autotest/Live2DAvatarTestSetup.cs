#if TOOLS
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	/// <summary>
	/// Bootstrap интеграционного теста Live2D-аватара. Сам аватар управляется
	/// сценой мода live2d_test (мод — единственное место с компайл-тайм
	/// доступом к Live2D-обёртке), поэтому эта сцена только проверяет
	/// загрузку мода, инстанцирует мод-сцену через рантайм tscn-пайплайн и
	/// передаёт управление драйверу: он печатает [TEST_RESULT] и завершает
	/// дерево с кодом выхода.
	/// </summary>
	public partial class Live2DAvatarTestSetup : Node
	{
		public override void _Ready()
		{
			GameLogger.Log("TEST", "Live2DAvatarTestSetup: starting");

			if (ModManager.Instance?.GetMod("live2d_test") == null)
			{
				TryHealDisabledMod();
				FailBootstrap("mod 'live2d_test' is not loaded");
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
		/// Моды live2d_test и его зависимость live2d могут быть выключены
		/// в user://disabled_mods.json (например, после ручной проверки
		/// тумблера в UI модов). Загрузка модов к этому моменту уже завершена,
		/// поэтому текущий прогон падает, но перед выходом тест включает моды
		/// обратно — следующий запуск будет зелёным.
		/// </summary>
		private void TryHealDisabledMod()
		{
			var disabled = DisabledModsStore.Load();
			var healed = false;
			foreach (var id in new[] { "live2d", "live2d_test" })
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
