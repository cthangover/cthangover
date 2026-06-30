using System;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Settings.Configs
{
    /// <summary>
    /// Serialisable audio preferences with independent volume (0–100, integer)
    /// and enabled toggle for each bus. Defaults: sounds at full volume,
    /// music and ambient at 5% — deliberate imbalance so SFX punch through
    /// by default while background layers are subtle.
    /// </summary>
    [Serializable]
    public class AudioSection
    {
        /// <summary>Master toggle for the SFX audio bus.</summary>
        [JsonPropertyName("sounds_enabled")]  public bool SoundsEnabled  { get; set; } = true;
        /// <summary>Master toggle for the music audio bus.</summary>
        [JsonPropertyName("musics_enabled")]  public bool MusicsEnabled  { get; set; } = true;
        /// <summary>Master toggle for the ambient/background audio bus.</summary>
        [JsonPropertyName("ambient_enabled")] public bool AmbientEnabled { get; set; } = true;
        /// <summary>SFX volume, integer 0–100 (default 100).</summary>
        [JsonPropertyName("sounds_volume")]   public int  SoundsVolume   { get; set; } = 100;
        /// <summary>Music volume, integer 0–100 (default 5).</summary>
        [JsonPropertyName("musics_volume")]   public int  MusicsVolume   { get; set; } = 5;
        /// <summary>Ambient volume, integer 0–100 (default 5).</summary>
        [JsonPropertyName("ambient_volume")]  public int  AmbientVolume  { get; set; } = 5;
    }
}