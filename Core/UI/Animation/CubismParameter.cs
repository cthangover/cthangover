using Godot;

namespace Live2D.Cubism.Core
{
    /// <summary>
    /// Minimal stub mirroring Live2D Cubism SDK's CubismParameter contract.
    /// Exists solely to prevent compilation errors when Live2D assemblies are absent
    /// — provides Value, MinimumValue, and MaximumValue properties as no-op placeholders.
    /// Part of the opt-in Live2D support strategy: real functionality requires the SDK.
    /// </summary>
    public partial class CubismParameter : Node
    {
        public float Value { get; set; }
        public float MinimumValue { get; set; } = -30f;
        public float MaximumValue { get; set; } = 30f;
    }
}
