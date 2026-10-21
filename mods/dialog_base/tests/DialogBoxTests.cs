namespace Cthangover.Mods.Dialog.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Dialog.Action.Impls;
    using Cthangover.Mods.Dialog.Executable;

    /// <summary>
    /// <see cref="DialogBox"/> as the single owner of the dialog: a locker-less click
    /// dialog keeps the box busy, a debugger hold keeps a <c>delay</c> from being
    /// skipped by its timer, and choice answers advance correctly with and without a
    /// target.
    /// </summary>
    public class DialogBoxTests
    {
        [ModTest("box/click_dialog_blocks_event_dialog", TimeoutSeconds = 10)]
        static async Task ClickDialogBlocksEventDialog(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            try
            {
                var click = new ProbeAction();
                var dlg = new DialogQueue();
                dlg.Queue.Add(click);
                ModAssert.True(stage.Box.SetDialogQueueAndRun(dlg, null, 0, null), "click dialog starts");
                ModAssert.True(stage.Box.IsRunning, "a locker-less dialog occupies the box");

                var evt = new ScenarioEvent { ScenarioText = "text \"event\"", ScenarioId = "dialog_base_test_one_run", IsOneRun = true };
                stage.Host.AddChild(evt);
                ModAssert.False(evt.RunDialog(), "event dialog is refused while the click dialog runs");
                await DialogStage.Frames(3);

                ModAssert.Null(stage.Box.Locker, "the click dialog keeps no locker");
                ModAssert.True(stage.Box.Visible, "box stays visible");
                ModAssert.True(stage.Box.Runtime.CurrentAction == click, "click dialog still waits on its action");
                ModAssert.True(evt.IsFirstRun, "refused event is not counted as run");
                var completed = GameData.Instance?.Runtime?.CompletedScenarioIds;
                ModAssert.False(completed != null && completed.Contains("dialog_base_test_one_run"), "refused one-run scenario is not marked completed");

                var other = new DialogQueue();
                other.Queue.Add(new ProbeAction());
                ModAssert.False(stage.Box.SetDialogQueueAndRun(other, null, 0, null), "a second click dialog is refused too");
            }
            finally
            {
                stage.Free();
            }
        }

        [ModTest("box/held_delay_is_not_skipped", TimeoutSeconds = 10)]
        static async Task HeldDelayIsNotSkipped(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            var previous = DialogRuntime.Debugger;
            try
            {
                var delay = new ActionDelay { WaitTime = 0.05f };
                var after = new ProbeAction();
                var dlg = new DialogQueue();
                dlg.Queue.Add(delay);
                dlg.Queue.Add(after);
                DialogRuntime.Debugger = new HoldDebugger { Target = delay };

                stage.Box.SetDialogQueueAndRun(dlg, null, 0, null);
                ModAssert.True(stage.Box.Runtime.IsHeld, "runtime is held before the delay");
                await DialogStage.Seconds(0.3);

                ModAssert.True(stage.Box.Runtime.IsHeld, "hold survives the delay's duration");
                ModAssert.True(stage.Box.Runtime.CurrentAction == delay, "held delay stays current");
                ModAssert.Equal(0, after.Runs, "action after the held delay has not run");

                DialogRuntime.Debugger = null;
                stage.Box.Resume();
                await DialogStage.Seconds(0.3);

                ModAssert.Equal(1, after.Runs, "after resume the delay elapses and the dialog goes on");
                ModAssert.True(stage.Box.Runtime.CurrentAction == after, "runtime waits on the action after the delay");
            }
            finally
            {
                DialogRuntime.Debugger = previous;
                stage.Free();
            }
        }

        [ModTest("box/option_without_target_falls_through", TimeoutSeconds = 10)]
        static async Task OptionWithoutTargetFallsThrough(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            try
            {
                var after = new ProbeAction();
                var dlg = new DialogQueue();
                dlg.Select(new List<SelectVariant> { SelectVariant.New("go on", null) }, "choose");
                dlg.Queue.Add(after);

                stage.Box.SetDialogQueueAndRun(dlg, null, 0, null);
                ModAssert.True(stage.Box.IsWaitingAnswer, "choice is shown");
                ModAssert.True(stage.Box.TrySelectVariant(0), "variant is picked");

                ModAssert.False(stage.Box.IsWaitingAnswer, "the same choice is not shown again");
                ModAssert.Equal(1, after.Runs, "action after the choice runs");
                ModAssert.True(stage.Box.Runtime.CurrentAction == after, "runtime waits on the action after the choice");
            }
            finally
            {
                stage.Free();
            }
        }

        [ModTest("box/option_to_missing_label_ends_once", TimeoutSeconds = 10)]
        static async Task OptionToMissingLabelEndsOnce(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            var recorder = new DialogEndRecorder();
            recorder.Start();
            try
            {
                var dlg = new DialogQueue();
                dlg.Select(new List<SelectVariant> { SelectVariant.New("broken", "no_such_label") }, "choose");
                dlg.Queue.Add(new ProbeAction());

                stage.Box.SetDialogQueueAndRun(dlg, null, 0, null);
                stage.Box.TrySelectVariant(0);

                ModAssert.Equal(1, recorder.LockerIds.Count, "DialogEndedEvent count after an option to a missing label");
                ModAssert.False(stage.Box.IsRunning, "box is free");
            }
            finally
            {
                recorder.Stop();
                stage.Free();
            }
        }
    }
}
