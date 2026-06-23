using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Battle;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Quests;
using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.Core.Settings
{
    public class BattleData
    {

        public QuestBase Quest  { get; set; }
        public string    NewTag { get; set; }

		public List<Character> EnemiesCards { get; private set; }
		public Texture2D Background { get; private set; }
		public Texture2D DepthMap { get; set; }
		public Texture2D AlbedoMap { get; set; }
		public BattleSide BattleSide { get; private set; } = BattleSide.Player;
        public string ActiveBattleCore { get; set; }
        public string ReturnScene { get; private set; }

        private BattleData()
        { }

        public static BattleData InitBattle(Texture2D background, string returnScene, params string[] enemies)
        {
            return InitBattle(background, returnScene, BattleSide.Player, enemies);
        }
        
        public static BattleData InitBattle(Texture2D background, string returnScene, BattleSide cardOrder = BattleSide.Player, params string[] enemies)
        {
            var list = enemies.Select(o => CharacterFactory.Instance.Get(o) ?? new Character
            {
                ID = o,
                Attributes = new CharacterAttributes
                {
                    Health = new Attribute { Value = 20, BaseValue = 20 },
                    Point = new Attribute { Value = 3, BaseValue = 3 },
                },
                Level = 1,
                Exp = 10,
            }).ToList();
            return InitBattle(background, returnScene, list, cardOrder);
        }
        
        public static BattleData InitBattle(Texture2D background, string returnScene, List<Character> enemiesCards, BattleSide cardOrder = BattleSide.Player)
        {
            return new BattleData
            {
                Background   = background,
                ReturnScene  = returnScene,
                EnemiesCards = enemiesCards,
                BattleSide    = cardOrder,
            };
        }
    }
}
