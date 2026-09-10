using System;

namespace Cthangover.Core.Utils.Types;

/// <summary>
/// Nullable <c>int</c> parser. Returns <c>null</c> on null, empty,
/// or unparseable input — never throws. When the property didn't exist,
/// <c>null</c> is distinguishable from a legitimate zero.
/// </summary>
public class IntParse : IParse<int?>
{
    /// <inheritdoc/>
    public int? Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            return int.Parse(value);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string Format(int? value)
    {
        return value?.ToString();
    }
}

/// <summary>
/// Non-nullable <c>int</c> parser. Delegates to <see cref="IntParse"/>
/// and folds <c>null</c> into <c>0</c> — a sensible default when the
/// consumer doesn't need to distinguish "property absent" from "zero".
/// </summary>
public class IntPrimitiveParse : IParse<int>
{
    private static readonly IntParse parser = new IntParse();

    /// <inheritdoc/>
    public int Parse(string value)
    {
        return parser.Parse(value) ?? 0;
    }

    /// <inheritdoc/>
    public string Format(int value)
    {
        return parser.Format(value);
    }
}
