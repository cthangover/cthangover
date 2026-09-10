
namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Contract for intercepting property writes in <see cref="Cthangover.Core.Utils.PropertyData"/>.
    /// Each interceptor is bound to a single property key via <see cref="PropertyName"/>
    /// and is called in registration order when that property is written through
    /// any typed setter (<c>SetStr</c>, <c>SetInt</c>, <c>SetFloat</c>, <c>SetBool</c>, <c>SetLong</c>).
    /// Multiple interceptors for the same property form a pipeline: each receives
    /// the value returned by the previous one, so interceptors can compose
    /// (e.g. one doubles healing, another caps max HP).
    /// </summary>
    /// <remarks>
    /// Registration is per-instance via <see cref="Cthangover.Core.Utils.PropertyData.AddInterceptor"/>.
    /// Interceptors are NOT copied by <see cref="Cthangover.Core.Utils.PropertyData.Clone"/> —
    /// they are runtime behaviour scoped to a specific character's property bag.
    /// </remarks>
    public interface IPropertyInterceptor
    {
        /// <summary>
        /// Called before the value is stored in the backing dictionary.
        /// Receives the previous string value (<c>null</c> if the property
        /// didn't exist) and the new value (possibly already modified by
        /// earlier interceptors). Returns the value to store after this
        /// interceptor's transformation.
        /// </summary>
        /// <param name="prevValue">
        /// Raw string value before this write, or <c>null</c> if the
        /// property was absent. Use <see cref="PropertyInterceptor{T}"/>
        /// for type-safe access — it handles parsing and formatting.
        /// </param>
        /// <param name="newValue">
        /// Value being written — may have been modified by earlier
        /// interceptors in the chain. Guaranteed non-null: all
        /// <c>SetXxx</c> methods produce a non-null string.
        /// </param>
        /// <returns>The (possibly modified) string value to store.</returns>
        string Intercept(string prevValue, string newValue);

        /// <summary>
        /// The property key this interceptor watches.
        /// <see cref="PropertyData.Set{T}"/> only calls interceptors whose
        /// <see cref="PropertyName"/> matches the key being written.
        /// </summary>
        string PropertyName { get; }
    }
}
