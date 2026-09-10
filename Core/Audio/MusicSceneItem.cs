using System;

namespace Cthangover.Core.Audio
{

    /// <summary>
    /// Serializable scene-to-music mapping with an explicit Index for
    /// ordering. Defaults to <c>Ambient</c> type. Used for serialised
    /// music configs that need a deterministic per-scene track order
    /// rather than random selection.
    /// </summary>
    [Serializable]
    public class MusicSceneItem
    {
        /// <summary>
        /// Asset name used as a key in <c>MusicFactory</c> lookups.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Zero-based ordering index within the scene's track list.
        /// Used for deterministic playback order rather than random
        /// selection.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The music category for this track. Defaults to
        /// <see cref="MusicType.Ambient"/>.
        /// </summary>
        public MusicType Type { get; set; } = MusicType.Ambient;

        /// <summary>
        /// Parameterless constructor for serialisation. Initialises
        /// <c>Name</c> to <see cref="string.Empty"/>.
        /// </summary>
        public MusicSceneItem()
        {
            Name = string.Empty;
        }

        /// <summary>
        /// Constructs an entry with a name, an ordering index, and an
        /// optional music type (defaults to <c>Ambient</c>).
        /// </summary>
        public MusicSceneItem(string name, int index, MusicType type = MusicType.Ambient)
        {
            Name = name;
            Index = index;
            Type = type;
        }
        
        /// <summary>
        /// Returns the item name for debug/log display.
        /// </summary>
        public override string ToString()
        {
            return $"MusicSceneItem: Name={Name}";
        }
    }

}
