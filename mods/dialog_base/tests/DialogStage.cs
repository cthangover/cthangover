namespace Cthangover.Mods.Dialog.Tests
{
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;
    using Cthangover.Mods.Interface.View;
    using Godot;

    /// <summary>
    /// A minimal live dialog stage for mod tests: a host Control under <c>/root</c>
    /// with the real <c>dialog_box.tscn</c> prefab (named <c>DialogBox</c>, so
    /// <see cref="SceneContextNode.FindNode{T}"/> and the event classes find it like in
    /// a scene) and, on request, a bare <see cref="ViewBox"/> with a foreground slot.
    /// The scene-context lookup cache is cleared on build and on <see cref="Free"/>:
    /// it remembers misses, and a test box must be found even if an earlier lookup
    /// failed. The host is the box's parent Control — background fades animate it.
    /// </summary>
    public class DialogStage
    {
        /// <summary>Parent of every stage node; freed by <see cref="Free"/>.</summary>
        public Control Host { get; private set; }

        /// <summary>The dialog box built from its prefab.</summary>
        public DialogBox Box { get; private set; }

        /// <summary>The view box, after <see cref="AddViewBox"/>.</summary>
        public ViewBox View { get; private set; }

        /// <summary>The running scene tree.</summary>
        public static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

        /// <summary>Builds the host and the dialog box and waits a frame for their <c>_Ready</c>.</summary>
        public static async Task<DialogStage> Create()
        {
            SceneContextNode.Instance?.ClearData();
            var stage = new DialogStage { Host = new Control { Name = "DialogTestHost" } };
            Tree.Root.AddChild(stage.Host);

            stage.Box = new ModScenePrefab("scenes/ui/dialog_box.tscn").Instantiate<DialogBox>();
            ModAssert.NotNull(stage.Box, "dialog_box.tscn builds a DialogBox");
            stage.Box.Name = "DialogBox";
            stage.Host.AddChild(stage.Box);
            await Frames(1);
            return stage;
        }

        /// <summary>Adds a <see cref="ViewBox"/> named <c>ViewBox</c> with a <c>Foreground</c> TextureRect.</summary>
        public ViewBox AddViewBox()
        {
            View = new ViewBox { Name = "ViewBox" };
            View.AddChild(new TextureRect { Name = "Foreground" });
            Host.AddChild(View);
            SceneContextNode.Instance?.ClearData();
            return View;
        }

        /// <summary>Removes the stage from the tree and forgets cached lookups of its nodes.</summary>
        public void Free()
        {
            if (Box != null && Box.IsRunning)
                Box.Abort();
            if (GodotObject.IsInstanceValid(Host))
            {
                Tree.Root.RemoveChild(Host);
                Host.QueueFree();
            }
            SceneContextNode.Instance?.ClearData();
        }

        /// <summary>Waits <paramref name="count"/> process frames (deferred calls run at the end of each).</summary>
        public static async Task Frames(int count)
        {
            for (var i = 0; i < count; i++)
                await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        }

        /// <summary>Waits <paramref name="seconds"/> of game time.</summary>
        public static async Task Seconds(double seconds)
        {
            await Tree.ToSignal(Tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        }
    }
}
