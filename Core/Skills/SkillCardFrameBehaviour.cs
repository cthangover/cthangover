using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.Skills
{
    /// <summary>
    /// Controls the tinted visual frame drawn behind each skill card. The frame
    /// colour is determined by the card's <see cref="RareType"/>: an
    /// <see cref="rareList"/> array, configured in the Godot editor, maps each
    /// rarity tier to a specific <see cref="Color"/>. The selected colour is
    /// applied by modulating the exported <see cref="TextureRect"/> node.
    /// </summary>
    public partial class SkillCardFrameBehaviour : Control
    {
        [Export] private Godot.Collections.Array<Color> rareList;
        [Export] private TextureRect img;
        [Export] private RareType type;

        /// <summary>
        /// Selects and applies the rarity colour for this frame. Looks up the
        /// colour at index <c>(int)type</c> in <see cref="rareList"/>, then delegates
        /// to <see cref="SetColor"/> to modulate the frame texture.
        /// </summary>
        /// <param name="type">The rarity tier to colour-match.</param>
        public void Init(RareType type)
        {
            this.type = type;
            if (rareList != null && (int)type < rareList.Count)
                SetColor(rareList[(int)type]);
        }

        /// <summary>
        /// Applies a modulation colour to the frame's <see cref="TextureRect"/>,
        /// effectively tinting the border artwork. If the image node is not assigned
        /// this call is a no-op.
        /// </summary>
        /// <param name="color">The tint colour to apply to <see cref="img"/>.</param>
		public void SetColor(Color color)
		{
			if (img == null)
				img = GetNodeOrNull<TextureRect>("FrameImg");
			if (img != null)
				img.Modulate = color;
		}

#if TOOLS
        /// <summary>
        /// Godot editor-only hook that re-applies the rarity colour when
        /// properties change in the inspector, keeping the editor preview in sync
        /// with the exported <see cref="rareList"/> and <see cref="type"/> values.
        /// </summary>
        public override void _ValidateProperty(Godot.Collections.Dictionary property)
        {
            base._ValidateProperty(property);
            if (rareList != null && (int)type < rareList.Count)
                SetColor(rareList[(int)type]);
        }
#endif
    }
}
