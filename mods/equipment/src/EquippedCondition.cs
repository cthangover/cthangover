namespace Cthangover.Mods.Equipment
{
    using Cthangover.Core.Scenes;

    /// <summary><c>equipped(who, item)</c>: true while the character wears the item in any slot.</summary>
    public class EquippedCondition : ConditionNode
    {
        /// <summary>Creates the condition.</summary>
        public EquippedCondition(string who, string item)
        {
            Who = who;
            Item = item;
        }

        /// <summary>Who must wear it (a character id or <c>player</c>).</summary>
        public string Who { get; }

        /// <summary>The item id.</summary>
        public string Item { get; }

        /// <inheritdoc />
        public override bool Evaluate(EvaluationContext ctx) => EquipmentService.Wears(Who, Item);
    }
}
