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
        /// <summary>Current parameter value. Range is clamped by <see cref="MinimumValue"/> and <see cref="MaximumValue"/> in the real SDK; unrestricted in this stub.</summary>
        public float Value { get; set; }
        /// <summary>Lower bound for parameter range. Defaults to -30 to match Cubism SDK conventions.</summary>
        public float MinimumValue { get; set; } = -30f;
        /// <summary>Upper bound for parameter range. Defaults to 30 to match Cubism SDK conventions.</summary>
        public float MaximumValue { get; set; } = 30f;
    }
}
