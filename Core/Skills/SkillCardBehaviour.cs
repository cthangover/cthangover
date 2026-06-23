using Cthangover.Core.UI.Base.Lists;
using Godot;

namespace Cthangover.Core.Skills
{
    public partial class SkillCardBehaviour : ListItem<SkillInfo>
    {
        [Export] private SkillCardFrameBehaviour frame;
        [Export] private Label txtName;
        [Export] private TextureRect imgCard;

        public override void Construct(SkillInfo skillInfo)
        {
            base.Construct(skillInfo);
            imgCard.Texture = skillInfo.Sprite;
            txtName.Text = TranslationServer.Translate(skillInfo.Name);
            frame.Init(skillInfo.RareType);
        }

        public override void Destruct()
        {
            QueueFree();
        }
    }
}
