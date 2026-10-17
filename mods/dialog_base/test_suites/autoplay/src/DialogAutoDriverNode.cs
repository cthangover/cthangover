using System.Collections.Generic;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.TestRunner
{
    /// <summary>
    /// Мод-порт интеграционного <c>DialogAutoDriver</c>: автопроигрывает
    /// активный диалог, кликая <c>IDialogBox.NextAction()</c> каждые 0.1с и
    /// отвечая на выборы по индексам из <c>--choices=</c> (по порядку, после
    /// исчерпания — первый вариант). Тик реализован дочерним Godot
    /// <see cref="Timer"/>, а не override <c>_Process</c>: сборка мода
    /// компилируется Roslyn вне source-генератора Godot, поэтому виртуальные
    /// методы Node не диспетчеризуются, тогда как сигнал <c>Timer.Timeout</c>
    /// — это Godot-объект и работает из мод-сборок. DialogBox ищется лениво
    /// через корень дерева по имени «DialogBox» (контракт ядра
    /// <see cref="IDialogBox"/>, типы dialog_base не нужны). Завершение
    /// диалога и общий таймаут печатают строку
    /// <c>[TEST_RESULT] name=test_runner passed=N failed=M</c> — формат парсит
    /// tests/Cthangover.TestRunner (ResultParser), после чего процесс
    /// завершается с соответствующим кодом выхода. Как и в оригинале,
    /// <c>IsDialogEnded</c> истинно до загрузки первой очереди, поэтому
    /// end-обработка ждёт, пока диалог реально наблюдался запущенным, плюс
    /// стартовая пауза 0.5с на построение сцены.
    /// </summary>
    public partial class DialogAutoDriverNode : Node
    {
        /// <summary>Имя, под которым раннер атрибутирует результат прогона.</summary>
        private const string ResultName = "test_runner";

        /// <summary>Пауза после старта, чтобы сцена успела построиться (как в SceneSwitcher).</summary>
        private const double StartDelaySeconds = 0.5;

        /// <summary>Сколько бокс может быть найден, но не запущен, прежде чем считаем сцену без диалога.</summary>
        private const double FirstDialogGraceSeconds = 3.0;

        /// <summary>Интервал автопрокликивания, сек (как в оригинальном драйвере).</summary>
        private const double AdvanceIntervalSeconds = 0.1;

        private readonly List<int> choices;
        private readonly float? timeoutSeconds;

        private IDialogBox dialogBox;
        private int choiceIndex;
        private double elapsedSeconds;
        private double dialogFoundAtSeconds;
        private bool hasSeenRunningDialog;
        private bool finished;

        /// <summary>
        /// Создаёт драйвер; <paramref name="choices"/> — индексы вариантов по
        /// порядку появления, <paramref name="timeoutSeconds"/> — общий лимит
        /// прогона (null — без таймаута). Тикающий таймер-ребёнок ставится
        /// прямо в конструкторе (без override _EnterTree/_Ready — мод-сборка
        /// вне source-генератора Godot): WaitTime 0.1с с автостартом
        /// воспроизводит ритм оригинального драйвера, а сигнал
        /// <c>Timer.Timeout</c> — Godot-сигнал и работает из мод-сборок.
        /// </summary>
        public DialogAutoDriverNode(List<int> choices, float? timeoutSeconds)
        {
            this.choices = choices ?? new List<int>();
            this.timeoutSeconds = timeoutSeconds;

            var timer = new Timer
            {
                Name = "TickTimer",
                WaitTime = AdvanceIntervalSeconds,
                Autostart = true,
                OneShot = false
            };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        /// <summary>Один тик автопроигрывания: ленивый поиск бокса, прокликивание, выборы, end/timeout-обработка.</summary>
        private void Tick()
        {
            if (finished)
                return;

            elapsedSeconds += AdvanceIntervalSeconds;

            if (timeoutSeconds.HasValue && elapsedSeconds >= timeoutSeconds.Value)
            {
                Finish(0, 1, $"timeout ({timeoutSeconds.Value}s) reached");
                return;
            }

            if (elapsedSeconds < StartDelaySeconds)
                return;

            if (dialogBox is not GodotObject liveBox || !GodotObject.IsInstanceValid(liveBox) || liveBox is not Node liveNode || !liveNode.IsInsideTree())
            {
                dialogBox = FindDialogBox();
                if (dialogBox == null)
                    return;

                dialogFoundAtSeconds = elapsedSeconds;
                hasSeenRunningDialog = false;
                var choicesInfo = choices.Count > 0
                    ? "choices=[" + string.Join(",", choices) + "]"
                    : "no choices, will pick first variant each time";
                GameLogger.Log("TEST", $"DialogAutoDriverNode started, interval={AdvanceIntervalSeconds}, {choicesInfo}");
            }

            if (dialogBox.IsRunning)
                hasSeenRunningDialog = true;

            // IsDialogEnded истинно и до первой очереди — ждём наблюдения
            // запущенного диалога плюс льготный период на пустых сценах.
            var waitingForFirstDialog = !hasSeenRunningDialog
                && elapsedSeconds - dialogFoundAtSeconds < FirstDialogGraceSeconds;

            if (dialogBox.IsDialogEnded && !waitingForFirstDialog)
            {
                GameLogger.Log("TEST", "DialogAutoDriverNode: dialog ended, quitting");
                Finish(1, 0, null);
                return;
            }

            if (dialogBox.IsWaitingAnswer)
            {
                HandleSelection();
                return;
            }

            dialogBox.NextAction();
        }

        /// <summary>Выбирает показанный вариант по индексу из --choices= (вне диапазона — первый).</summary>
        private void HandleSelection()
        {
            var count = dialogBox.AnswerVariantCount;
            if (count <= 0)
                return;

            var pick = 0;
            if (choiceIndex < choices.Count)
            {
                pick = choices[choiceIndex];
                if (pick >= count)
                    pick = 0;
                choiceIndex++;
            }

            GameLogger.Log("TEST", $"DialogAutoDriverNode: choice #{choiceIndex} picking index={pick} out of {count}");
            dialogBox.TrySelectVariant(pick);
        }

        /// <summary>
        /// Ищет DialogBox через корень дерева: драйвер часто добавляется до
        /// появления диалоговой сцены, а каст к контракту ядра избавляет мод
        /// от ссылки на конкретную реализацию.
        /// </summary>
        private IDialogBox FindDialogBox()
        {
            var tree = GetTree();
            if (tree?.Root == null)
                return null;
            return tree.Root.FindChild("DialogBox", true, false) as IDialogBox;
        }

        /// <summary>Печатает итоговую строку для раннера и завершает процесс (0 — успех, 1 — провал).</summary>
        private void Finish(int passed, int failed, string reason)
        {
            if (finished)
                return;
            finished = true;

            if (reason != null)
                GameLogger.Log("TEST", $"DialogAutoDriverNode: {reason}");
            GameLogger.Log("TEST", $"[TEST_RESULT] name={ResultName} passed={passed} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }
    }
}
