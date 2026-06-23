using Godot;

namespace Live2D.Cubism.Core
{
    public partial class CubismParameter : Node
    {
        public float Value { get; set; }
        public float MinimumValue { get; set; } = -30f;
        public float MaximumValue { get; set; } = 30f;
    }
}
