using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Cards.StatusEffect
{

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
