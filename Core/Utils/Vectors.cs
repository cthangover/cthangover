using Godot;

namespace Cthangover.Core.Utils
{
    
    public static class Vectors
    {

        public static Vector2 ToVector2(this Vector3 vector)
        {
            return new Vector2(vector.X, vector.Y);
        }
        
    }
    
}
