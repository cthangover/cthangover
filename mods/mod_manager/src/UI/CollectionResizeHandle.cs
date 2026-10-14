using System;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
namespace Mods.ModManager
{
    /// <summary>
    /// Visible resize grip in the bottom-right corner of a scheme group. The
    /// mockup showed three diagonal stripes there; in Godot they are painted
    /// through the <c>"draw"</c> signal because virtual overrides are unreliable
    /// in mod assemblies, and the control itself only reports raw GuiInput to its
    /// <see cref="CollectionGroupCard"/> owner.
    /// </summary>
    public partial class CollectionResizeHandle : ModWidget
    {
        /// <summary>Grip side in pixels; the card positions this control at its bottom-right corner.</summary>
        public const int HandleSize = 18;

        public CollectionResizeHandle()
        {
            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(HandleSize, HandleSize);
            Size = new Vector2(HandleSize, HandleSize);
            MouseDefaultCursorShape = CursorShape.Fdiagsize;
        }

        protected override void Construct()
        {
            Connect("draw", Callable.From((Action)DrawStripes));
        }

        private void DrawStripes()
        {
            for (var i = 0; i < 3; i++)
            {
                var offset = 5f + i * 5f;
                DrawLine(
                    new Vector2(Size.X - offset, Size.Y),
                    new Vector2(Size.X, Size.Y - offset),
                    UiTokens.LineStrong,
                    1.5f,
                    true);
            }
        }
    }
}
