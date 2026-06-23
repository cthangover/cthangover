using System;

namespace Cthangover.Core.Audio
{

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
