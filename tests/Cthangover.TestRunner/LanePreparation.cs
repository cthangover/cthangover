namespace Cthangover.TestRunner
{
    /// <summary>Starts physical copy preparation on a worker without serializing it behind other lanes' copies.</summary>
    internal sealed class LanePreparation
    {
        private readonly IParallelExecutor executor;
        internal int Lane { get; }
        internal Task Ready { get; }
        internal LanePreparation(IParallelExecutor executor, int lane)
        {
            this.executor = executor;
            Lane = lane;
            Ready = Task.Factory.StartNew(Prepare, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        private void Prepare() => executor.PrepareLane(Lane);
    }
}
