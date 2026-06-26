using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Cards.StatusEffect
{

    /// <summary>
    /// Runtime instance of a status effect. Constructed from
    /// StatusEffectInfo with the Actions behaviour resolved via
    /// StatusEffectActionFactory. Icon is loaded from the "characters"
    /// mod group via TextureUtils — effects share the same icon pool
    /// as characters. Copy() is a shallow clone of primitives and
    /// references (Actions is a shared singleton, not cloned) suitable
    /// for StatusEffectQueue duplication.
    /// </summary>
    public class StatusEffectItem : IStatusEffect
    {
        public string ID { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int Duration { get; set; }
        public int RemainingTurns { get; set; }
        public StatusEffectType EffectType { get; private set; }
        public Texture2D Icon { get; private set; }
        public IStatusActions Actions { get; private set; }

        public StatusEffectItem(StatusEffectInfo info)
        {
            ID   = info.ID;
            Name = info.Name;
            Description = info.Description;
            Duration = info.Duration;
            EffectType = info.Type;
            Icon = TextureUtils.LoadFromModGroup("characters", info.Icon);
            Actions = StatusEffectActionFactory.Instance.Get(info.Actions);
        }

        public StatusEffectItem() { }
        
        public IStatusEffect Copy()
        {
            return new StatusEffectItem
            {
                ID             = ID,
                Name           = Name,
                Description    = Description,
                Duration       = Duration,
                RemainingTurns = RemainingTurns,
                EffectType     = EffectType,
                Icon           = Icon,
                Actions        = Actions,
            };
        }
    }
}
