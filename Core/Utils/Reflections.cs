using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
        public static ICollection<T> FindAndCreate<T>(Assembly assemblyFilter = null, Predicate<Type> filter = null)
        {
            var list = new LinkedList<T>();
            var typeT = typeof(T);

            if (assemblyFilter == null)
            {
                foreach (var assembly in LoadedAssemblies())
                    CheckAssembly(typeT, list, assembly, filter);
            }
            else
            {
                CheckAssembly(typeT, list, assemblyFilter, filter);
            }

            return list;
        }

        /// <summary>
        /// The assemblies of the running game: <see cref="AppDomain.GetAssemblies"/>
        /// without those of released mod contexts, which linger there until
        /// collected (see <see cref="Mods.ModLoadContext.IsReleased"/>). Every
        /// "all loaded assemblies" scan of the game uses this instead of the
        /// AppDomain directly.
        /// </summary>
        public static List<Assembly> LoadedAssemblies()
        {
            var result = new List<Assembly>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!Mods.ModLoadContext.IsReleasedAssembly(assembly))
                    result.Add(assembly);
            }
            return result;
        }

        private static void CheckAssembly<T>(Type typeT, LinkedList<T> list, Assembly assemblyFilter, Predicate<Type> filter = null)
        {
            Type[] types;
            try
            {
                types = assemblyFilter.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
                GameLogger.Log("REFLECTIONS",
                    $"FindAndCreate<{typeT.Name}>: assembly '{assemblyFilter.GetName().Name}' partially loaded, continuing with {types.Length} type(s)",
                    LogLevel.Warning);
            }
            catch (Exception ex)
            {
                GameLogger.Log("REFLECTIONS",
                    $"FindAndCreate<{typeT.Name}>: cannot enumerate types of '{assemblyFilter.FullName}': {ex.Message}",
                    LogLevel.Warning);
                return;
            }

            foreach (var type in types)
            {
                if (!typeT.IsAssignableFrom(type) || !type.IsPublic || type.IsAbstract
                    || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                if (filter != null && !filter.Invoke(type))
                    continue;

                try
                {
                    list.AddLast((T)Activator.CreateInstance(type));
                }
                catch (Exception ex)
                {
                    GameLogger.Log("REFLECTIONS",
                        $"FindAndCreate<{typeT.Name}>: failed to instantiate '{type.FullName}': {ex.Message}",
                        LogLevel.Warning);
                }
            }
        }

    }
    
}
