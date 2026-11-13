namespace Cthangover.TestRunner
{
    /// <summary>Captures immutable launch inputs while the coordinator alone advances mutable component state.</summary>
    internal sealed class ScheduledLaunch
    {
        private readonly IParallelExecutor executor;
        private readonly TestPlanEntry entry;
        private readonly int lane;
        private readonly bool first;
        private readonly CancellationToken cancellationToken;

        internal ScheduledLaunch(IParallelExecutor executor, ScheduledComponent component, CancellationToken cancellationToken)
        {
            this.executor = executor;
            entry = component.Entry;
            lane = component.Lane.Value;
            first = component.Next == 0;
            this.cancellationToken = cancellationToken;
        }
        internal TestResult Execute() => executor.Execute(entry, lane, first, cancellationToken);
    }
}
