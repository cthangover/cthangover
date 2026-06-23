using Cthangover.Core.Characters;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{

    public interface IRecruitCondition
    {
        string Id { get; }
        bool CanRecruit(Character enemy, RuntimeData runtime);
    }

}
