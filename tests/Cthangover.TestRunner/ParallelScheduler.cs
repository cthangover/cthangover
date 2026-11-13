namespace Cthangover.TestRunner
{
    /// <summary>
    /// Coordinates ready copies and longest-first dependency components. Only this thread publishes completions;
    /// workers never mutate the ordered result set. GUI children consume both global and window permits.
    /// </summary>
    public static class ParallelScheduler
    {
        /// <summary>Runs fake or real children with identical lane/dependency rules; a fault drains workers before returning.</summary>
        public static IReadOnlyList<TestResult> Run(IReadOnlyList<TestPlanEntry> plan, RunnerOptions options,
            IParallelExecutor executor, CancellationToken cancellationToken = default)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var components = plan.GroupBy(entry => entry.Environment).Select(CreateComponent)
                .OrderByDescending(component => component.Seconds).ToList();
            var preparations = new List<LanePreparation>();
            if (components.Any(component => !component.Writable)) preparations.Add(new LanePreparation(executor, -1));
            var copies = Math.Min(Math.Min(options.WriteJobs, options.Jobs), components.Count(component => component.Writable));
            for (var lane = 0; lane < copies; lane++) preparations.Add(new LanePreparation(executor, lane));
            var completed = new Dictionary<string, TestResult>(StringComparer.Ordinal);
            var indices = plan.Select((entry, index) => (entry.Test.Name, index)).ToDictionary(pair => pair.Name, pair => pair.index);
            var results = new TestResult[plan.Count];
            try
            {
                while (components.Any(component => !component.Done))
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    foreach (var preparation in preparations)
                        if (preparation.Ready.IsCompleted) preparation.Ready.GetAwaiter().GetResult();
                    foreach (var component in components)
                    {
                        if (component.Running == null || !component.Running.IsCompleted) continue;
                        var result = component.Running.GetAwaiter().GetResult();
                        Complete(component, result, completed, results, indices, executor);
                    }
                    var active = components.Count(component => component.Running != null);
                    var gui = components.Count(component => component.Running != null && component.Entry.Test.Windowed);
                    foreach (var component in components)
                    {
                        if (component.Done || component.Running != null) continue;
                        if (component.Lane == null)
                        {
                            var available = preparations.FirstOrDefault(preparation => preparation.Ready.IsCompletedSuccessfully
                                && (component.Writable ? preparation.Lane >= 0 && !components.Any(other => other.Lane == preparation.Lane) : preparation.Lane == -1));
                            if (available == null) continue;
                            component.Lane = available.Lane;
                        }
                        var blocked = component.Entry.Test.DependsOn.Where(name => !completed.TryGetValue(name, out var prior) || !prior.Passed).ToArray();
                        if (blocked.Length > 0)
                        {
                            var result = Program.Pending(component.Entry);
                            result.Note = "blocked by unsuccessful prerequisite(s): " + string.Join(", ", blocked);
                            result.FailureKind = "dependency";
                            Complete(component, result, completed, results, indices, executor);
                            continue;
                        }
                        if (active >= options.Jobs || component.Entry.Test.Windowed && gui >= options.GuiJobs) continue;
                        var launch = new ScheduledLaunch(executor, component, cancellation.Token);
                        component.Running = Task.Factory.StartNew(launch.Execute, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        active++;
                        if (component.Entry.Test.Windowed) gui++;
                    }
                    var waiting = components.Where(component => component.Running != null).Select(component => (Task)component.Running)
                        .Concat(preparations.Where(preparation => !preparation.Ready.IsCompleted).Select(preparation => preparation.Ready)).ToArray();
                    if (waiting.Length > 0) Task.WaitAny(waiting, 100);
                }
                return results;
            }
            finally
            {
                cancellation.Cancel();
                var outstanding = components.Where(component => component.Running != null).Select(component => (Task)component.Running)
                    .Concat(preparations.Select(preparation => preparation.Ready)).ToArray();
                try { Task.WaitAll(outstanding); } catch (AggregateException) { }
            }
        }

        private static ScheduledComponent CreateComponent(IGrouping<string, TestPlanEntry> entries) => new(entries);

        private static void Complete(ScheduledComponent component, TestResult result, Dictionary<string, TestResult> completed,
            TestResult[] results, Dictionary<string, int> indices, IParallelExecutor executor)
        {
            var entry = component.Entry;
            result.Environment = entry.Environment;
            result.Filter = entry.Filter;
            result.ModFilter = entry.ModFilter;
            result.SelectionReason = entry.Reason;
            result.DependsOn = entry.Test.DependsOn;
            completed[entry.Test.Name] = result;
            results[indices[entry.Test.Name]] = result;
            executor.Completed(entry, result, component.Lane.Value);
            component.Running = null;
            component.Next++;
            if (component.Done) component.Lane = null;
        }
    }
}
