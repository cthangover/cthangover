using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Factory for full character definitions. Returns a <b>copy</b> of
    /// the cached <c>Character</c> on every <c>Get</c> call because
    /// <c>Character.Attributes</c> mutate during gameplay.
    ///
    /// Character actions are resolved through
    /// <c>ActionCharacterFactory</c>. Attribute fields that are missing
    /// or zero in JSON receive nonzero fallback values so incomplete
    /// mod data doesn't produce unplayable characters.
    ///
    /// Patching is handled transparently by
    /// <c>ModManager.CollectJsonGroup</c> — the factory receives
    /// already-merged <c>CharacterInfo</c> DTOs and simply maps them to
    /// the runtime model.
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

            _allInfos = JsonGroup.Collect<CharacterInfo>("characters");

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
                Actions = ResolveActions(info.Actions ?? new List<string>()),
                Image = LoadImage(info.Image),
                Loot = info.Loot,
            };

            item.Attributes.Health.Init(info.Health > 0 ? info.Health : 1);
            item.Attributes.Defence.Init(info.Defence);
            item.Attributes.Attack.Init(info.Attack);
            item.Attributes.Point.Init(info.Point > 0 ? info.Point : 1);

            PropertyData.ImportJsonProperties(item.Attributes.Properties, info.ExtraProperties);

            return item;
        }

        private static List<ActionCharacter> ResolveActions(List<string> actions)
        {
            if (actions == null || actions.Count == 0)
                return new List<ActionCharacter>();

            return actions
                .Select(id => ActionCharacterFactory.Instance.Get(id))
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
