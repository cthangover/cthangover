namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Collects skill declarations. Skills are a standalone
    /// entity kind — they used to share the <c>characters/</c> directory with
    /// characters and actions, which is exactly the ambiguity the kind system
    /// removed; here they are declared under their own kind.
    /// </summary>
    public class SkillEntityStrategy : EntityKindStrategy
    {
        public override string Kind => ValidationKind.Skill;
    }
}
