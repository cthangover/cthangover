using System;
using Cthangover.Core.Cards.StatusEffect;

namespace Cthangover.Core.Factories.Impls
{
    public class StatusEffectInfoFactory : FileFactory<StatusEffectInfo>
    {
        private static readonly Lazy<StatusEffectInfoFactory> lazy = new(() => new StatusEffectInfoFactory());
        public static StatusEffectInfoFactory Instance => lazy.Value;
        public override string GroupName => "status_effects";
    }
}
