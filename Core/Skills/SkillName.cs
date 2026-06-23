namespace Cthangover.Core.Skills
{
    public enum SkillName : int
    {
        SLAVE_MASTER
    }

    public static class SkillNameExtensions
    {
        public static string ID(this SkillName skill)
        {
            return skill.ToString().ToLower();
        }
    }
}
