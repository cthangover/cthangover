using System;
using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Factory for battle action cards equipped by characters. Returns a
    /// <b>copy</b> of the cached <c>ActionCharacter</c> on every <c>Get</c>
    /// call — action cards carry mutable battle state (cooldowns, modifiers)
    /// and must never be shared across character instances. Properties are
    /// parsed from a compact <c>key=value</c> string list rather than a full
    /// JSON object, keeping mod JSON terse for authored content.
    ///
    /// Images are loaded via <c>TextureUtils.LoadFromModGroup</c> — not
    /// through a texture factory — because action character assets live
    /// inside the same <c>"characters"</c> group as their JSON definitions,
    /// making co-location of data and art the default mod layout.
    /// </summary>
    public class ActionCharacterFactory : ICacheLoader<string, ActionCharacter>
    {
        private static readonly Lazy<ActionCharacterFactory> lazy = new(() => new ActionCharacterFactory());
        public static ActionCharacterFactory Instance => lazy.Value;

        private Dictionary<string, ActionCharacterInfo> _allInfos;
        private readonly BoundedCache<string, ActionCharacter> _cache;

        private ActionCharacterFactory()
        {
            var size = ModConfig.Instance.Cache.GetCacheSize("actions", 128);
            _cache = new BoundedCache<string, ActionCharacter>(size, this);
        }

        ActionCharacter ICacheLoader<string, ActionCharacter>.Load(string id)
        {
            EnsureInfos();
            if (_allInfos.TryGetValue(id, out var info))
                return CreateActionCharacter(info);
            return null;
        }

        private void EnsureInfos()
        {
            if (_allInfos != null)
                return;
            _allInfos = ModManager.Instance.CollectJsonGroup<ActionCharacterInfo>("characters");
        }

        public ActionCharacter Get(string id)
        {
            EnsureInfos();
            var item = _cache.Get(id);
            return item?.Copy();
        }

        private ActionCharacter CreateActionCharacter(ActionCharacterInfo info)
        {
            if (info == null)
                return null;

            var item = new ActionCharacter
            {
                ID = info.ID,
                Name = info.Name,
                Description = info.Description,
                Type = info.Type,
                Properties = ParseValues(info.Values),
                Image = LoadImage(info.Image),
            };

            return item;
        }

        private Texture2D LoadImage(string imagePath)
        {
            return TextureUtils.LoadFromModGroup("characters", imagePath);
        }

        private static PropertyData ParseValues(List<string> values)
        {
            var props = new PropertyData();
            if (values == null)
                return props;

            foreach (var value in values)
            {
                var idx = value.IndexOf('=');
                if (idx > 0)
                    props.Values[value.Substring(0, idx)] = value.Substring(idx + 1);
            }
            return props;
        }
    }
}
