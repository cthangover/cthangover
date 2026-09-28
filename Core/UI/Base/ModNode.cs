using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Lifecycle forwarder for Node scripts compiled from mods. Godot's C#
    /// source generator only runs in the main game assembly, so virtual methods
    /// overridden inside a runtime-compiled mod (e.g. <c>_Ready</c>, <c>_Process</c>)
    /// are never registered with the engine and therefore never called. A mod node
    /// must instead derive from a core base class — the engine drives the base
    /// (which has bindings) and the base forwards every lifecycle callback to
    /// virtual <c>OnXxx</c> methods the mod can override.
    /// <para/>
    /// The same trick is used by <see cref="InputHandlerNode"/> for input events
    /// and by <see cref="ModWidget"/> for Controls. Use this wrapper for plain
    /// <see cref="Node"/> scripts that need enter/exit/ready/process hooks.
    /// </summary>
    public partial class ModNode : Node
    {
        public override void _EnterTree() => OnEnterTree();

        public override void _ExitTree() => OnExitTree();

        public override void _Ready() => OnReady();

        public override void _Process(double delta) => OnProcess(delta);

        /// <summary>Forwarded from <see cref="Node._EnterTree"/>. Override in mod nodes.</summary>
        protected virtual void OnEnterTree() { }

        /// <summary>Forwarded from <see cref="Node._ExitTree"/>. Override in mod nodes.</summary>
        protected virtual void OnExitTree() { }

        /// <summary>Forwarded from <see cref="Node._Ready"/>. Override in mod nodes.</summary>
        protected virtual void OnReady() { }

        /// <summary>Forwarded from <see cref="Node._Process"/>. Override in mod nodes.</summary>
        protected virtual void OnProcess(double delta) { }
    }
}
