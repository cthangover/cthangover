namespace Cthangover.Mods.Items
{
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;

    /// <summary>Reads the current bag on evaluation, so saves and new games never leave a cached inventory behind.</summary>
    public class ItemCountCondition : ConditionNode
    {
        private readonly string id;
        private readonly string op;
        private readonly int count;

        /// <summary>Compares the owned count of an item with a whole-number threshold.</summary>
        public ItemCountCondition(string id, string op, int count)
        {
            this.id = id;
            this.op = op;
            this.count = count;
        }

        /// <inheritdoc />
        public override bool Evaluate(EvaluationContext ctx)
            => ConditionCompare.Compare(op, GameData.Instance?.Runtime?.Inventory?.CheckCount(id) ?? 0, count);
    }
}
