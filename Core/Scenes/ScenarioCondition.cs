using System;
using System.Collections.Generic;
using Cthangover.Core.Events;
using Cthangover.Core.Quests;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Evaluates condition expression strings by parsing them into an AST via
    /// <see cref="ConditionParser"/> and walking the tree polymorphically — each
    /// <see cref="ConditionNode"/> subclass implements its own
    /// <see cref="ConditionNode.Evaluate"/> method. Story flags are game state
    /// (<see cref="RuntimeData.Flags"/>, saved and reset with the game); they are
    /// set via <see cref="SetFlag"/> and injected (along with the quest service)
    /// into an <see cref="EvaluationContext"/> carried through the AST walk.
    /// Outside a running game (tests, tools) a detached flag set stands in.
    ///
    /// The polymorphic design means that a mod assembly can define a custom
    /// <see cref="ConditionNode"/> and it will be evaluated automatically when
    /// present in the AST — no dispatch table, no switch, no registration.
    /// </summary>
    public static class ScenarioCondition
    {
        private static readonly Dictionary<string, string> DetachedFlags = new();

        private static Dictionary<string, string> Flags
            => GameEnvironment.IsRuntime && GameData.Instance is { } data ? data.Runtime.Flags : DetachedFlags;
        private static readonly Dictionary<string, ConditionNode> AstCache = new();
        private static readonly object AstLock = new();
        private static readonly EvaluationContext evalCtx = new();

        /// <summary>
        /// Sets a story flag of the running game (the scenario <c>flag</c> command
        /// uses it) so later <see cref="Evaluate"/> calls see it. Publishes a
        /// <see cref="FlagChangedEvent"/> only when the value actually changed
        /// (setting the same value is a no-op); <see cref="ConditionStateMonitor"/>
        /// turns it into a condition rescan, so a waiting scenario can start.
        /// </summary>
        /// <param name="key">The flag name.</param>
        /// <param name="value">The flag value, which may be parsed as integer for numeric comparisons.</param>
        public static void SetFlag(string key, string value)
        {
            var flags = Flags;
            if (flags.TryGetValue(key, out var current) && current == value)
                return;
            flags[key] = value;
            ModEventBus.Publish(new FlagChangedEvent { Key = key, Value = value });
        }

        /// <summary>
        /// Parses and evaluates a condition expression string. Returns <c>true</c> for
        /// null or whitespace-only input (vacuous truth). Returns <c>false</c> on parse
        /// errors. Supports boolean combinators (<c>&amp;&amp;</c>, <c>||</c>, <c>!</c>),
        /// numeric comparisons on flags and quest properties, quest tag membership
        /// checks (<c>hasTag</c>, <c>notHasTag</c>), and boolean literals
        /// (<c>true</c>, <c>false</c>).
        ///
        /// The parsed AST is cached per unique condition string (the tree and its
        /// static inputs are immutable), while execution still reads live flag and
        /// quest state at evaluation time. Parse errors are not cached and re-parse
        /// (and fail) on every call.
        /// </summary>
        /// <param name="condition">The condition expression string to evaluate.</param>
        public static bool Evaluate(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return true;

            ConditionNode ast;
            lock (AstLock)
            {
                if (!AstCache.TryGetValue(condition, out ast))
                {
                    try
                    {
                        ast = ConditionParser.Parse(condition);
                    }
                    catch (FormatException ex)
                    {
                        GameLogger.Log("COND", $"parse error: {ex.Message}", LogLevel.Error);
                        return false;
                    }

                    if (ast == null)
                        return true;

                    AstCache[condition] = ast;
                }
            }

            evalCtx.Flags = Flags;
            evalCtx.QuestFactory = QuestServiceRegistry.Provider;
            return ast.Evaluate(evalCtx);
        }

        /// <summary>
        /// Forgets every cached AST. Leaves of a tree may be nodes of a mod's own
        /// leaf parser (quest checks come from <c>quests_base</c>), so a cached
        /// tree pins that mod's assembly; when the mod unloads, the unload sweep
        /// nulls those references and the cached tree would throw on the next
        /// evaluation. Called from <c>ScenarioConditionCacheConsumer</c> before
        /// any mod assembly unloads; the next <see cref="Evaluate"/> re-parses
        /// with the parsers registered at that moment.
        /// </summary>
        public static void ClearCache()
        {
            lock (AstLock)
                AstCache.Clear();
        }
    }
}
