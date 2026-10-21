namespace Cthangover.Mods.Dialog.Tests
{
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;
    using Cthangover.Mods.Dialog.Executable;

    /// <summary>
    /// A standalone <see cref="ExecutableEvent"/> (not inside a chain): a run the dialog
    /// box refused is not counted, and the event starts by itself on the next state
    /// change, without polling.
    /// </summary>
    public class ExecutableEventTests
    {
        [ModTest("event/refused_run_is_not_counted", TimeoutSeconds = 10)]
        static async Task RefusedRunIsNotCounted(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            try
            {
                stage.Box.IsActive = false;
                var evt = new ScenarioEvent { ScenarioText = "text \"S\"", ScenarioId = "dialog_base_test_standalone", IsOneRun = true };
                stage.Host.AddChild(evt);
                await DialogStage.Frames(2);

                ModAssert.False(stage.Box.IsRunning, "inactive box refused the event");
                ModAssert.True(evt.IsFirstRun, "refused automatic run is not counted");
                ModAssert.False(evt.RunDialog(), "direct run is refused too");
                ModAssert.True(evt.IsFirstRun, "refused direct run is not counted");

                stage.Box.IsActive = true;
                ConditionStateMonitor.MarkStateChanged();
                await DialogStage.Frames(2);

                ModAssert.True(stage.Box.Locker == evt, "one-run event starts on the next state change");
                ModAssert.False(evt.IsFirstRun, "the real run is counted");
            }
            finally
            {
                stage.Free();
            }
        }
    }
}
