namespace Cthangover.Core.Relationship
{
    using Cthangover.Core.Mods.Consumers;

    /// <summary>
    /// Feeds <see cref="RecruitBehaviourRegistry"/> with every mod's
    /// <see cref="IRecruitBehaviour"/> implementations as the mods load and
    /// removes them when their mod unloads.
    /// </summary>
    public class RecruitBehaviourConsumer : ModTypeConsumer<IRecruitBehaviour>
    {
        /// <inheritdoc />
        public override string Id => "recruiting.behaviours";

        /// <inheritdoc />
        protected override void Add(IRecruitBehaviour instance, string modId) => RecruitBehaviourRegistry.Instance.RegisterBehaviour(instance);

        /// <inheritdoc />
        protected override void Remove(IRecruitBehaviour instance, string modId) => RecruitBehaviourRegistry.Instance.UnregisterBehaviour(instance);
    }
}
