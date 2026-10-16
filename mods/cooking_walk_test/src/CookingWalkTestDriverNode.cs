using System.Collections.Generic;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.CookingWalkTest
{
    /// <summary>
    /// Драйвер интеграционного теста «прогулка + готовка». Проводит игрока по
    /// маршруту теми же контрактами, что и человек: выборы локационных меню —
    /// честным кликом по кнопке варианта (<c>AnswerBox → Content → AnswerItem
    /// → Button.Pressed</code>), панель верстака — через <c>UiRegistry.Toggle</c>
    /// (тот же путь, что сценарная опция «Готовить»), клик по рецепту —
    /// эмуляцией <c>gui_input</c> строки рецепта, готовка и закрытие — кликами
    /// по <c>BtnCook</c>/<c>BtnClose</c>. Разблокировка рецепта выполняется
    /// публичным игровым путём: использование предмета-рецепта
    /// (<c>IItem.ItemAction.UseAction</c>) — как это делает инвентарь.
    ///
    /// Машина состояний: один шаг за тик таймера (0.4с), чтобы успевали
    /// применяться отложенные смены сцен и пересборки панелей. Каждый шаг —
    /// отдельная проверка: провал фиксируется, но не обрывает маршрут (кроме
    /// неожиданных исключений). Ожидание смены сцены — по
    /// <c>SceneManager.CurrentSceneName</c>, ожидание меню — по
    /// <c>IDialogBox.IsWaitingAnswer && IsAnswerBoxShowed</c>. Итог — контракт
    /// раннера <c>[TEST_RESULT] name=cooking_walk_test passed=N failed=M</code>
    /// и код выхода процесса; watchdog (<c>--timeout=</c>, дефолт 90с) гасит
    /// зависание с failed=1.
    /// </summary>
    public partial class CookingWalkTestDriverNode : Node
    {
        private const string ResultName = "cooking_walk_test";
        private const float TickSeconds = 0.4f;
        private const float DefaultTimeoutSeconds = 90f;

        private readonly float? timeoutSeconds;
        private readonly List<string> failures = new();
        private int passed;
        private int failed;
        private int step;
        private double elapsed;

        /// <summary>Ожидаемая логическая сцена для шагов-переходов (null — шаг не про переход).</summary>
        private string expectedScene;

        private IDialogBox dialogBox;
        private Control workbench;
        private Control recipeRow;
        private int rationBefore;
        private int meatBefore;
        private int branchesBefore;

        /// <summary>Создаёт драйвер с watchdog-таймаутом (null — дефолт 90с) и ставит тикающий таймер-ребёнок.</summary>
        public CookingWalkTestDriverNode(float? timeoutSeconds)
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

        /// <summary>Один шаг машины состояний: ожидание готовности, затем маршрут по очереди.</summary>
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
            // Шаг 0: ждём готовности — бут завершён, сцена home_outside вошла в
            // дерево, диалог-бокс доступен.
            if (step == 0)
            {
                var sceneName = SceneContextNode.FindNode<SceneManager>("SceneManager")?.CurrentSceneName;
                var ready = Boot.IsReady
                    && GameData.Instance?.Runtime != null
                    && sceneName == "home_outside"
                    && TryGetDialogBox(out dialogBox);
                if (!ready)
                    return;
                Log("environment ready (home_outside entered, dialog box found)");
                Next();
                return;
            }

            // Шаг 1: стартовое меню home_outside показано; сеем предметы и
            // предмет-рецепт через публичный API (эквивалент лута с волка).
            if (step == 1)
            {
                if (!WaitSceneMenu())
                    return;
                Check("home_outside menu shown; seeded wolf meat + branches + recipe item", () =>
                {
                    var inv = GameData.Instance.Runtime.Inventory;
                    if (inv == null)
                        throw new CheckException("Runtime.Inventory is null (items mod factory missing?)");
                    inv.Add("food/wolf_meat", 1);
                    inv.Add("resource/branches", 2);
                    inv.Add("recipe/wolf_meat_to_ration", 1);
                    if (inv.CheckCount("food/wolf_meat") < 1
                        || inv.CheckCount("resource/branches") < 2
                        || inv.CheckCount("recipe/wolf_meat_to_ration") < 1)
                        throw new CheckException("seeded items missing after Add");
                });
                Next();
                return;
            }

            // Шаг 2: используем предмет-рецепт (использование предмета из
            // инвентаря вызывает его ItemAction) — рецепт должен разблокироваться.
            if (step == 2)
            {
                Check("recipe unlocked via item use action", () =>
                {
                    var item = ItemFactory.Instance.Get("recipe/wolf_meat_to_ration");
                    if (item == null)
                        throw new CheckException("recipe item not resolvable from factory");
                    if (item.ItemAction == null)
                        throw new CheckException("recipe item has no ItemAction (items/cooking mods not wired?)");
                    var wasKnown = GameData.Instance.Runtime.RecipeData.Has("recipe/wolf_meat_to_ration");
                    item.ItemAction.UseAction(item);
                    if (!wasKnown && !GameData.Instance.Runtime.RecipeData.Has("recipe/wolf_meat_to_ration"))
                        throw new CheckException("UseAction did not register the recipe");
                });
                Next();
                return;
            }

            // Шаг 3: клик по кнопке «Зайти в дом» (первый вариант меню
            // home_outside) → home_vestibule.
            if (step == 3)
            {
                if (!WaitSceneMenu())
                    return;
                ClickVariant(0, "home_outside: 'Зайти в дом'");
                expectedScene = "home_vestibule";
                Next();
                return;
            }

            // Шаг 4: ждём прихода в коридор.
            if (step == 4) { WaitArrival(); return; }

            // Шаг 5: меню коридора; клик «В обеденную комнату» → home_kitchen.
            if (step == 5)
            {
                if (!WaitSceneMenu())
                    return;
                ClickVariant(1, "home_vestibule: 'В обеденную комнату'");
                expectedScene = "home_kitchen";
                Next();
                return;
            }

            // Шаг 6: ждём кухню.
            if (step == 6) { WaitArrival(); return; }

            // Шаг 7: меню кухни (cooking-сценарий добавляет вариант «Готовить»);
            // клик по нему открывает верстак через toggle_cooking_workbench.
            if (step == 7)
            {
                if (!WaitSceneMenu())
                    return;
                Check("kitchen menu has the cook option", () =>
                {
                    // На кухне сначала показывается cooking-сценарий (priority 5
                    // раньше default 10) с двумя вариантами: «Выйти», «Готовить».
                    if (dialogBox.AnswerVariantCount < 2)
                        throw new CheckException($"kitchen menu shows {dialogBox.AnswerVariantCount} variants, expected >= 2 (exit + cook)");
                });
                ClickVariant(1, "home_kitchen: 'Готовить'");
                Next();
                return;
            }

            // Шаг 8: ждём панель верстака (overlay открывается Toggle'ом).
            if (step == 8)
            {
                Control panel = null;
                if (UiHost.Instance != null && UiHost.Instance.TryGetInstance("cooking_workbench", out var instance))
                    panel = instance;
                if (panel == null || !panel.Visible)
                    return;
                workbench = panel;
                Check("workbench panel visible with recipe row", () =>
                {
                    var content = workbench.FindChild("Content", true, false);
                    if (content == null || content.GetChildCount() < 1)
                        throw new CheckException("workbench recipe list is empty (recipe not unlocked?)");
                });
                Next();
                return;
            }

            // Шаг 9: клик по строке рецепта (gui_input — тот же путь, что клик
            // мышью: RecipeItemBehaviour подписан на GuiInput).
            if (step == 9)
            {
                Check("recipe row click selects recipe", () =>
                {
                    var content = workbench.FindChild("Content", true, false);
                    recipeRow = content.GetChildCount() > 0 ? content.GetChild(0) as Control : null;
                    if (recipeRow == null)
                        throw new CheckException("no recipe row to click");
                    var click = new InputEventMouseButton
                    {
                        ButtonIndex = MouseButton.Left,
                        Pressed = true
                    };
                    recipeRow.EmitSignal("gui_input", click);

                    var description = workbench.FindChild("TxtDescription", true, false) as RichTextLabel;
                    if (description == null || string.IsNullOrWhiteSpace(description.Text))
                        throw new CheckException("recipe description empty after row click");
                });
                Next();
                return;
            }

            // Шаг 10: запоминаем запасы и жмём BtnCook; проверяем дельты —
            // стартовая партия может уже держать те же предметы.
            if (step == 10)
            {
                Check("cook click consumes inputs and yields ration", () =>
                {
                    var inv = GameData.Instance.Runtime.Inventory;
                    rationBefore = inv.CheckCount("food/ration");
                    meatBefore = inv.CheckCount("food/wolf_meat");
                    branchesBefore = inv.CheckCount("resource/branches");

                    var btn = workbench.FindChild("BtnCook", true, false) as Button;
                    if (btn == null)
                        throw new CheckException("BtnCook not found");
                    btn.EmitSignal("pressed");

                    if (inv.CheckCount("food/wolf_meat") != meatBefore - 1)
                        throw new CheckException($"wolf meat {inv.CheckCount("food/wolf_meat")}, expected {meatBefore - 1} (input consumed)");
                    if (inv.CheckCount("resource/branches") != branchesBefore - 2)
                        throw new CheckException($"branches {inv.CheckCount("resource/branches")}, expected {branchesBefore - 2} (input consumed)");
                    if (inv.CheckCount("food/ration") != rationBefore + 1)
                        throw new CheckException($"ration count {inv.CheckCount("food/ration")}, expected {rationBefore + 1}");
                });
                Next();
                return;
            }

            // Шаг 11: закрываем верстак кнопкой BtnClose.
            if (step == 11)
            {
                Check("workbench closes via BtnClose", () =>
                {
                    var btn = workbench.FindChild("BtnClose", true, false) as Button;
                    if (btn == null)
                        throw new CheckException("BtnClose not found");
                    btn.EmitSignal("pressed");
                    if (workbench.Visible)
                        throw new CheckException("workbench still visible after BtnClose");
                });
                Next();
                return;
            }

            // Шаг 12: меню кухни снова показано; клик «Выйти» → коридор.
            if (step == 12)
            {
                if (!WaitSceneMenu())
                    return;
                ClickVariant(0, "home_kitchen: 'Выйти'");
                expectedScene = "home_vestibule";
                Next();
                return;
            }

            // Шаг 13: ждём коридор.
            if (step == 13) { WaitArrival(); return; }

            // Шаг 14: меню коридора; клик «На улицу» → home_outside.
            if (step == 14)
            {
                if (!WaitSceneMenu())
                    return;
                ClickVariant(4, "home_vestibule: 'На улицу'");
                expectedScene = "home_outside";
                Next();
                return;
            }

            // Шаг 15: ждём улицу.
            if (step == 15) { WaitArrival(); return; }

            // Шаг 16: меню улицы; клик «Зайти в дом» → коридор.
            if (step == 16)
            {
                if (!WaitSceneMenu())
                    return;
                ClickVariant(0, "home_outside: 'Зайти в дом' (2nd visit)");
                expectedScene = "home_vestibule";
                Next();
                return;
            }

            // Шаг 17: ждём коридор (регрессия «кнопки пропали»: меню обязано
            // быть — сценарий коридора не должен быть отфильтрован как
            // завершённый после первого же перехода).
            if (step == 17) { WaitArrival(); return; }

            // Шаг 18: меню коридора; клик «В спальню» → home_bedroom.
            if (step == 18)
            {
                if (!WaitSceneMenu())
                    return;
                ClickVariant(2, "home_vestibule: 'В спальню'");
                expectedScene = "home_bedroom";
                Next();
                return;
            }

            // Шаг 19: ждём спальню.
            if (step == 19) { WaitArrival(); return; }

            // Шаг 20: меню спальни; клик «Выйти» → коридор.
            if (step == 20)
            {
                if (!WaitSceneMenu())
                    return;
                ClickVariant(0, "home_bedroom: 'Выйти'");
                expectedScene = "home_vestibule";
                Next();
                return;
            }

            // Шаг 21: ждём коридор.
            if (step == 21) { WaitArrival(); return; }

            // Шаг 22: меню коридора; клик «В рабочую комнату» → home_workroom.
            if (step == 22)
            {
                if (!WaitSceneMenu())
                    return;
                ClickVariant(0, "home_vestibule: 'В рабочую комнату'");
                expectedScene = "home_workroom";
                Next();
                return;
            }

            // Шаг 23: ждём рабочую комнату — финальная точка маршрута.
            if (step == 23) { WaitArrival(); return; }

            // Шаг 24: меню рабочей комнаты показано → маршрут пройден целиком.
            if (step == 24)
            {
                if (!WaitSceneMenu())
                    return;
                Check("route complete: workroom menu shown", () => { });
                Finish(null);
            }
        }

        /// <summary>
        /// Ждёт, что в текущей сцене показано меню выбора (диалог в режиме
        /// ожидания ответа и бокс вариантов на экране). Меню появляется через
        /// несколько тиков после смены сцены — шаг просто не проходит, пока
        /// условие не выполнено; watchdog страхует зависание.
        /// </summary>
        private bool WaitSceneMenu()
        {
            return dialogBox != null
                && dialogBox.IsActive
                && dialogBox.IsWaitingAnswer
                && dialogBox.IsAnswerBoxShowed;
        }

        /// <summary>
        /// Шаг ожидания перехода: сцена уже сменилась на ожидаемую — проверка
        /// пройдена; иначе шаг молча повторяется на следующем тике.
        /// </summary>
        private void WaitArrival()
        {
            var target = expectedScene;
            var current = SceneContextNode.FindNode<SceneManager>("SceneManager")?.CurrentSceneName;
            if (current != target)
                return;

            Check($"arrived at '{target}'", () =>
            {
                if (current != target)
                    throw new CheckException($"current scene '{current}', expected '{target}'");
            });
            expectedScene = null;
            Next();
        }

        /// <summary>
        /// Честный клик по кнопке варианта: находит AnswerBox в дереве
        /// диалог-бокса, берёт строку варианта с индексом
        /// <paramref name="index"/> и дёргает её Button (тот же сигнал, что
        /// нажатие мышью — AnswerItem подписан на Pressed). Падает, если
        /// структура бокса не соответствует ожидаемой.
        /// </summary>
        private void ClickVariant(int index, string what)
        {
            Check($"click {what}", () =>
            {
                // IDialogBox — контракт ядра без Node-членов; сам бокс живёт
                // под concrete-узлом DialogBox в дереве.
                var box = (dialogBox as Node)?.FindChild("AnswerBox", true, false) as Control;
                if (box == null)
                    throw new CheckException("AnswerBox node not found under dialog box");
                var content = box.FindChild("Content", true, false);
                if (content == null || content.GetChildCount() <= index)
                    throw new CheckException($"answer content missing or has fewer than {index + 1} items");

                var item = content.GetChild(index);
                var button = item?.FindChild("Button", true, false) as Button;
                if (button == null)
                    throw new CheckException("variant Button node not found in answer item");

                button.EmitSignal("pressed");
            });
        }

        private bool TryGetDialogBox(out IDialogBox box)
        {
            var node = SceneContextNode.FindNode<Node>("DialogBox");
            box = node as IDialogBox;
            return box != null;
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
            GameLogger.Log("TEST", $"CookingWalkTest: {message}");
        }

        /// <summary>Отличает ожидаемую проверку от неожиданного исключения шага.</summary>
        private sealed class CheckException : System.Exception
        {
            public CheckException(string message) : base(message) { }
        }
    }
}
