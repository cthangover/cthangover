using System;

namespace Cthangover.Core.Utils.Types;

/// <summary>
/// Nullable <c>bool</c> parser. Returns <c>null</c> on null, empty,
/// or unparseable input — never throws. Stores as <c>"True"</c>/<c>"False"</c>
/// via <see cref="Format"/>.
/// </summary>
public class BoolParse : IParse<bool?>
{
    /// <inheritdoc/>
    public bool? Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            return bool.Parse(value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string Format(bool? value)
    {
        return value?.ToString();
    }
}

/// <summary>
/// Non-nullable <c>bool</c> parser. Delegates to <see cref="BoolParse"/>
/// and folds <c>null</c> into <c>false</c> — the conventional default
/// for boolean flags.
/// </summary>
public class BoolPrimitiveParse : IParse<bool>
{
    private static readonly BoolParse parser = new BoolParse();

    /// <inheritdoc/>
    public bool Parse(string value)
    {
        return parser.Parse(value) ?? false;
    }

    /// <inheritdoc/>
    public string Format(bool value)
    {
        return parser.Format(value);
    }
}
