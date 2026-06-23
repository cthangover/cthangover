using Godot;

namespace Cthangover.Core.UI
{
    public abstract partial class ModWidget : Control
    {
        private bool _constructed;

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
