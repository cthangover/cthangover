using System.Linq;
using System.Reflection;
using Cthangover.Core.Scenes.TscnBuild.PropertyResolvers;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>Auto-discovering ordered list of <see cref="IPropertyResolver"/> implementations. First-match-wins via <see cref="Find"/>. DefaultPropertyResolver is always moved to the end so catch-all doesn't shadow specific resolvers.</summary>
    public static class PropertyResolverRegistry
    {
        private static System.Collections.Generic.List<IPropertyResolver> _resolvers;

        public static void Initialize()
        {
            if (_resolvers != null) return;
            _resolvers = new System.Collections.Generic.List<IPropertyResolver>();
            foreach (var r in Reflections.FindAndCreate<IPropertyResolver>())
                _resolvers.Add(r);
            BumpDefaultToEnd();
        }

        public static void RegisterAssembly(Assembly assembly)
        {
            Initialize();
            foreach (var r in Reflections.FindAndCreate<IPropertyResolver>(assembly))
                _resolvers.Add(r);
            BumpDefaultToEnd();
        }

        private static void BumpDefaultToEnd()
        {
            var idx = _resolvers.FindIndex(r => r is DefaultPropertyResolver);
            if (idx >= 0)
            {
                var d = _resolvers[idx];
                _resolvers.RemoveAt(idx);
                _resolvers.Add(d);
            }
        }

        public static IPropertyResolver Find(string key, object value) { Initialize(); foreach (var r in _resolvers) if (r.CanResolve(key, value)) return r; return null; }
    }
}
