using Godot;

namespace Cthangover.Core.UI.View
{
    /// <summary>
    /// Static utility that calculates content positioning offsets for the 9-point
    /// AlignType grid. Uses the viewport's visible rect as the camera/view area
    /// and computes offsets so the content aligns correctly within it. The
    /// CenterCenter case negates X because the content position is treated as
    /// a negative offset from top-left in Godot's coordinate space.
    /// </summary>
    public static class ViewBoxNavigator
    {

        /// <summary>
        /// Calculates the content node position offset for the given 9-point <see cref="AlignType"/>.
        /// Uses the visible viewport rect size as the camera area and computes offsets relative
        /// to the top-left corner. Negative X coordinates are intentional: Godot positions content
        /// relative to the container origin, so aligning center or right requires negative offsets
        /// when content is larger than the viewport.
        /// </summary>
        /// <param name="align">The target alignment from the 9-point grid.</param>
        /// <param name="contentSize">The size of the content node being positioned.</param>
        /// <returns>A position offset that places the content correctly for the chosen alignment.</returns>
        public static Vector2 GetPositionByAlign(AlignType align, Vector2 contentSize)
        {
            var viewport = Godot.Engine.GetMainLoop() as SceneTree;
            var screenSize = viewport?.Root?.GetVisibleRect().Size ?? new Vector2(1920, 1024);
            var cameraSize = screenSize;
            var alignPos = Vector2.Zero;

            switch (align)
            {
                case AlignType.LeftTop:
                    break;
                case AlignType.LeftCenter:
                    alignPos.Y = (contentSize.Y - cameraSize.Y) * 0.5f;
                    break;
                case AlignType.LeftBottom:
                    alignPos.Y = contentSize.Y - cameraSize.Y;
                    break;

                case AlignType.CenterTop:
                    alignPos.X = -(contentSize.X - cameraSize.X) * 0.5f;
                    break;
                case AlignType.CenterCenter:
                    alignPos = (contentSize - cameraSize) * 0.5f;
                    alignPos.X = -alignPos.X;
                    break;
                case AlignType.CenterBottom:
                    alignPos.X = -(contentSize.X - cameraSize.X) * 0.5f;
                    alignPos.Y = contentSize.Y - cameraSize.Y;
                    break;

                case AlignType.RightTop:
                    alignPos.X = -(contentSize.X - cameraSize.X);
                    break;
                case AlignType.RightCenter:
                    alignPos.Y = (contentSize.Y - cameraSize.Y) * 0.5f;
                    alignPos.X = -(contentSize.X - cameraSize.X);
                    break;
                case AlignType.RightBottom:
                    alignPos.X = -(contentSize.X - cameraSize.X);
                    alignPos.Y = contentSize.Y - cameraSize.Y;
                    break;
            }

            return alignPos;
        }

    }

}
