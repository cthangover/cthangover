using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
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
    /// inside the same <c>"characters"</c> asset group as their PNGs,
    /// making co-location of data and art the default mod layout. The JSON
    /// definitions themselves are found by kind, not by that directory.
    /// <para/>
    /// The factory lives in the characters engine mod; its singleton is also
    /// published to the kernel through <see cref="ActionTemplateRegistry"/> by
    /// <c>CharactersModInitializer</c>, so core-side consumers resolve
    /// templates without referencing this assembly.
    /// </summary>
    public class ActionCharacterFactory : ICacheLoader<string, ActionCharacter>, IActionTemplateProvider
    {
        /// <summary>Process-wide singleton for this mod assembly; the public API for action template lookups.</summary>
        public static ActionCharacterFactory Instance { get; } = new ActionCharacterFactory();

        private Dictionary<string, ActionCharacterInfo> _allInfos;
        private readonly BoundedCache<string, ActionCharacter> _cache;

        internal ActionCharacterFactory()
        {
            var size = ModConfig.Instance.Cache.GetCacheSize("actions", 128);
            _cache = new BoundedCache<string, ActionCharacter>(size, this);
        }

        ActionCharacter ICacheLoader<string, ActionCharacter>.Load(string id)
        {
            var infos = EnsureInfos();
            if (infos.TryGetValue(id, out var info))
                return CreateActionCharacter(info);
            return null;
        }

        /// <summary>
        /// Returns the action definitions, collecting them on first use.
        /// Publication to <c>_allInfos</c> waits until the mod registry is
        /// finalized so an early lookup from the launcher boot window cannot
        /// freeze an empty snapshot for the session.
        /// </summary>
        private Dictionary<string, ActionCharacterInfo> EnsureInfos()
        {
            // The resolver's dictionary identity is the data snapshot (see CharacterFactory.EnsureInfos).
            var infos = Entities.Collect<ActionCharacterInfo>();
            if (!ReferenceEquals(infos, _allInfos))
            {
                _allInfos = infos;
                _cache.Clear();
            }
            return infos;
        }

        /// <summary>
        /// Returns an independent copy of the action definition for
        /// <paramref name="id"/>, or <c>null</c> when unknown. The copy is
        /// deliberate: action cards carry mutable battle state, so callers
        /// never share one instance.
        /// </summary>
        public ActionCharacter Get(string id)
        {
            EnsureInfos();
            var item = _cache.Get(id);
            return item?.Copy();
        }

        /// <summary>
        /// Returns all known action info IDs. Used by the character panel to determine
        /// which actions are not assigned to any character (the action pool).
        /// </summary>
        public IReadOnlyCollection<string> GetAllActionIds()
        {
            return EnsureInfos()?.Keys;
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
