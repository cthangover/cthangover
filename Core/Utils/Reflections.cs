using System;
using System.Collections.Generic;

namespace Cthangover.Core.Utils
{
    
    /// <summary>
    /// Reflection-based discovery utility that scans the current <see cref="AppDomain"/>
    /// for concrete implementations of a given type and instantiates them. This is
    /// the backbone of the project's plugin-discovery mechanism: mod behaviours,
    /// JSON converters, and command handlers register themselves simply by being
    /// public, non-abstract classes implementing a known interface or base class.
    /// </summary>
    /// <remarks>
    /// The returned collection uses <see cref="LinkedList{T}"/> for O(1) append
    /// during the scan. Callers may copy to a <see cref="List{T}"/> if random access
    /// is needed downstream.
    /// </remarks>
    public static class Reflections
    {
        /// <summary>
        /// Walks every loaded assembly in the current <see cref="AppDomain"/>,
        /// finds every public, non-abstract class assignable to <typeparamref name="T"/>,
        /// creates a parameterless instance via <see cref="Activator.CreateInstance(Type)"/>,
        /// and appends it to the result list.
        /// </summary>
        /// <typeparam name="T">The base type or interface to search for.</typeparam>
        /// <returns>A collection of freshly-constructed instances in discovery order.</returns>
        public static ICollection<T> FindAndCreate<T>()
        {
            var list = new LinkedList<T>();
            var typeT = typeof(T);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeT.IsAssignableFrom(type) && type.IsPublic && !type.IsAbstract)
                    {
                        list.AddLast((T)Activator.CreateInstance(type));
                    }
                }
            }
            return list;
        }

    }
    
}
