namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Collects status effect declarations. Definitions name
    /// behaviour sets and icons that are resolved at runtime by the battle mods;
    /// for now the validator records the declaration so unused or duplicated
    /// effects can be reported.
    /// </summary>
    public class StatusEffectEntityStrategy : EntityKindStrategy
    {
        public override string Kind => ValidationKind.StatusEffect;
    }
}
