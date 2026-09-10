namespace Cthangover.Core.Interactive
{
	/// <summary>Enumeration of supported hit-area shapes for interactive objects.</summary>
	public enum HitAreaType
	{
		/// <summary>Full rectangular area matching the control bounds.</summary>
		Rect,

		/// <summary>Circular area centred within the control bounds.</summary>
		Circle,

		/// <summary>Custom polygon defined by vertex array.</summary>
		Polygon
	}
}
