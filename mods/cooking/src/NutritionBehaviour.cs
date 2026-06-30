using Cthangover.Core.Relationship;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Tracks nutrition and starvation for party recruits.
    /// Works by recording the tick when a recruit last ate (<c>FullnessTime</c>),
    /// then each tick computes the elapsed time as a percentage of a 24-hour window.
    /// Once the fullness window expires (100%+), the recruit loses 1 HP per tick
    /// until fed again via a ration or meal action.
    /// Registered with the <see cref="Recruit"/> behaviour pipeline and called
    /// by the time system on each global tick.
    /// </summary>
    public class NutritionBehaviour : IRecruitBehaviour
    {
        /// <summary>
        /// Unique behaviour identifier used to look up and configure this
        /// behaviour from <see cref="Recruit"/>'s behaviour collection.
        /// </summary>
        public string Id => "nutrition";

        /// <summary>
        /// Called when the behaviour is first attached to a recruit.
        /// Seeds <c>FullnessTime</c> with the current global tick so that
        /// starve-damage begins counting from this point.
        /// </summary>
        public void ConfigureRecruit(Recruit recruit, RuntimeData runtime)
        {
            recruit.Properties.SetLong("FullnessTime", runtime.Time.Tick);
        }

        /// <summary>
        /// Evaluates whether the recruit has gone beyond the fullness window
        /// (24 in-game hours, represented as <c>24 * 4 * 60</c> ticks) since
        /// <c>FullnessTime</c>. If the window is exceeded, reduces the
        /// recruit's <c>PROP_HEALTH</c> by 1 per tick until they are fed.
        /// Tick-driven starvation allows gradual death when rations run out.
        /// </summary>
        public void OnTick(Recruit recruit, RuntimeData runtime, long currentTick)
        {
            var fullnessTime = recruit.Properties.GetLong("FullnessTime");
            if (fullnessTime == 0)
                return;
            var windowSize = 24 * 4 * 60L;
            var deltaTime = currentTick - fullnessTime;
            var fullness = (float)deltaTime / windowSize * 100f;
            if (fullness > 100)
            {
                var hp = recruit.Properties.GetInt(Recruit.PROP_HEALTH);
                if (hp > 0)
                    recruit.Properties.SetInt(Recruit.PROP_HEALTH, hp - 1);
            }
        }

        /// <summary>
        /// Cleanup hook invoked when the behaviour is detached from a recruit.
        /// No persistent state requires teardown; exists for interface contract.
        /// </summary>
        public void OnRemove(Recruit recruit, RuntimeData runtime)
        {
        }
    }
}
