using System;

namespace Cthangover.Core.Settings
{
	/// <summary>
	/// Stores the current state of the lamp light source — its radius and
	/// influence. <see cref="Radius"/> controls the visible light circle
	/// (pixels from the lamp origin); <see cref="Influence"/> modulates
	/// the power (0–1) affecting enemy detection and visibility.
	/// Persisted inside <see cref="SaveData.LampRadius"/> and
	/// <see cref="SaveData.LampInfluence"/> on save.
	/// </summary>
	[Serializable]
	public class LampData
	{
		/// <summary>Visible radius of the lamp light in screen pixels (default 300).</summary>
		public float Radius    { get; set; } = 300f;
		/// <summary>Modulation factor 0–1 affecting how strongly the lamp
		/// reveals enemies and influences stealth mechanics (default 1.0).</summary>
		public float Influence { get; set; } = 1f;
	}
}
