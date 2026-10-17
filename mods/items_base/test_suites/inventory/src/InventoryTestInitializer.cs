using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.InventoryTest
{
    /// <summary>
    /// Точка входа интеграционного теста инвентаря (прецедент live2d_test:
    /// мод сам везёт драйвер и сам печатает [TEST_RESULT]). Активация —
    /// только по явному пользовательскому флагу <c>--itest=inventory</c>
    /// (user-args после --), чтобы мод никогда не вмешивался в обычную игру и
    /// в другие смоук-прогоны. Само переключение сцен выполняет ядро
    /// (<c>--scene=</c>), драйверу остаётся только дождаться готового шелла.
    /// Драйвер ставится в корень дерева отложенно: <c>OnModResourcesReady</c>
    /// срабатывает в фазе Scenes, до окончания бута и до входа в сцену.
    /// </summary>
    public class InventoryTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        /// <summary>Per-mod инициализация не требуется — драйвер ставится на resources-ready.</summary>
        public void OnModLoaded(string modId) { }

        /// <summary>
        /// Разбирает user-args; при активационном флаге добавляет драйвер
        /// теста в корень дерева. Повторный <c>ModManager.Reload()</c> не
        /// должен плодить драйверы — защищено статическим флагом.
        /// </summary>
        public void OnModResourcesReady()
        {
            if (driverQueued)
                return;

            var args = OS.GetCmdlineUserArgs();
            // Ровно свой флаг: широкая проверка StartsWith("--itest=") активировала
            // бы этот тест и при запуске чужого itest-мода, а его драйвер квитит
            // процесс в конце — второй тест не успевал бы отработать.
            if (!args.Any(a => a == "--itest=inventory"))
                return;

            var timeout = ParseTimeout(args);
            driverQueued = true;
            GameLogger.Log("TEST", $"InventoryTestInitializer: activating inventory GUI test, timeout={timeout?.ToString() ?? "none"}s");

            // Deferred: сцена ещё не готова; драйвер сам ждёт Boot.IsReady и
            // появления CurrentScene, поэтому порядок с загрузкой сцены не важен.
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new InventoryTestDriverNode(timeout));
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
