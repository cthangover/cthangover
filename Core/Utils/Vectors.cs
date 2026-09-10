using Godot;

namespace Cthangover.Core.Utils
{
    
    /// <summary>
    /// Extension helpers for Godot vector types, bridging the gap between
    /// 3D world-space coordinates and 2D UI or gameplay projections.
    /// </summary>
    public static class Vectors
    {
        /// <summary>
        /// Projects a <see cref="Godot.Vector3"/> onto the XY plane by discarding the Z component.
        /// Typically used when converting 3D world positions into 2D screen-space or minimap
        /// coordinates where depth is irrelevant.
        /// </summary>
        public static Vector2 ToVector2(this Vector3 vector)
        {
            return new Vector2(vector.X, vector.Y);
        }
        
    }
    
}
