using System;

namespace Cthangover.Core.Utils.Types;

/// <summary>
/// Nullable <c>long</c> parser. Returns <c>null</c> on null, empty,
/// or unparseable input — never throws.
/// </summary>
public class LongParse : IParse<long?>
{
    /// <inheritdoc/>
    public long? Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            return long.Parse(value);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string Format(long? value)
    {
        return value?.ToString();
    }
}

/// <summary>
/// Non-nullable <c>long</c> parser. Delegates to <see cref="LongParse"/>
/// and folds <c>null</c> into <c>0L</c>.
/// </summary>
public class LongPrimitiveParse : IParse<long>
{
    private static readonly LongParse parser = new LongParse();

    /// <inheritdoc/>
    public long Parse(string value)
    {
        return parser.Parse(value) ?? 0L;
    }

    /// <inheritdoc/>
    public string Format(long value)
    {
        return parser.Format(value);
    }
}
