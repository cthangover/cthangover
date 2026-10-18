using System.Collections.Generic;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.InventoryTest
{
    /// <summary>
    /// Драйвер интеграционного GUI-теста инвентаря. Проходит по реальному UI
    /// теми же контрактами, что и игрок: открывает панель через публичный
    /// <see cref="UiRegistry.Toggle"/> (тот же путь, что кнопка тулбара),
    /// сеет детерминированные предметы через публичный <c>IInventory.Add</c>,
    /// кликает ячейку эмуляцией Godot-сигнала <c>gui_input</c> (обработчик
    /// ячейки подписан на него — мод не может переопределять виртуальные
    /// методы, но сигналы из мод-сборок работают), фильтрует через
    /// <c>LineEdit</c>-сигнал <c>text_changed</c> и закрывает панель.
    ///
    /// Машина состояний: один шаг за тик таймера (0.4с), чтобы отложенные
    /// пересборки сетки успевали примениться. Каждый шаг — отдельная
    /// проверка: исход фиксируется, провал не прерывает остальные. Итог —
    /// контракт раннера <c>[TEST_RESULT] name=inventory_test passed=N
    /// failed=M</c> и код выхода процесса. Общий watchdog
    /// (<c>--timeout=</c>, по умолчанию 60с) гасит зависание с failed=1 —
    /// тем же контрактом, что и у DialogAutoDriverNode.
    /// </summary>
    public partial class InventoryTestDriverNode : Node
    {
        private const string ResultName = "inventory_test";
        private const string PanelEntryId = "inventory";
        private const float TickSeconds = 0.4f;
        private const float DefaultTimeoutSeconds = 60f;

        private readonly float? timeoutSeconds;
        private readonly List<string> failures = new();
        private int passed;
        private int failed;
        private int step;
        private double elapsed;

        private Control panel;
        private Control gridContent;
        private LineEdit search;

        /// <summary>Создаёт драйвер с watchdog-таймаутом (null — дефолт 60с) и ставит тикающий таймер-ребёнок.</summary>
        public InventoryTestDriverNode(float? timeoutSeconds)
        {
            this.timeoutSeconds = timeoutSeconds;
            var timer = new Timer
            {
                Name = "TickTimer",
                WaitTime = TickSeconds,
                Autostart = true,
                OneShot = false
            };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        /// <summary>Один шаг машины состояний: ожидание готовности, затем проверки по очереди.</summary>
        private void Tick()
        {
            if (step < 0)
                return; // тест уже завершён

            elapsed += TickSeconds;
            var limit = timeoutSeconds ?? DefaultTimeoutSeconds;
            if (elapsed >= limit)
            {
                Finish($"timeout ({limit:0}s) reached at step {step}");
                return;
            }

            try
            {
                RunStep();
            }
            catch (System.Exception ex)
            {
                failures.Add($"step {step}: unhandled {ex.GetType().Name}: {ex.Message}");
                failed++;
                Log($"[FAIL] step {step}: {ex.Message}");
                Finish(null);
            }
        }

        private void RunStep()
        {
            // Шаг 0: ждём готовности — бут завершён и сцена вошла в дерево.
            if (step == 0)
            {
                var ready = Boot.IsReady
                    && GameData.Instance?.Runtime != null
                    && (Godot.Engine.GetMainLoop() as SceneTree)?.CurrentScene != null;
                if (!ready)
                    return; // ждём дальше, watchdog страхует
                Log("environment ready (boot complete, scene entered)");
                Next();
                return;
            }

            // Шаг 1: UI-запись инвентаря зарегистрирована модом cthangover_story.
            if (step == 1)
            {
                Check("entry registered", () =>
                {
                    var entry = AppServices.Current.Ui.GetById(PanelEntryId);
                    if (entry == null)
                        throw new CheckException($"ui entry '{PanelEntryId}' not found");
                });
                Next();
                return;
            }

            // Шаг 2: сеем детерминированные предметы через публичный API
            // инвентаря (в этой сессии; сохранения тест не пишет).
            if (step == 2)
            {
                Check("inventory seed via IInventory.Add", () =>
                {
                    var inv = GameData.Instance.Runtime.Inventory;
                    if (inv == null)
                        throw new CheckException("Runtime.Inventory is null (items mod factory missing?)");
                    inv.Add("food/ration", 3);
                    inv.Add("food/wolf_meat", 2);
                    if (inv.CheckCount("food/ration") < 3)
                        throw new CheckException("food/ration count < 3 after Add");
                    if (inv.CheckCount("food/wolf_meat") < 2)
                        throw new CheckException("food/wolf_meat count < 2 after Add");
                });
                Next();
                return;
            }

            // Шаг 3: открываем панель тем же вызовом, что и кнопка тулбара.
            if (step == 3)
            {
                Check("open via UiRegistry.Toggle", () =>
                {
                    UiRegistry.Toggle(PanelEntryId);
                    if (!UiHost.Instance.TryGetInstance(PanelEntryId, out panel) || panel == null)
                        throw new CheckException("panel instance not created by Toggle");
                    if (!panel.Visible)
                        throw new CheckException("panel not visible after Toggle");
                });
                Next();
                return;
            }

            // Шаг 4: сетка отрисовала засеянные предметы.
            if (step == 4)
            {
                Check("grid renders seeded items", () =>
                {
                    gridContent = panel.FindChild("GridContent", true, false) as Control;
                    if (gridContent == null)
                        throw new CheckException("GridContent node not found");
                    // Один кадр на пересборку после Add: если ячейки ещё не
                    // успели, тик-ждать нельзя (шаг уже выполняется), поэтому
                    // сравниваем с запасом на асинхронность через событие Change,
                    // которое панель обрабатывает синхронно.
                    if (gridContent.GetChildCount() < 2)
                        throw new CheckException($"grid has {gridContent.GetChildCount()} cell(s), expected >= 2");
                });
                Next();
                return;
            }

            // Шаг 5: клик по первой ячейке — эмуляция gui_input, как будто
            // щёлкнул игрок; панель выбирает предмет и наполняет паспорт.
            if (step == 5)
            {
                Check("cell click fills passport", () =>
                {
                    var cell = gridContent.GetChildCount() > 0 ? gridContent.GetChild(0) as Control : null;
                    if (cell == null)
                        throw new CheckException("no cell to click");
                    var click = new InputEventMouseButton
                    {
                        ButtonIndex = MouseButton.Left,
                        Pressed = true
                    };
                    cell.EmitSignal("gui_input", click);

                    var passport = panel.FindChild("Passport", true, false);
                    if (passport == null)
                        throw new CheckException("Passport node not found");
                    var hasText = false;
                    foreach (var child in passport.FindChildren("*", "Label", true, false))
                    {
                        if (child is Label l && !string.IsNullOrWhiteSpace(l.Text))
                        {
                            hasText = true;
                            break;
                        }
                    }
                    if (!hasText)
                        throw new CheckException("passport has no non-empty label after selection");
                });
                Next();
                return;
            }

            // Шаг 6: поиск-фильтр через сигнал LineEdit. Старые ячейки сетка
            // удаляет через QueueFree — они исчезают лишь в конце кадра, поэтому
            // в этом же тике проверяем синхронный индикатор ShownLabel
            // ("показано 0"), а количество нод — на следующем тике.
            if (step == 6)
            {
                Check("no-match filter zeroes the shown counter", () =>
                {
                    search = panel.FindChild("Search", true, false) as LineEdit;
                    if (search == null)
                        throw new CheckException("Search LineEdit not found");

                    search.Text = "zzz_no_such_item";
                    search.EmitSignal("text_changed", search.Text);

                    var shown = panel.FindChild("ShownLabel", true, false) as Label;
                    var text = shown?.Text ?? "";
                    if (!text.Contains("0"))
                        throw new CheckException($"ShownLabel '{text}' does not show zero after no-match filter");
                });
                Next();
                return;
            }

            // Шаг 7: кадр прошёл — ячейки после no-match фильтра реально
            // удалены из дерева; затем возвращаем фильтр в пустое значение.
            if (step == 7)
            {
                Check("grid empties after filter, restores after reset", () =>
                {
                    var after = gridContent.GetChildCount();
                    if (after != 0)
                        throw new CheckException($"grid has {after} cell(s) after no-match filter, expected 0");

                    search.Text = "";
                    search.EmitSignal("text_changed", search.Text);
                });
                Next();
                return;
            }

            // Шаг 8: сетка восстановлена; повторный Toggle закрывает панель.
            if (step == 8)
            {
                Check("grid restored and second Toggle closes", () =>
                {
                    var restored = gridContent.GetChildCount();
                    if (restored < 2)
                        throw new CheckException($"grid has {restored} cell(s) after filter reset, expected >= 2");

                    UiRegistry.Toggle(PanelEntryId);
                    if (panel.Visible)
                        throw new CheckException("panel still visible after second Toggle");
                });
                Finish(null);
            }
        }

        private void Next()
        {
            step++;
        }

        private void Check(string title, System.Action body)
        {
            var sw = Godot.Time.GetTicksUsec();
            try
            {
                body();
                passed++;
                Log($"[PASS] {title} ({(Godot.Time.GetTicksUsec() - sw) / 1000.0:0.0} ms)");
            }
            catch (CheckException ex)
            {
                failed++;
                failures.Add($"{title}: {ex.Message}");
                Log($"[FAIL] {title}: {ex.Message}");
            }
        }

        /// <summary>Печатает итог и завершает процесс; после вызова шаги не выполняются.</summary>
        private void Finish(string reason)
        {
            if (step < 0)
                return;
            step = -1;

            if (reason != null)
            {
                failed++;
                Log($"[FAIL] {reason}");
            }
            foreach (var f in failures)
                Log($"       failed: {f}");
            Log($"[TEST_RESULT] name={ResultName} passed={passed} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }

        private void Log(string message)
        {
            GameLogger.Log("TEST", $"InventoryTest: {message}");
        }

        /// <summary>Отличает ожидаемую проверку от неожиданного исключения шага.</summary>
        private sealed class CheckException : System.Exception
        {
            public CheckException(string message) : base(message) { }
        }
    }
}
