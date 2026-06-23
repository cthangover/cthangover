namespace Cthangover.Core.Scenes
{
    public class UnaryExpr : ConditionNode
    {
        public string Op { get; }
        public ConditionNode Operand { get; }

        public UnaryExpr(string op, ConditionNode operand)
        {
            Op = op;
            Operand = operand;
        }
    }
}
