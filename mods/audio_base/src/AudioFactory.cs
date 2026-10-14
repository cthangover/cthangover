using System;
using System.Collections.Generic;
using Cthangover.Core.Factories;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Audio
{
    /// <summary>
    /// Base factory for loading audio streams from mod archives.
    /// <c>.ogg</c> files are parsed through <c>OggPacketParser</c> to
    /// produce <c>AudioStreamOggVorbis</c> (Godot needs the raw packet
    /// sequence, not just file bytes). <c>.wav</c> files are loaded
    /// directly into <c>AudioStreamWav</c>. Inherits the mod-aware
    /// caching and fallback logic from <c>PrefabFactory</c>.
    /// </summary>
    public abstract class AudioFactory : PrefabFactory<AudioStream>
    {
        protected AudioFactory(string factoryKey, int fallbackCacheSize)
            : base(factoryKey, fallbackCacheSize) { }

        /// <summary>Audio extensions from mod config (typically .ogg and .wav), tried in order.</summary>
        protected override List<string> Extensions { get; } = ModConfig.Instance.AudioExtensions;

        /// <summary>Builds an <c>AudioStreamOggVorbis</c> from parsed OGG packets, or an <c>AudioStreamWav</c> from raw PCM bytes.</summary>
        protected override AudioStream ConvertFromBytes(string id, byte[] data, string extension)
        {
            if (extension?.Equals(".ogg", StringComparison.OrdinalIgnoreCase) == true)
            {
                var packetSequence = OggPacketParser.CreateFromOggBytes(data);
                if (packetSequence == null)
                {
                    GameLogger.Log("FACTORY", $"Failed to parse OGG data for sound '{id}'", LogLevel.Error);
                    return null;
                }
                return new AudioStreamOggVorbis { PacketSequence = packetSequence };
            }

            if (extension?.Equals(".wav", StringComparison.OrdinalIgnoreCase) == true)
                return new AudioStreamWav { Data = data };

            GameLogger.Log("FACTORY", $"Unsupported audio format '{extension}' for sound '{id}'", LogLevel.Error);
            return null;
        }

    }
}
