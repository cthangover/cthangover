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
        [JsonPropertyName("sounds_enabled")]  public bool SoundsEnabled  { get; set; } = true;
        [JsonPropertyName("musics_enabled")]  public bool MusicsEnabled  { get; set; } = true;
        [JsonPropertyName("ambient_enabled")] public bool AmbientEnabled { get; set; } = true;
        [JsonPropertyName("sounds_volume")]   public int  SoundsVolume   { get; set; } = 100;
        [JsonPropertyName("musics_volume")]   public int  MusicsVolume   { get; set; } = 5;
        [JsonPropertyName("ambient_volume")]  public int  AmbientVolume  { get; set; } = 5;
    }
}