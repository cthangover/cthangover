using System;
using System.Collections.Generic;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Mods
{
    [Serializable]
    internal class FileData<T> where T : class, IIdentifiable
    {
        public List<T> Items { get; set; }
    }
}
