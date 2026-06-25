using System.Collections.Generic;

namespace Cthangover.Core.Mods
{

    public interface IModRegistry
    {
        IReadOnlyDictionary<string, IModInfo> Mods { get; }
    }

}