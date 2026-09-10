using System;
using Cthangover.Core.Cards.StatusEffect;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>FileFactory</c> for status effect metadata — duration, stack
    /// behaviour, visual icon, etc. Works in tandem with
    /// <c>StatusEffectActionFactory</c>: this factory provides the <b>data</b>
    /// (what the effect is), while the action factory provides the
    /// <b>behaviour</b> (what the effect does each turn).
    /// </summary>
    public class StatusEffectInfoFactory : FileFactory<StatusEffectInfo>
    {
        private static readonly Lazy<StatusEffectInfoFactory> lazy = new(() => new StatusEffectInfoFactory());
        public static StatusEffectInfoFactory Instance => lazy.Value;
        public override string GroupName => "status_effects";
    }
}
