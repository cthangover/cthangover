using Godot;

namespace Cthangover.Core.Skills
{
    public partial class SkillCardClickEvent : Control
    {
        [Export] private SkillCardBehaviour card;
        
        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
            {
            }
        }
    }
}
