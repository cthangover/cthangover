using Godot;

namespace Live2D.Cubism.Core
{
    /// <summary>
    /// Minimal stub for the Live2D Cubism SDK's CubismModel class.
    /// Provides a no-op ForceUpdateNow() to satisfy code references when the
    /// Live2D runtime is not available. Part of optional Live2D support.
    /// </summary>
    public partial class CubismModel : Node
    {
        public void ForceUpdateNow()
        {
        }
    }
}
