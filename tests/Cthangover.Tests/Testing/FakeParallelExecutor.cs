namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Cthangover.TestRunner;

    /// <summary>Models timed children and copy readiness, recording resource overlap and component state without Godot.</summary>
    internal sealed class FakeParallelExecutor : IParallelExecutor
    {
        private readonly object gate = new();
        private readonly Dictionary<int, string> occupied = new();
        private readonly Dictionary<string, int> chainState = new();
        private readonly Dictionary<int, string> resident = new();
        private int active;
        private int gui;
        internal int MaxActive { get; private set; }
        internal int MaxGui { get; private set; }
        internal int MaxWriters { get; private set; }
        internal bool LaneOverlap { get; private set; }
        internal bool ChainLostState { get; private set; }
        internal bool StartedBeforeSlowCopy { get; private set; }
        internal int SlowLane { get; set; } = int.MinValue;
        internal HashSet<string> Failures { get; } = new();
        internal List<string> Starts { get; } = new();
        internal List<string> Completions { get; } = new();
        internal HashSet<int> CompletionThreads { get; } = new();
        internal Dictionary<string, int> Lanes { get; } = new();
        internal Dictionary<string, int> Delays { get; } = new();
        internal string ThrowOn { get; set; }
        internal int Active => active;
        private bool slowCopyReady;

        public void PrepareLane(int lane)
        {
            if (lane != SlowLane) return;
            Thread.Sleep(250);
            lock (gate) slowCopyReady = true;
        }

        public TestResult Execute(TestPlanEntry entry, int lane, bool first, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                Starts.Add(entry.Test.Name);
                Lanes[entry.Test.Name] = lane;
                if (SlowLane != int.MinValue && !slowCopyReady) StartedBeforeSlowCopy = true;
                if (lane >= 0)
                {
                    if (occupied.ContainsKey(lane)) LaneOverlap = true;
                    occupied[lane] = entry.Environment;
                    if (first) resident[lane] = entry.Environment;
                    else if (resident.GetValueOrDefault(lane) != entry.Environment) ChainLostState = true;
                    MaxWriters = Math.Max(MaxWriters, occupied.Count);
                }
                if (first) chainState[entry.Environment] = 0;
                else if (!chainState.ContainsKey(entry.Environment)) ChainLostState = true;
                chainState[entry.Environment] = chainState.GetValueOrDefault(entry.Environment) + 1;
                MaxActive = Math.Max(MaxActive, ++active);
                if (entry.Test.Windowed) MaxGui = Math.Max(MaxGui, ++gui);
            }
            try
            {
                if (cancellationToken.WaitHandle.WaitOne(Delays.GetValueOrDefault(entry.Test.Name, 35))) cancellationToken.ThrowIfCancellationRequested();
                if (entry.Test.Name == ThrowOn) throw new InvalidOperationException("fake launcher infrastructure failure");
                return new TestResult { Name = entry.Test.Name, Passed = !Failures.Contains(entry.Test.Name), ExitCode = 0,
                    PassedChecks = 1, Duration = TimeSpan.FromMilliseconds(35) };
            }
            finally
            {
                lock (gate)
                {
                    active--;
                    if (entry.Test.Windowed) gui--;
                    if (lane >= 0) occupied.Remove(lane);
                }
            }
        }

        public void Completed(TestPlanEntry entry, TestResult result, int lane)
        {
            CompletionThreads.Add(Environment.CurrentManagedThreadId);
            Completions.Add(entry.Test.Name);
        }
    }
}
