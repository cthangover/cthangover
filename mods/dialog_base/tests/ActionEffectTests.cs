namespace Cthangover.Mods.Dialog.Tests
{
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Dialog.Action.Impls;
    using Godot;

    /// <summary>
    /// <see cref="ActionEffect"/> shows its effect: the instance lands in the ViewBox UI
    /// layer when the action runs and is freed when the dialog ends.
    /// </summary>
    public class ActionEffectTests
    {
        [ModTest("effect/spawned_into_view_until_dialog_end", TimeoutSeconds = 10)]
        static async Task SpawnedIntoViewUntilDialogEnd(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            var view = stage.AddViewBox();
            var template = new Node2D { Name = "TestEffect" };
            var prefab = new PackedScene();
            prefab.Pack(template);
            template.Free();
            try
            {
                var dlg = new DialogQueue();
                dlg.Queue.Add(new ActionEffect { Prefab = prefab });
                dlg.Queue.Add(new ProbeAction());

                stage.Box.SetDialogQueueAndRun(dlg, null, 0, null);
                var layer = view.GetInteractiveLayer("ui");
                ModAssert.Equal(1, layer.GetChildCount(), "effect is shown in the UI layer");
                ModAssert.Equal("TestEffect", layer.GetChild(0).Name.ToString(), "the shown node is the effect");

                stage.Box.NextAction();
                await DialogStage.Frames(2);
                ModAssert.Equal(0, layer.GetChildCount(), "effect is freed when the dialog ends");
            }
            finally
            {
                stage.Free();
            }
        }
    }
}
