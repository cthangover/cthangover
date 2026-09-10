namespace Cthangover.Core.Utils.Types;

/// <summary>
/// Identity parser for <c>string</c>. <see cref="Parse"/> returns the
/// value as-is (including <c>null</c>). <see cref="Format"/> is also
/// identity — strings need no conversion.
/// </summary>
public class StringParse : IParse<string>
{
    /// <inheritdoc/>
    public string Parse(string value)
    {
        return value;
    }

    /// <inheritdoc/>
    public string Format(string value)
    {
        return value;
    }
}
