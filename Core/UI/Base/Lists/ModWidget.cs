using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Lightweight alternative to Widget for mod-authored controls. Has a simpler
    /// lifecycle — just Construct() on first access and Destruct() on tree exit —
    /// without the visibility interception and Show/Hide pipeline that Widget
    /// carries. Use when the heavy Widget state machine would be overkill or
    /// when the control's visibility is managed by Godot's native system.
    /// </summary>
    public abstract partial class ModWidget : Control
    {
        private bool _constructed;

        /// <summary>Idempotent construction gate: calls <see cref="Construct"/> exactly once, on the first invocation. Thread-safe for repeated external calls.</summary>
        public void EnsureConstructed()
        {
            if (_constructed)
                return;
            _constructed = true;
            
            Construct();
        }

        public override void _ExitTree()
        {
            if (_constructed)
                Destruct();
        }
        
        protected abstract void Construct();
        protected virtual void Destruct() { }
    }
}
