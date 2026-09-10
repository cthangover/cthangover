namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Fundamental identity contract for any object that participates in the
    /// factory lookup system. Every factory (file-based or prefab-based)
    /// ultimately maps a string <c>ID</c> to a game entity. Classes implementing
    /// this interface can be stored in <c>Dictionary&lt;string, T&gt;</c>
    /// caches and referenced from other data objects by ID rather than by
    /// direct reference, which is essential for JSON-driven content where
    /// inter-object links must survive serialization boundaries.
    /// </summary>
    public interface IIdentifiable
    {
        string ID { get; }
    }

}
