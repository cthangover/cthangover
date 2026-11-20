using Cthangover.Core.Actions;

namespace Engine.Tests.Actions;

internal class MockCharacterService : ICharacterService
{
    public string LastAddToPartyType { get; private set; }
    public string LastNotifyType { get; private set; }
    public string LastSkillId { get; private set; }

    /// <summary>Call counts: "not called" must stay distinguishable from "called with null".</summary>
    public int AddSkillCalls { get; private set; }

    public void AddToParty(string type) => LastAddToPartyType = type;
    public void SendNotification(string type) => LastNotifyType = type;

    public void AddSkill(string id)
    {
        AddSkillCalls++;
        LastSkillId = id;
    }
}
