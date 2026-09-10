using System.Threading;
using Cthangover.Core.Events;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Единый «флаг актуальности» результатов вычисления сценарных условий.
    /// Подписывается на все события изменения состояния, влияющие на кондишины
    /// (теги/статусы квестов, флаги, восстановление сейва, интерактивы), и
    /// инкрементирует монотонный Version. Потребители (ScenarioEvent, цепочки)
    /// считают закешированный результат актуальным, пока Version не изменился.
    /// MarkStateChanged() — публичный ручной инвалидатор для модов с кастомным
    /// состоянием вне квестов. Notify&lt;T&gt; — публичен для юнит-тестов без Godot.
    /// Внимание: ModEventBus.ClearAll() сбрасывает подписки; в игре он не
    /// вызывается, но при вызове нужно заново дёрнуть EnsureSubscribed().
    /// </summary>
    public static class ConditionStateMonitor
    {
        private static long _version;
        private static readonly object Lock = new();
        private static bool _subscribed;

        /// <summary>
        /// Монотонно растущий счётчик изменений состояния. Любое событие,
        /// подписанное монитором, увеличивает его на 1; равенство значений
        /// означает «состояние не менялось с прошлого чтения».
        /// </summary>
        public static long Version => Interlocked.Read(ref _version);

        /// <summary>
        /// Ручной инвалидатор: форсирует рост Version вне штатных событий
        /// (для модов с кастомным состоянием, влияющим на кондишины).
        /// </summary>
        public static void MarkStateChanged()
        {
            Interlocked.Increment(ref _version);
        }

        /// <summary>Обработчик любого события монитора — бампает версию.</summary>
        public static void Notify<T>(T evt) where T : ModEvent
        {
            MarkStateChanged();
        }

        /// <summary>
        /// Гарантирует подписку монитора на все события изменения состояния.
        /// Идемпотентна — повторные вызовы ничего не делают. Вызывается из
        /// статического конструктора; повторный вызов нужен только если
        /// <c>ModEventBus.ClearAll()</c> сбросил статические подписки.
        /// </summary>
        public static void EnsureSubscribed()
        {
            lock (Lock)
            {
                if (_subscribed)
                    return;
                _subscribed = true;
                ModEventBus.Subscribe<QuestTagChangedEvent>(Notify);
                ModEventBus.Subscribe<QuestStatusChangedEvent>(Notify);
                ModEventBus.Subscribe<QuestDataStatusChangedEvent>(Notify);
                ModEventBus.Subscribe<FlagChangedEvent>(Notify);
                ModEventBus.Subscribe<QuestStateRestoredEvent>(Notify);
                ModEventBus.Subscribe<InteractiveStateChangedEvent>(Notify);
            }
        }

        static ConditionStateMonitor()
        {
            EnsureSubscribed();
        }
    }
}
