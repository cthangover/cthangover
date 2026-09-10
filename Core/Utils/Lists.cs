using System.Collections.Generic;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Lightweight collection helpers that avoid LINQ allocations in hot paths.
    /// Designed for the scenario engine's internal state machines and UI
    /// iteration where <c>null</c> collections are semantically equivalent to
    /// empty collections.
    /// </summary>
    public static class Lists
    {
        /// <summary>
        /// Wraps a single element in a <see cref="LinkedList{T}"/> viewable
        /// through the <see cref="ICollection{T}"/> interface. Useful where an
        /// API expects a collection but the caller has only one item — avoids
        /// allocating an array or list with a backing array.
        /// </summary>
        public static ICollection<T> Singleton<T>(T item)
        {
            ICollection<T> list = new LinkedList<T>();
            list.Add(item);
            return list;
        }
        
        /// <summary>
        /// Returns <c>true</c> when <paramref name="collection"/> is <c>null</c>
        /// or has zero elements. This null-safe check eliminates boilerplate
        /// null guards at every iteration site.
        /// </summary>
        public static bool IsEmpty<T>(ICollection<T> collection)
        {
            if (collection == null)
                return true;
            return collection.Count == 0;
        }

        /// <summary>
        /// Negation of <see cref="IsEmpty{T}"/> for more readable guard clauses.
        /// </summary>
        public static bool IsNotEmpty<T>(ICollection<T> collection)
        {
            return !IsEmpty(collection);
        }
    }
}
