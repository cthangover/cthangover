using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Event;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Lazy bridge between the scene timer system and the recruit
    /// behaviour tick loop. Implements <see cref="IOnTimeEvent"/>
    /// so it can be registered with <see cref="SceneEventController"/>
    /// via <c>AddTimerTickEventListener</c>.
    ///
    /// Registration is lazy (<see cref="EnsureRegistered"/> is a no-op
    /// after the first call) because the recruit system should not
    /// force the timer subsystem to activate if no behaviours are
    /// actually loaded. The first behaviour lifecycle method in
    /// <see cref="RecruitBehaviourRegistry"/> calls
    /// <c>EnsureRegistered</c>, which finds the scene's
    /// <c>EventController</c> node and subscribes.
    ///
    /// <c>Priority = 0</c> so recruit ticks fire at a neutral point in
    /// the timer event chain — before or after other listeners as
    /// determined by their relative priorities.
    /// </summary>
    public class RecruitTickController : IOnTimeEvent
    {
        /// <summary>Neutral priority — doesn't preempt or follow any specific listener.</summary>
        public int Priority => 0;

        private bool _registered;

        /// <summary>
        /// Finds the scene's <c>EventController</c> node and subscribes
        /// this controller as a timer-tick listener. Idempotent —
        /// subsequent calls do nothing.
        /// </summary>
        public void EnsureRegistered()
        {
            if (_registered)
                return;
            var eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
            if (eventController == null)
                return;
            eventController.AddTimerTickEventListener(this);
            _registered = true;
        }

        /// <summary>
        /// Called by <see cref="SceneEventController"/> on every
        /// timer tick. Delegates to
        /// <see cref="RecruitBehaviourRegistry.OnTick"/>.
        /// </summary>
        public void OnTimerTick()
        {
            RecruitBehaviourRegistry.Instance.OnTick();
        }
    }

}
