using Cthangover.Core.Items;

namespace Engine.Tests.Items
{
    /// <summary>
    /// Minimal <see cref="IItemAction"/> double for item presentation tests:
    /// lets a test item advertise a usable action without touching the real
    /// item-action factory or any mod assembly.
    /// </summary>
    public class StubItemAction : IItemAction
    {
        public string ID { get; set; } = "stub/action";

        public bool Result { get; set; } = true;

        public bool UseAction(IItem item)
        {
            return Result;
        }
    }
}
