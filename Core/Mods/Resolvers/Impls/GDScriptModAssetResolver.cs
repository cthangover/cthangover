using Godot;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Bridge that wraps a GDScript object as an
    /// <see cref="IModAssetResolver"/>. Enables GDScript mods to provide
    /// custom asset resolvers without writing C# — the GDScript object
    /// must expose <c>resolver_id</c> as a property and may implement
    /// <c>initialize()</c> and <c>clear_cache()</c> methods.
    ///
    /// Unlike C# resolvers discovered via reflection, GDScript resolvers
    /// are manually instantiated and registered by
    /// <see cref="ModGDScriptLoader"/>.
    /// </summary>
    public class GDScriptModAssetResolver : IModAssetResolver
    {
        private readonly GodotObject _instance;

        /// <summary>
        /// The resolver ID, read from the GDScript object's
        /// <c>resolver_id</c> property. Used as the key in the resolver
        /// dictionary inside <see cref="ModManager"/>.
        /// </summary>
        public string Id => (string)_instance.Get("resolver_id");

        /// <summary>
        /// Creates a bridge wrapping the given GDScript object. The
        /// object must have a <c>resolver_id</c> property and may
        /// optionally implement <c>initialize</c> and <c>clear_cache</c>.
        /// </summary>
        public GDScriptModAssetResolver(GodotObject instance)
        {
            _instance = instance;
        }

        /// <summary>
        /// Calls the GDScript object's <c>initialize()</c> method if
        /// present. Unlike the C# interface, the context is not passed
        /// to GDScript — GDScript resolvers access mod data through
        /// their own mechanisms.
        /// </summary>
        public void Initialize(ModResolutionContext context)
        {
            if (_instance.HasMethod("initialize"))
                _instance.Call("initialize");
        }

        /// <summary>
        /// Calls the GDScript object's <c>clear_cache()</c> method if
        /// present.
        /// </summary>
        public void ClearCache()
        {
            if (_instance.HasMethod("clear_cache"))
                _instance.Call("clear_cache");
        }
    }
}
