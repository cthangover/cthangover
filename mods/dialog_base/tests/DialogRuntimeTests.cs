namespace Cthangover.Mods.Dialog.Tests
{
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Dialog.Action.Impls;

    /// <summary>
    /// Queue mechanics of <see cref="DialogRuntime"/>: a queue ends exactly once however
    /// it reaches the end, actions reached again through a looping goto get a full
    /// construct/destruct life every pass, and a fall-through branch does not skip the
    /// action after it.
    /// </summary>
    public class DialogRuntimeTests
    {
        [ModTest("runtime/end_command_publishes_dialog_ended_once", TimeoutSeconds = 10)]
        static async Task EndCommandPublishesOnce(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            var recorder = new DialogEndRecorder();
            recorder.Start();
            try
            {
                var after = new ProbeAction();
                var dlg = new DialogQueue();
                dlg.Set("x", "1");
                dlg.End();
                dlg.Queue.Add(after);

                ModAssert.True(stage.Box.SetDialogQueueAndRun(dlg, null, 0, null), "dialog starts");

                ModAssert.Equal(1, recorder.LockerIds.Count, "DialogEndedEvent count after 'end'");
                ModAssert.Equal(0, after.Runs, "action after 'end' never runs");
                ModAssert.False(stage.Box.IsRunning, "box is free after 'end'");
            }
            finally
            {
                recorder.Stop();
                stage.Free();
            }
        }

        [ModTest("runtime/goto_missing_label_publishes_dialog_ended_once", TimeoutSeconds = 10)]
        static async Task GotoMissingLabelPublishesOnce(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            var recorder = new DialogEndRecorder();
            recorder.Start();
            try
            {
                var after = new ProbeAction();
                var dlg = new DialogQueue();
                dlg.GoTo("no_such_label");
                dlg.Queue.Add(after);

                stage.Box.SetDialogQueueAndRun(dlg, null, 0, null);

                ModAssert.Equal(1, recorder.LockerIds.Count, "DialogEndedEvent count after a goto to a missing label");
                ModAssert.Equal(0, after.Runs, "action after the broken goto never runs");
                ModAssert.False(stage.Box.IsRunning, "box is free after the broken goto");
            }
            finally
            {
                recorder.Stop();
                stage.Free();
            }
        }

        [ModTest("runtime/goto_loop_destructs_every_pass")]
        static void GotoLoopDestructsEveryPass(ModTestContext ctx)
        {
            var probe = new ProbeAction();
            var dlg = new DialogQueue();
            dlg.Point("loop");
            dlg.Queue.Add(probe);
            dlg.GoTo("loop");

            var runtime = new DialogRuntime();
            runtime.SetDialogQueueAndRun(null, dlg.Queue, null);
            runtime.Run();
            for (var pass = 0; pass < 2; pass++)
            {
                runtime.Next();
                runtime.Run();
            }

            ModAssert.Equal(3, probe.Runs, "probe ran on every pass");
            ModAssert.Equal(3, probe.Constructs, "probe constructed on every pass");
            ModAssert.Equal(2, probe.Destructs, "probe destructed after every finished pass");
            ModAssert.True(runtime.CurrentAction == probe, "runtime waits on the probe again");
            runtime.Abort();
            ModAssert.Equal(3, probe.Destructs, "abort destructs the live pass");
        }

        [ModTest("runtime/if_fall_through_runs_next_action")]
        static void IfFallThroughRunsNextAction(ModTestContext ctx)
        {
            var probe = new ProbeAction();
            var dlg = new DialogQueue();
            dlg.IfGoTo(AlwaysTrue, null, null);
            dlg.Queue.Add(probe);

            var runtime = new DialogRuntime();
            runtime.SetDialogQueueAndRun(null, dlg.Queue, null);
            runtime.Run();

            ModAssert.Equal(1, probe.Runs, "action after a fall-through if runs");
            ModAssert.True(runtime.CurrentAction == probe, "runtime waits on the action after the if");
        }

        [ModTest("runtime/if_without_condition_runs_next_action")]
        static void IfWithoutConditionRunsNextAction(ModTestContext ctx)
        {
            var probe = new ProbeAction();
            var dlg = new DialogQueue();
            dlg.Queue.Add(new ActionIf { TrueGoTo = "nowhere" });
            dlg.Queue.Add(probe);

            var runtime = new DialogRuntime();
            runtime.SetDialogQueueAndRun(null, dlg.Queue, null);
            runtime.Run();

            ModAssert.Equal(1, probe.Runs, "action after an if without condition runs");
            ModAssert.True(runtime.CurrentAction == probe, "runtime waits on the action after the if");
        }

        private static bool AlwaysTrue() => true;
    }
}
