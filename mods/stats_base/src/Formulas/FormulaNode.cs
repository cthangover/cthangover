namespace Cthangover.Mods.Stats.Formulas
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One node of a parsed formula. A formula is parsed once (<see cref="Formula.Parse"/>
    /// caches the tree per text) and evaluated many times against different scopes, so the
    /// tree is immutable and holds no values of its own beyond literals.
    /// <para/>
    /// Arithmetic is forgiving on purpose, because formulas are written by game authors and
    /// run in the middle of play: division or remainder by zero gives 0, a comparison or a
    /// logical operator gives 1 or 0, and any non-zero value counts as true.
    /// </summary>
    public class FormulaNode
    {
        private static readonly FormulaNode[] NoChildren = Array.Empty<FormulaNode>();

        /// <summary>Creates a node; <paramref name="children"/> may be null for leaves.</summary>
        public FormulaNode(FormulaNodeKind kind, double value = 0, string name = null, string op = null, FormulaNode[] children = null)
        {
            Kind = kind;
            Value = value;
            Name = name;
            Op = op;
            Children = children ?? NoChildren;
        }

        /// <summary>What the node computes.</summary>
        public FormulaNodeKind Kind { get; }

        /// <summary>The literal of a <see cref="FormulaNodeKind.Number"/>.</summary>
        public double Value { get; }

        /// <summary>The variable or function name (lowercase).</summary>
        public string Name { get; }

        /// <summary>The operator of a unary or binary node.</summary>
        public string Op { get; }

        /// <summary>Operands or arguments, in source order.</summary>
        public IReadOnlyList<FormulaNode> Children { get; }

        /// <summary>Computes the node in <paramref name="scope"/>; an unknown name reads as 0.</summary>
        public double Evaluate(IFormulaScope scope)
        {
            switch (Kind)
            {
                case FormulaNodeKind.Number:
                    return Value;
                case FormulaNodeKind.Variable:
                    return scope != null && scope.TryGet(Name, out var v) ? v : 0;
                case FormulaNodeKind.Unary:
                    var operand = Children[0].Evaluate(scope);
                    return Op switch
                    {
                        "-" => -operand,
                        "!" => operand == 0 ? 1 : 0,
                        _ => operand
                    };
                case FormulaNodeKind.Binary:
                    return EvaluateBinary(scope);
                case FormulaNodeKind.Conditional:
                    return Children[0].Evaluate(scope) != 0 ? Children[1].Evaluate(scope) : Children[2].Evaluate(scope);
                case FormulaNodeKind.Call:
                    var args = new double[Children.Count];
                    for (var i = 0; i < args.Length; i++)
                        args[i] = Children[i].Evaluate(scope);
                    return FormulaFunctions.Call(Name, args, scope?.Random);
                default:
                    return 0;
            }
        }

        /// <summary>Every variable name the formula reads, once each, in first-use order (tools list and check them).</summary>
        public List<string> Variables()
        {
            var names = new List<string>();
            Collect(names);
            return names;
        }

        private void Collect(List<string> names)
        {
            if (Kind == FormulaNodeKind.Variable && !names.Contains(Name))
                names.Add(Name);
            foreach (var child in Children)
                child.Collect(names);
        }

        private double EvaluateBinary(IFormulaScope scope)
        {
            // && and || short-circuit, so "b.hp > 0 && 100 / b.hp" never divides by zero needlessly.
            if (Op == "&&")
                return Children[0].Evaluate(scope) != 0 && Children[1].Evaluate(scope) != 0 ? 1 : 0;
            if (Op == "||")
                return Children[0].Evaluate(scope) != 0 || Children[1].Evaluate(scope) != 0 ? 1 : 0;

            var a = Children[0].Evaluate(scope);
            var b = Children[1].Evaluate(scope);
            return Op switch
            {
                "+" => a + b,
                "-" => a - b,
                "*" => a * b,
                "/" => b == 0 ? 0 : a / b,
                "%" => b == 0 ? 0 : a % b,
                "^" => Math.Pow(a, b),
                "<" => a < b ? 1 : 0,
                ">" => a > b ? 1 : 0,
                "<=" => a <= b ? 1 : 0,
                ">=" => a >= b ? 1 : 0,
                "==" => a == b ? 1 : 0,
                "!=" => a != b ? 1 : 0,
                _ => 0
            };
        }
    }
}
