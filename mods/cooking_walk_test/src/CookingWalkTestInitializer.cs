using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.CookingWalkTest
{
    /// <summary>
    /// Точка входа интеграционного теста «прогулка + готовка» (прецедент
    /// inventory_test: мод сам везёт драйвер и сам печатает [TEST_RESULT]).
    /// Активация — только по явному пользовательскому флагу
    /// <c>--itest=cooking_walk</c> (user-args после --), чтобы мод никогда не
    /// вмешивался в обычную игру. Стартовая сцена задаётся ядром
    /// (<c>--scene=home_outside</c>); драйверу остаётся дождаться готового
    /// шелла и провести игрока по маршруту кликами по настоящим кнопкам.
    /// </summary>
    public class CookingWalkTestInitializer : IModInitializer
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
            if (!args.Any(a => a == "--itest=cooking_walk"))
                return;

            var timeout = ParseTimeout(args);
            driverQueued = true;
            GameLogger.Log("TEST", $"CookingWalkTestInitializer: activating cooking walk test, timeout={timeout?.ToString() ?? "none"}s");

            // Deferred: сцена ещё не готова; драйвер сам ждёт Boot.IsReady и
            // появления CurrentScene, поэтому порядок с загрузкой сцены не важен.
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new CookingWalkTestDriverNode(timeout));
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
