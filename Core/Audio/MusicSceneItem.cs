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
        public string Name { get; set; }
        public int Index { get; set; }
        public MusicType Type { get; set; } = MusicType.Ambient;

        public MusicSceneItem()
        {
            Name = string.Empty;
        }

        public MusicSceneItem(string name, int index, MusicType type = MusicType.Ambient)
        {
            Name = name;
            Index = index;
            Type = type;
        }
        
        public override string ToString()
        {
            return $"MusicSceneItem: Name={Name}";
        }
    }

}
