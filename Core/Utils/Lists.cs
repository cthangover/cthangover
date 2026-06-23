using System.Collections.Generic;

namespace Cthangover.Core.Utils
{
    public static class Lists
    {

        public static ICollection<T> Singleton<T>(T item)
        {
            ICollection<T> list = new LinkedList<T>();
            list.Add(item);
            return list;
        }
        
        public static bool IsEmpty<T>(ICollection<T> collection)
        {
            if (collection == null)
                return true;
            return collection.Count == 0;
        }

        public static bool IsNotEmpty<T>(ICollection<T> collection)
        {
            return !IsEmpty(collection);
        }
    }
}
