using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.TestRunner
{
    /// <summary>
    /// Точка входа тестового мода: переносит интеграционную обвязку диалогов
    /// (tests DialogAutoDriver + SceneSwitcher) в engine-мод. Активируется
    /// только при наличии пользовательских флагов <c>--choices=</c> или
    /// <c>--timeout=</c> (как <c>OS.GetCmdlineUserArgs()</c>; флаг <c>--scene=</c>
    /// мод не читает — навигацией по сценам управляет ядро). При активации
    /// создаёт <see cref="DialogAutoDriverNode"/> и добавляет его в корень
    /// дерева через deferred-вызов: <c>OnModResourcesReady</c> срабатывает до
    /// окончания построения сцены, прямой AddChild в этот момент отклоняется
    /// движком, а драйверу всё равно нужно дождаться диалога (та же задержка
    /// 0.5с, что у SceneSwitcher.ScheduleDriverAttach). Повторный
    /// <c>ModManager.Reload()</c> не должен плодить драйверы — защищено
    /// статическим флагом.
    /// </summary>
    public class TestRunnerInitializer : IModInitializer
    {
        private static bool driverQueued;

        /// <summary>Инициализация per-mod не требуется — драйвер ставится на resources-ready.</summary>
        public void OnModLoaded(string modId) { }

        /// <summary>
        /// Разбирает пользовательские аргументы командной строки и при
        /// активационном флаге ставит драйвер автопроигрывания в корень дерева.
        /// </summary>
        public void OnModResourcesReady()
        {
            if (driverQueued)
                return;

            var args = OS.GetCmdlineUserArgs();
            var hasChoices = args.Any(a => a.StartsWith("--choices=", System.StringComparison.Ordinal));
            var hasTimeout = args.Any(a => a.StartsWith("--timeout=", System.StringComparison.Ordinal));
            if (!hasChoices && !hasTimeout)
                return;

            var choices = ParseChoices(args);
            var timeout = ParseTimeout(args);

            // Godot. квалифицирован: в dev-компиляции модов (ToolsModCompileTests)
        // в глобальном пространстве имён существует namespace Engine из тестовой сборки,
        // и неквалифицированное имя резолвится в него вместо Godot.Engine.
        var root = (Godot.Engine.GetMainLoop() as SceneTree)?.Root;
            if (root == null)
            {
                GameLogger.Log("TEST", "TestRunnerInitializer: tree root not available, driver not attached", LogLevel.Error);
                return;
            }

            driverQueued = true;
            GameLogger.Log("TEST",
                $"TestRunnerInitializer: activating dialog auto-driver, choices=[{string.Join(",", choices)}], timeout={timeout?.ToString() ?? "none"}s");

            // Deferred: сцена ещё строится; драйвер внутри сам выдерживает
            // стартовую паузу и лениво ищет DialogBox, поэтому порядок с
            // загрузкой сцены не важен.
            root.CallDeferred("add_child", new DialogAutoDriverNode(choices, timeout));
        }

        private static List<int> ParseChoices(string[] args)
        {
            var raw = args
                .Where(a => a.StartsWith("--choices=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--choices=".Length))
                .FirstOrDefault();
            if (string.IsNullOrEmpty(raw))
                return new List<int>();

            return raw.Split(',')
                .Select(s => int.TryParse(s.Trim(), out var n) ? n : -1)
                .Where(n => n >= 0)
                .ToList();
        }

        private static float? ParseTimeout(string[] args)
        {
            var raw = args
                .Where(a => a.StartsWith("--timeout=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--timeout=".Length))
                .FirstOrDefault();
            return float.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var t) && t > 0
                ? t
                : null;
        }
    }
}
