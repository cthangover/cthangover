using System;
using System.Collections.Generic;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Generic JSON wrapper for mod data files. Every JSON file in a mod's
    /// resource groups follows the convention <c>{ "Items": [ ... ] }</c>
    /// — an outer object containing an array of typed entries. This class
    /// is the deserialization target for that envelope, and is marked
    /// <c>internal</c> because callers consume the already-unwrapped
    /// <c>Dictionary&lt;string, T&gt;</c> from <c>ModManager.CollectJsonGroup</c>.
    /// </summary>
    [Serializable]
    internal class FileData<T> where T : class, IIdentifiable
    {
        /// <summary>The typed entries inside the {"Items": [...]} JSON envelope.</summary>
        public List<T> Items { get; set; }
    }
}
