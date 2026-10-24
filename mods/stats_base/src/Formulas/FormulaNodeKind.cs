namespace Cthangover.Mods.Stats.Formulas
{
    /// <summary>What a <see cref="FormulaNode"/> computes.</summary>
    public enum FormulaNodeKind
    {
        /// <summary>A number literal (<see cref="FormulaNode.Value"/>).</summary>
        Number,

        /// <summary>A named value supplied by the scope (<see cref="FormulaNode.Name"/>): a stat, <c>level</c>, <c>a.atk</c>.</summary>
        Variable,

        /// <summary>A prefix operator over one child: <c>-</c>, <c>+</c>, <c>!</c>.</summary>
        Unary,

        /// <summary>An infix operator over two children (<see cref="FormulaNode.Op"/>).</summary>
        Binary,

        /// <summary><c>condition ? then : else</c> over three children.</summary>
        Conditional,

        /// <summary>A function call (<see cref="FormulaNode.Name"/>) over its argument children.</summary>
        Call
    }
}
