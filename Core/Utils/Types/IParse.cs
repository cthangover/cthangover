namespace Cthangover.Core.Utils.Types;

/// <summary>
/// Bidirectional parser for a type <typeparamref name="T"/>.
/// <see cref="Parse"/> converts a string to <typeparamref name="T"/>
/// (safe — returns <c>default</c> on unparseable input, never throws).
/// <see cref="Format"/> converts <typeparamref name="T"/> back to a string
/// (culture-invariant for numeric types).
/// </summary>
/// <remarks>
/// Implementations are registered in <see cref="TParser"/> and used by
/// <see cref="PropertyData.Get{T}"/>/<see cref="PropertyData.Set{T}"/>
/// and <see cref="Cthangover.Core.Characters.PropertyInterceptor{T}"/>.
/// Two implementation families exist: nullable (e.g. <c>IntParse : IParse&lt;int?&gt;</c>)
/// for parsers that distinguish "absent" from "zero", and primitive
/// (e.g. <c>IntPrimitiveParse : IParse&lt;int&gt;</c>) that fold
/// absent into a sensible default.
/// </remarks>
public interface IParse<T>
{
    /// <summary>
    /// Converts a string to <typeparamref name="T"/>.
    /// Returns <c>default(T)</c> on null, empty, or unparseable input.
    /// Never throws.
    /// </summary>
    T Parse(string value);

    /// <summary>
    /// Converts <typeparamref name="T"/> back to a string.
    /// Numeric types use <c>"."</c> as decimal separator (invariant culture).
    /// </summary>
    string Format(T value);
}
