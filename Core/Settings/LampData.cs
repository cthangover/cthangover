using System;

namespace Cthangover.Core.Settings
{
	[Serializable]
	public class LampData
	{
		public float Radius    { get; set; } = 300f;
		public float Influence { get; set; } = 1f;
	}
}
