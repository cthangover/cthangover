using System;

namespace Cthangover.Core.Scenes.Parsing
{
    /// <summary>
    /// Parses a binary or unary operator token into an <see cref="ICondition"/> subtree.
    /// Registered in <see cref="OperatorRegistry"/> — mods can add custom operators
    /// (e.g. XOR, NAND) by implementing this interface and calling
    /// <see cref="OperatorRegistry.Register"/> during mod initialisation.
    ///
    /// <see cref="Precedence"/> controls binding strength: higher values mean
    /// looser binding. In <c>A && B || C</c>, <c>||</c> has higher precedence (binds
    /// looser) than <c>&&</c>, so the tree is <c>||(&&(A, B), C)</c>.
    ///
    /// For infix operators (<c>&&</c>, <c>||</c>), <see cref="ExpressionParser"/>
    /// calls <see cref="BuildInfix"/> with left and right operands. For prefix
    /// operators (<c>!</c>), it calls <see cref="BuildPrefix"/> with the single
    /// operand.
    /// </summary>
    public interface IOperatorParser
    {
        /// <summary>The token type this parser handles.</summary>
        ConditionParser.TokenType Token { get; }

        /// <summary>Binding precedence — higher = looser binding (||=40, &&=30, !=50).</summary>
        int Precedence { get; }

        /// <summary>True for prefix unary operators (!A).</summary>
        bool IsPrefix { get; }

        /// <summary>True for infix binary operators (A && B).</summary>
        bool IsInfix { get; }

        /// <summary>Constructs an infix node from left and right operands. Returns null when <see cref="IsInfix"/> is false.</summary>
        ICondition BuildInfix(ICondition left, ICondition right);

        /// <summary>Constructs a prefix node from its operand. Returns null when <see cref="IsPrefix"/> is false.</summary>
        ICondition BuildPrefix(ICondition operand);
    }
}
