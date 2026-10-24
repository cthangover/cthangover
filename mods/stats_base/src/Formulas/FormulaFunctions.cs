namespace Cthangover.Mods.Stats.Formulas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The functions a formula may call. The set is closed and small on purpose: a formula
    /// is data an author writes in an editor field, not a script, and every name here is
    /// one a spreadsheet user already knows. The parser rejects unknown names, so a typo
    /// is an authoring error at load time, not a silent 0 in the middle of a battle.
    /// </summary>
    public static class FormulaFunctions
    {
        /// <summary>Function names with their allowed argument counts (min, max; -1 = any).</summary>
        public static readonly IReadOnlyDictionary<string, (int Min, int Max)> Arity = new Dictionary<string, (int, int)>
        {
            ["min"] = (1, -1),
            ["max"] = (1, -1),
            ["floor"] = (1, 1),
            ["ceil"] = (1, 1),
            ["round"] = (1, 1),
            ["abs"] = (1, 1),
            ["sqrt"] = (1, 1),
            ["clamp"] = (3, 3),
            ["rand"] = (2, 2),
            ["chance"] = (1, 1),
            ["if"] = (3, 3)
        };

        /// <summary>Whether <paramref name="name"/> is a function and takes <paramref name="count"/> arguments.</summary>
        public static bool Accepts(string name, int count)
            => Arity.TryGetValue(name, out var arity) && count >= arity.Min && (arity.Max < 0 || count <= arity.Max);

        /// <summary>
        /// Calls a function. <c>rand(lo, hi)</c> is a whole number between the bounds,
        /// both included (swapped bounds are fine); <c>chance(p)</c> is 1 with
        /// <c>p</c> percent probability. Without dice both use the shared generator.
        /// </summary>
        public static double Call(string name, double[] args, Random random)
        {
            switch (name)
            {
                case "min": return args.Min();
                case "max": return args.Max();
                case "floor": return Math.Floor(args[0]);
                case "ceil": return Math.Ceiling(args[0]);
                case "round": return Math.Round(args[0], MidpointRounding.AwayFromZero);
                case "abs": return Math.Abs(args[0]);
                case "sqrt": return args[0] <= 0 ? 0 : Math.Sqrt(args[0]);
                case "clamp": return Math.Clamp(args[0], Math.Min(args[1], args[2]), Math.Max(args[1], args[2]));
                case "if": return args[0] != 0 ? args[1] : args[2];
                case "rand":
                    var lo = (int)Math.Ceiling(Math.Min(args[0], args[1]));
                    var hi = (int)Math.Floor(Math.Max(args[0], args[1]));
                    return hi < lo ? lo : (random ?? Random.Shared).Next(lo, hi + 1);
                case "chance":
                    return (random ?? Random.Shared).NextDouble() * 100 < args[0] ? 1 : 0;
                default:
                    return 0;
            }
        }
    }
}
