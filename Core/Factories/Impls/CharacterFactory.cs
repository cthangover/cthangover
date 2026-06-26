using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Factory for full character definitions — party members, enemies, NPCs —
    /// each carrying stats, recruited actions, loot tables, and a portrait.
    /// Returns a <b>copy</b> of the cached <c>Character</c> on every <c>Get</c>
    /// call because <c>Character.Attributes</c> mutate during gameplay (health
    /// changes, stat buffs/debuffs). Sharing a single instance would cause
    /// one battle's damage to bleed into every other reference.
    ///
    /// Character actions are stored as comma-separated IDs in JSON and resolved
    /// through <c>ActionCharacterFactory</c> — this two-level dependency chain
    /// keeps the character JSON compact while allowing actions to be authored
    /// and balanced independently. Attribute fields that are missing or zero in
    /// the JSON receive nonzero fallback values (e.g. <c>Fullness = 100</c>,
    /// <c>Health</c> and <c>Point</c> default to 1) so that incomplete mod
    /// data doesn't produce unplayable characters.
    /// </summary>
    public class CharacterFactory : ICacheLoader<string, Character>
    {
        private static readonly Lazy<CharacterFactory> lazy = new(() => new CharacterFactory());
        public static CharacterFactory Instance => lazy.Value;

        private Dictionary<string, CharacterInfo> _allInfos;
        private readonly BoundedCache<string, Character> _cache;

        private CharacterFactory()
        {
            var size = ModConfig.Instance.Cache.GetCacheSize("characters", 128);
            _cache = new BoundedCache<string, Character>(size, this);
        }

        Character ICacheLoader<string, Character>.Load(string id)
        {
            EnsureInfos();

            GameLogger.Log("FACTORY", $"CharacterFactory.Load('{id}'): infos count={_allInfos?.Count ?? -1}", LogLevel.Debug);
            if (_allInfos.TryGetValue(id, out var info))
                return CreateCharacter(info);

            return null;
        }

        private void EnsureInfos()
        {
            if (_allInfos != null)
                return;

            GameLogger.Log("FACTORY", $"CharacterFactory.EnsureInfos: START, ModRegistry.IsInitialized={ModRegistry.Instance.IsInitialized}, mods count={ModRegistry.Instance.Mods?.Count ?? 0}", LogLevel.Debug);

            _allInfos = ModManager.Instance.CollectJsonGroup<CharacterInfo>("characters");

            GameLogger.Log("FACTORY", $"CharacterFactory.EnsureInfos: END, loaded {_allInfos?.Count ?? 0} infos", LogLevel.Debug);
        }

        public Character Get(string id)
        {
            EnsureInfos();

            if (!_allInfos.ContainsKey(id))
            {
                GameLogger.Log("FACTORY", $"CharacterFactory.Get('{id}'): NOT FOUND in infos", LogLevel.Error);
                return null;
            }

            var item = _cache.Get(id);
            if(item?.Image == null)
                GameLogger.Log("FACTORY", $"CharacterFactory.Get('{id}'): found, image=null", LogLevel.Error);
            
            return item?.Copy();
        }

        private static Character CreateCharacter(CharacterInfo info)
        {
            if (info == null)
                return null;

            var item = new Character
            {
                ID = info.ID,
                Behaviour = info.Behaviour,
                Name = info.Name,
                Level = info.Level,
                Exp = info.Exp,
                RecruitmentChance = info.RecruitmentChance,
                Actions = ResolveActions(info.Actions),
                Image = LoadImage(info.Image),
                Loot = info.Loot,
            };

            item.Attributes.Depravity = info.Depravity;
            item.Attributes.Discipline = info.Discipline;
            item.Attributes.Fullness = info.Fullness > 0 ? info.Fullness : 100;
            item.Attributes.Health.Init(info.Health > 0 ? info.Health : 1);
            item.Attributes.Defence.Init(info.Defence);
            item.Attributes.Attack.Init(info.Attack);
            item.Attributes.Strength.Init(info.Strength);
            item.Attributes.Magic.Init(info.Magic);
            item.Attributes.Point.Init(info.Point > 0 ? info.Point : 1);

            return item;
        }

        private static List<ActionCharacter> ResolveActions(string actions)
        {
            if (string.IsNullOrWhiteSpace(actions))
                return new List<ActionCharacter>();

            return actions.Split(',')
                .Select(id => ActionCharacterFactory.Instance.Get(id.Trim()))
                .Where(card => card != null)
                .ToList();
        }

        private static Texture2D LoadImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                GameLogger.Log("FACTORY", $"LoadImage: imagePath is null/empty", LogLevel.Error);
                return null;
            }

            var tex = TextureUtils.LoadFromModGroup("characters", imagePath);
            
            if(tex == null)
                GameLogger.Log("FACTORY", $"LoadImage: result=null", LogLevel.Error);
            return tex;
        }
    }
}
