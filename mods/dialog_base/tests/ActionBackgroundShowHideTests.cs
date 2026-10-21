namespace Cthangover.Mods.Dialog.Tests
{
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Dialog.Action.Impls;

    /// <summary>
    /// <see cref="ActionBackgroundShowHide"/> without <c>wait</c>: the dialog goes on at
    /// once and the fade still plays to the end.
    /// </summary>
    public class ActionBackgroundShowHideTests
    {
        [ModTest("background_fade/no_wait_fade_completes", TimeoutSeconds = 10)]
        static async Task NoWaitFadeCompletes(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            try
            {
                var after = new ProbeAction();
                var dlg = new DialogQueue();
                dlg.BackgroundShowHide(BackgroundActionType.Hide, 0.1f);
                dlg.Queue.Add(after);

                stage.Box.SetDialogQueueAndRun(dlg, null, 0, null);
                ModAssert.Equal(1, after.Runs, "dialog does not wait for the fade");
                await DialogStage.Seconds(0.4);

                ModAssert.True(stage.Host.Modulate.A < 0.01f, $"background faded out (alpha {stage.Host.Modulate.A})");
            }
            finally
            {
                stage.Free();
            }
        }
    }
}
