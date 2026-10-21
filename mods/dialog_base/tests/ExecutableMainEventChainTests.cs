namespace Cthangover.Mods.Dialog.Tests
{
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;
    using Cthangover.Mods.Dialog.Executable;

    /// <summary>
    /// <see cref="ExecutableMainEventChain"/> is event-driven and loop-safe: the next
    /// event starts within a couple of frames after a dialog ends (no polling delay),
    /// two repeatable events do not ping-pong while the state stays the same, and a
    /// state change makes them eligible again.
    /// </summary>
    public class ExecutableMainEventChainTests
    {
        [ModTest("chain/push_start_and_no_ping_pong", TimeoutSeconds = 15)]
        static async Task PushStartAndNoPingPong(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            try
            {
                var chain = new ExecutableMainEventChain { Name = "TestChain" };
                stage.Host.AddChild(chain);
                var a = new ScenarioEvent { ScenarioText = "text \"A\"", ScenarioId = "dialog_base_test_chain_a" };
                var b = new ScenarioEvent { ScenarioText = "text \"B\"", ScenarioId = "dialog_base_test_chain_b" };
                chain.AddEvent(a);
                chain.AddEvent(b);

                await DialogStage.Frames(2);
                ModAssert.True(stage.Box.Locker == a, "first event starts right after it was added");

                stage.Box.NextAction();
                await DialogStage.Frames(2);
                ModAssert.True(stage.Box.Locker == b, "second event starts within two frames after the first ended");

                stage.Box.NextAction();
                await DialogStage.Frames(4);
                ModAssert.False(stage.Box.IsRunning, "no event re-runs while the state did not change");

                ConditionStateMonitor.MarkStateChanged();
                await DialogStage.Frames(2);
                ModAssert.True(stage.Box.Locker == a, "a state change makes the first event eligible again");
            }
            finally
            {
                stage.Free();
            }
        }
    }
}
