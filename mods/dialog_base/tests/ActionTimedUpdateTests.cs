namespace Cthangover.Mods.Dialog.Tests
{
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Dialog.Action.Impls;

    /// <summary>
    /// <see cref="ActionTimedUpdate"/> completes itself: when its timeline elapses the
    /// dialog goes on, including the timer of a <c>delay</c> right after it.
    /// </summary>
    public class ActionTimedUpdateTests
    {
        [ModTest("timed_update/completion_continues_dialog", TimeoutSeconds = 10)]
        static async Task CompletionContinuesDialog(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            try
            {
                var timed = new ActionTimedUpdate { WaitTime = 0.1f };
                var after = new ProbeAction();
                var dlg = new DialogQueue();
                dlg.Queue.Add(timed);
                dlg.Delay(0.1f);
                dlg.Queue.Add(after);

                stage.Box.SetDialogQueueAndRun(dlg, null, 0, null);
                ModAssert.True(stage.Box.Runtime.CurrentAction == timed, "dialog waits on the timeline");
                await DialogStage.Seconds(0.8);

                ModAssert.True(timed.IsDestructed, "timeline is finished and detached");
                ModAssert.Equal(1, after.Runs, "timeline and the delay after it elapsed, the dialog went on");
                ModAssert.True(stage.Box.Runtime.CurrentAction == after, "runtime waits on the action after the delay");
            }
            finally
            {
                stage.Free();
            }
        }
    }
}
