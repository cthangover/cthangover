using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Characters
{
    
    public class ActionCharacter : IIdentifiable
    {
        public const string ATTRIBUTE_REQUIRED_POINT = "RequiredPoint";
        public const string ATTRIBUTE_ATTACK = "Attack";
        public const string ATTRIBUTE_DEFENCE = "Defence";
        public const string ATTRIBUTE_HEAL = "Heal";
        public const string ATTRIBUTE_TURN = "Turn";
        
        public string              ID          { get; set; }
        public string              Name        { get; set; }
        public string              Description { get; set; }
        public ActionCharacterType Type        { get; set; }
        public Texture2D           Image       { get; set; }
        public PropertyData        Properties  { get; set; }

        public string GetStr(string name, string defaultValue = null)
            => Properties.GetStr(name, defaultValue);
        
        public int GetInt(string name, int defaultValue = 0)
            => Properties.GetInt(name, defaultValue);
        
        public float GetFloat(string name, float defaultValue = 0f)
            => Properties.GetFloat(name, defaultValue);
        
        public bool GetBool(string name)
            => Properties.GetBool(name);
        
        public ActionCharacter Copy()
        {
            return new ActionCharacter()
            {
                ID          = ID,
                Name        = Name,
                Description = Description,
                Type        = Type,
                Image       = Image,
                Properties  = Properties?.Clone(),
            };
        }
    }

}
