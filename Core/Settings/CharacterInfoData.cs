using Cthangover.Core.Characters;

namespace Cthangover.Core.Settings
{

    public class CharacterInfoData
    {
        public string         ID { get; set; }
        public CharacterType  CharacterType { get; set; }
        public int            Level { get; set; }
        public int            Exp { get; set; }
        public CharacterAttributes Attributes { get; set; }
    }

}
