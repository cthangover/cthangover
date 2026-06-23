namespace Cthangover.Core.Scenes
{
    public class FlagCondition : ConditionNode
    {
        public string Key { get; }
        public string Op { get; }
        public string Value { get; }

        public FlagCondition(string key, string op, string value)
        {
            Key = key;
            Op = op;
            Value = value;
        }
    }
}
