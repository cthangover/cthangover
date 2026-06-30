namespace Cthangover.Core.UI.View
{
    /// <summary>
    /// 9-point alignment grid for viewport content positioning. The naming
    /// uses horizontal-first, vertical-second convention (LeftTop, CenterCenter,
    /// RightBottom) — matching the intuitive order for CSS-like layout.
    /// </summary>
    public enum AlignType
    {
        /// <summary>Content aligned to top-left corner of the viewport.</summary>
        LeftTop,
        /// <summary>Content horizontally left, vertically centered in the viewport.</summary>
        LeftCenter,
        /// <summary>Content horizontally left, vertically at the bottom of the viewport.</summary>
        LeftBottom,

        /// <summary>Content horizontally centered, vertically at the top of the viewport.</summary>
        CenterTop,
        /// <summary>Content centered both horizontally and vertically in the viewport.</summary>
        CenterCenter,
        /// <summary>Content horizontally centered, vertically at the bottom of the viewport.</summary>
        CenterBottom,

        /// <summary>Content horizontally right, vertically at the top of the viewport.</summary>
        RightTop,
        /// <summary>Content horizontally right, vertically centered in the viewport.</summary>
        RightCenter,
        /// <summary>Content aligned to the bottom-right corner of the viewport.</summary>
        RightBottom,
    }

}
