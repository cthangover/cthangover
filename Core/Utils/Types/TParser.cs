using System;
using System.Collections.Generic;

namespace Cthangover.Core.Utils.Types;

/// <summary>
/// Central registry for <see cref="IParse{T}"/> instances, keyed by
/// <see cref="Type"/>. Resolves a typed parser from a dictionary without
/// reflection — the <c>(IParse&lt;T&gt;)(object)</c> cast is safe because
/// each entry was inserted with the correct <see cref="Type"/> key.
/// </summary>
/// <remarks>
/// New types are registered by adding entries to <see cref="_parsers"/>.
/// Each key must match the generic argument of its value exactly
/// (both nullable and primitive variants are supported).
/// Used by <see cref="PropertyData.Get{T}"/>, <see cref="PropertyData.Set{T}"/>,
/// and <see cref="Cthangover.Core.Characters.PropertyInterceptor{T}"/>.
/// </remarks>
public static class TParser
{
    private static readonly Dictionary<Type, object> _parsers = new()
    {
        { typeof(int?),    new IntParse()            },
        { typeof(int),     new IntPrimitiveParse()   },
        { typeof(float?),  new FloatParse()          },
        { typeof(float),   new FloatPrimitiveParse() },
        { typeof(long?),   new LongParse()           },
        { typeof(long),    new LongPrimitiveParse()  },
        { typeof(bool?),   new BoolParse()           },
        { typeof(bool),    new BoolPrimitiveParse()  },
        { typeof(string),  new StringParse()         },
    };

    /// <summary>
    /// Looks up the <see cref="IParse{T}"/> registered for
    /// <typeparamref name="T"/>. Throws
    /// <see cref="NotSupportedException"/> if no parser is registered
    /// for the requested type.
    /// </summary>
    public static IParse<T> Resolve<T>()
    {
        if (_parsers.TryGetValue(typeof(T), out var boxed))
            return (IParse<T>)boxed;
        throw new NotSupportedException($"No parser for {typeof(T)}");
    }
}
