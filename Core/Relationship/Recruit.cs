using System;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Relationship
{

    [Serializable]
    public class Recruit
    {
        public const string PROP_HEALTH = "Health";

        public string       ID          { get; set; }
        public string       CharacterID { get; set; }
        public PropertyData Properties  { get; set; } = new();
    }

}
