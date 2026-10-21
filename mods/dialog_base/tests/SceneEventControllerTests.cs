namespace Cthangover.Mods.Dialog.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.UI.Event;

    /// <summary>
    /// Dispatch contract of the core <see cref="SceneEventController"/> (tested here:
    /// it is a Godot node, xUnit cannot create it; the dialog actions are its main
    /// listeners): lower priority first, registration order within a priority, and a
    /// listener list changed during a dispatch neither skips nor double-calls anyone.
    /// </summary>
    public class SceneEventControllerTests
    {
        [ModTest("event_controller/lower_priority_first_then_registration_order")]
        static void LowerPriorityFirst(ModTestContext ctx)
        {
            var log = new List<string>();
            var controller = new SceneEventController();
            try
            {
                controller.AddUpdateEventListener(new OrderListener { Name = "b", Priority = 1, Log = log });
                controller.AddUpdateEventListener(new OrderListener { Name = "a", Priority = 0, Log = log });
                controller.AddUpdateEventListener(new OrderListener { Name = "c", Priority = 1, Log = log });

                controller._Process(0.0);

                ModAssert.Equal("a,b,c", string.Join(",", log), "dispatch order");
            }
            finally
            {
                controller.Free();
            }
        }

        [ModTest("event_controller/list_changed_during_dispatch")]
        static void ListChangedDuringDispatch(ModTestContext ctx)
        {
            var log = new List<string>();
            var controller = new SceneEventController();
            try
            {
                var removed = new OrderListener { Name = "removed", Priority = 1, Log = log };
                var added = new OrderListener { Name = "added", Priority = 0, Log = log };
                var changer = new OrderListener
                {
                    Name = "changer", Priority = 0, Log = log, Controller = controller,
                    RemoveOnUpdate = removed, AddOnUpdate = added,
                };
                var last = new OrderListener { Name = "last", Priority = 2, Log = log };
                controller.AddUpdateEventListener(changer);
                controller.AddUpdateEventListener(removed);
                controller.AddUpdateEventListener(last);

                controller._Process(0.0);
                ModAssert.Equal("changer,last", string.Join(",", log), "first dispatch: removed one skipped, added one waits");

                log.Clear();
                controller._Process(0.0);
                ModAssert.Equal("changer,added,last", string.Join(",", log), "second dispatch includes the added listener");
            }
            finally
            {
                controller.Free();
            }
        }
    }
}
