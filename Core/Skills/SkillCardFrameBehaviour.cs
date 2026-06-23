using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.Skills
{
    public partial class SkillCardFrameBehaviour : Control
    {
        [Export] private Godot.Collections.Array<Color> rareList;
        [Export] private TextureRect img;
        [Export] private RareType type;
        
        public void Init(RareType type)
        {
            this.type = type;
            if (rareList != null && (int)type < rareList.Count)
                SetColor(rareList[(int)type]);
        }
        
        public void SetColor(Color color)
        {
            if (img != null)
                img.Modulate = color;
        }
        
#if TOOLS
        public override void _ValidateProperty(Godot.Collections.Dictionary property)
        {
            base._ValidateProperty(property);
            if (rareList != null && (int)type < rareList.Count)
                SetColor(rareList[(int)type]);
        }
#endif
    }
}
