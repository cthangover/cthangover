namespace Cthangover.Core.Scenes
{
    public class BinaryExpr : ConditionNode
    {
        public ConditionNode Left { get; }
        public string Op { get; }
        public ConditionNode Right { get; }

        public BinaryExpr(ConditionNode left, string op, ConditionNode right)
        {
            Left = left;
            Op = op;
            Right = right;
        }
    }
}
