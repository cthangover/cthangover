namespace Cthangover.Core.Events
{
    /// <summary>
    /// Base class for all event bus events. C# mods inherit from it,
    /// GDScript uses the string layer (Dictionary).
    /// </summary>
    public abstract class ModEvent
    {
        /// <summary>Sending mod (null = engine).</summary>
        public string SenderModId { get; init; }

        /// <summary>
        /// Tick from GameData.Instance.Runtime.Time at publish time.
        /// 0 if GameData is not yet initialized.
        /// </summary>
        public long Tick { get; init; }
    }
}
