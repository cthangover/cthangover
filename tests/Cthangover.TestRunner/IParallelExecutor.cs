namespace Cthangover.TestRunner
{
    /// <summary>The scheduler knows resource/dependency contracts; real and fake executors own workspace and child mechanics.</summary>
    public interface IParallelExecutor
    {
        /// <summary>Creates a lane's copy; -1 is the immutable shared lane, nonnegative lanes are exclusive copies.</summary>
        void PrepareLane(int lane);

        /// <summary>Runs one launch. The first launch prepares its component's data; subsequent launches share that state.</summary>
        TestResult Execute(TestPlanEntry entry, int lane, bool first, CancellationToken cancellationToken);

        /// <summary>Called only by the coordinating thread, including blocked launches, for single-writer report checkpoints.</summary>
        void Completed(TestPlanEntry entry, TestResult result, int lane);
    }
}
