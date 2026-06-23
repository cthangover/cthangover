using System;
using System.Collections.Generic;

namespace Cthangover.Core.Utils
{
    
    public static class Reflections
    {

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
