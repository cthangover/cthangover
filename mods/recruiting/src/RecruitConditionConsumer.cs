namespace Cthangover.Core.Relationship
{
    using Cthangover.Core.Mods.Consumers;

    /// <summary>
    /// Feeds <see cref="RecruitBehaviourRegistry"/> with every mod's
    /// <see cref="IRecruitCondition"/> implementations as the mods load and
    /// removes them when their mod unloads.
    /// </summary>
    public class RecruitConditionConsumer : ModTypeConsumer<IRecruitCondition>
    {
        /// <inheritdoc />
        public override string Id => "recruiting.conditions";

        /// <inheritdoc />
        protected override void Add(IRecruitCondition instance, string modId) => RecruitBehaviourRegistry.Instance.RegisterCondition(instance);

        /// <inheritdoc />
        protected override void Remove(IRecruitCondition instance, string modId) => RecruitBehaviourRegistry.Instance.UnregisterCondition(instance);
    }
}
