using Cthangover.Core.Relationship;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{
    public class NutritionBehaviour : IRecruitBehaviour
    {
        public string Id => "nutrition";

        public void ConfigureRecruit(Recruit recruit, RuntimeData runtime)
        {
            recruit.Properties.SetLong("FullnessTime", runtime.Time.Tick);
        }

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

        public void OnRemove(Recruit recruit, RuntimeData runtime)
        {
        }
    }
}
