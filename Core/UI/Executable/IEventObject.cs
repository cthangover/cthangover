using Cthangover.Core.Factories;

namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// Contract for event-tracked objects: extends IIdentifiable (has an ID
    /// string) and must support Destruct for cleanup.
    /// </summary>
    public interface IEventObject : IIdentifiable
    {
        void Destruct();
    }

}
