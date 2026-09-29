namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Collects quest declarations. Quests carry no
    /// references the validator can check yet, so the base skeleton — parse the
    /// envelope, key by <c>Id</c>, declare it under the quest kind — is all
    /// there is to do.
    /// </summary>
    public class QuestEntityStrategy : EntityKindStrategy
    {
        public override string Kind => ValidationKind.Quest;
    }
}
