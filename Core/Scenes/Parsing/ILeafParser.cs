using System;

namespace Cthangover.Core.Scenes.Parsing
{
    /// <summary>
    /// Parses a leaf value from the token stream — a literal, a flag comparison,
    /// a quest method call, or any other terminal expression that produces an
    /// <see cref="ICondition"/>.
    ///
    /// Registered in <see cref="LeafParserRegistry"/>. Mods add custom leaf types
    /// (e.g. weather conditions, skill checks) by implementing this interface and
    /// calling <see cref="LeafParserRegistry.Register"/>.
    ///
    /// <see cref="CanParse"/> receives a <see cref="TokenPeeker"/> — a read-only
    /// view of upcoming tokens — and must return true only when this parser can
    /// handle the input. No tokens are consumed during the check.
    ///
    /// <see cref="Parse"/> receives a mutable stream and the
    /// <paramref name="parseSubExpr"/> callback for recursive sub-expression
    /// parsing (used when a leaf contains parentheses, e.g. flag arguments that
    /// are themselves expressions).
    /// </summary>
    public interface ILeafParser
    {
        /// <summary>
        /// Checks whether this parser can handle the tokens at the current position.
        /// Peek only — do not advance the stream.
        /// </summary>
        bool CanParse(TokenPeeker peeker);

        /// <summary>
        /// Consumes tokens from the stream and returns the parsed <see cref="ICondition"/>.
        /// </summary>
        /// <param name="peeker">Mutable token stream — advance past consumed tokens.</param>
        /// <param name="parseSubExpr">Callback for nested sub-expression parsing, e.g. content inside parentheses.</param>
        ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr);
    }
}
