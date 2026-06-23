using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{

    public interface IRecruitBehaviour
    {
        string Id { get; }
        void ConfigureRecruit(Recruit recruit, RuntimeData runtime);
        void OnTick(Recruit recruit, RuntimeData runtime, long currentTick);
        void OnRemove(Recruit recruit, RuntimeData runtime);
    }

}
