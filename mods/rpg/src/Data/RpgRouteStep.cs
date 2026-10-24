using System;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// One step of an <see cref="RpgMoveRoute"/> resolved for the current positions: a
    /// direction (null for <c>wait</c> or an unknown word) and whether the event walks or only
    /// turns. Pure, so routes are tested without a map.
    /// </summary>
    public readonly struct RpgRouteStep
    {
        /// <summary>Creates a step.</summary>
        public RpgRouteStep(RpgDirection? direction, bool moves)
        {
            Direction = direction;
            Moves = moves;
        }

        /// <summary>Where to walk or turn; null = stand.</summary>
        public RpgDirection? Direction { get; }

        /// <summary>True for a walk, false for a turn or a wait.</summary>
        public bool Moves { get; }

        /// <summary>Resolves <paramref name="word"/> (any case) for an event at <paramref name="self"/>.</summary>
        public static RpgRouteStep Resolve(string word, Vector2I self, Vector2I player, Random random)
        {
            var step = (word ?? string.Empty).Trim().ToLowerInvariant();
            switch (step)
            {
                case "up": return new RpgRouteStep(RpgDirection.Up, true);
                case "down": return new RpgRouteStep(RpgDirection.Down, true);
                case "left": return new RpgRouteStep(RpgDirection.Left, true);
                case "right": return new RpgRouteStep(RpgDirection.Right, true);
                case "face_up": return new RpgRouteStep(RpgDirection.Up, false);
                case "face_down": return new RpgRouteStep(RpgDirection.Down, false);
                case "face_left": return new RpgRouteStep(RpgDirection.Left, false);
                case "face_right": return new RpgRouteStep(RpgDirection.Right, false);
                case "random": return new RpgRouteStep((RpgDirection)(random ?? Random.Shared).Next(4), true);
                case "toward": return new RpgRouteStep(Toward(self, player), true);
                case "away": return new RpgRouteStep(Toward(self, player) is { } d ? RpgDirections.Opposite(d) : null, true);
                default: return new RpgRouteStep(null, false);
            }
        }

        /// <summary>The direction that shortens the longer axis of the distance to <paramref name="target"/>; null when already there.</summary>
        public static RpgDirection? Toward(Vector2I self, Vector2I target)
        {
            var delta = target - self;
            if (delta == Vector2I.Zero)
                return null;
            if (Math.Abs(delta.X) >= Math.Abs(delta.Y))
                return delta.X > 0 ? RpgDirection.Right : RpgDirection.Left;
            return delta.Y > 0 ? RpgDirection.Down : RpgDirection.Up;
        }
    }
}
