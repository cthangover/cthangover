using System;
using System.Globalization;

namespace Cthangover.Core.Utils.Types;

/// <summary>
/// Nullable <c>float</c> parser with invariant culture (<c>"."</c> decimal).
/// Returns <c>null</c> on null, empty, or unparseable input — never throws.
/// The invariant formatting ensures JSON round-trip compatibility regardless
/// of OS locale.
/// </summary>
public class FloatParse : IParse<float?>
{
    private NumberFormatInfo format = new()
    {
        NumberDecimalSeparator = "."
    };

    /// <inheritdoc/>
    public float? Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            return float.Parse(value, format);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string Format(float? value)
    {
        return value?.ToString(format);
    }
}

/// <summary>
/// Non-nullable <c>float</c> parser. Delegates to <see cref="FloatParse"/>
/// and folds <c>null</c> into <c>0f</c>.
/// </summary>
public class FloatPrimitiveParse : IParse<float>
{
    private static readonly FloatParse parser = new FloatParse();

    /// <inheritdoc/>
    public float Parse(string value)
    {
        return parser.Parse(value) ?? 0f;
    }

    /// <inheritdoc/>
    public string Format(float value)
    {
        return parser.Format(value);
    }
}
