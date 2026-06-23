using System;
using System.Collections.Generic;
using Cthangover.Core.Audio;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories
{
    public abstract class AudioFactory : PrefabFactory<AudioStream>
    {
        protected AudioFactory(string factoryKey, int fallbackCacheSize)
            : base(factoryKey, fallbackCacheSize) { }

        protected override List<string> Extensions { get; } = ModConfig.Instance.AudioExtensions;
        
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
