using Cthangover.Core.Scenes;

namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// Shared <see cref="ModScenePrefab"/> handles for the character panel's
    /// scene-built parts (cards, slots, the pool). Godot cannot load files
    /// inside <c>mods/</c> directly, so every instantiation of these UI pieces
    /// goes through the runtime tscn pipeline; the handles are created once and
    /// reused so repeated <see cref="ModScenePrefab.Instantiate{T}"/> calls stay
    /// cheap cache hits in <see cref="TscnScenes"/>.
    /// </summary>
    public static class CharacterPanelPrefabs
    {
        private static ModScenePrefab _card;
        private static ModScenePrefab _slot;
        private static ModScenePrefab _pool;
        private static ModScenePrefab _characterCard;

        /// <summary>Prefab of the action card (<c>scenes/ui/action_card.tscn</c>).</summary>
        public static ModScenePrefab Card => _card ??= new ModScenePrefab("scenes/ui/action_card.tscn");

        /// <summary>Prefab of the action slot (<c>scenes/ui/action_slot.tscn</c>).</summary>
        public static ModScenePrefab Slot => _slot ??= new ModScenePrefab("scenes/ui/action_slot.tscn");

        /// <summary>Prefab of the action pool column (<c>scenes/ui/action_pool.tscn</c>).</summary>
        public static ModScenePrefab Pool => _pool ??= new ModScenePrefab("scenes/ui/action_pool.tscn");

        /// <summary>Prefab of the character card (<c>scenes/ui/character_card.tscn</c>).</summary>
        public static ModScenePrefab CharacterCard => _characterCard ??= new ModScenePrefab("scenes/ui/character_card.tscn");

        /// <summary>Instantiates a fresh action card from its scene; null when the scene cannot be resolved.</summary>
        public static ActionCardBehaviour InstantiateCard() => Card.Instantiate<ActionCardBehaviour>();

        /// <summary>Instantiates a fresh action slot from its scene; null when the scene cannot be resolved.</summary>
        public static ActionSlotBehaviour InstantiateSlot() => Slot.Instantiate<ActionSlotBehaviour>();

        /// <summary>Instantiates a fresh character card from its scene; null when the scene cannot be resolved.</summary>
        public static CharacterCardBehaviour CardInstance() => CharacterCard.Instantiate<CharacterCardBehaviour>();
    }
}
