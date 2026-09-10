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
        /// <inheritdoc cref="IStatusEffect.ID"/>
        public string ID { get; private set; }
        /// <inheritdoc cref="IStatusEffect.Name"/>
        public string Name { get; private set; }
        /// <inheritdoc cref="IStatusEffect.Description"/>
        public string Description { get; private set; }
        /// <inheritdoc cref="IStatusEffect.Duration"/>
        public int Duration { get; set; }
        /// <inheritdoc cref="IStatusEffect.RemainingTurns"/>
        public int RemainingTurns { get; set; }
        /// <inheritdoc cref="IStatusEffect.EffectType"/>
        public StatusEffectType EffectType { get; private set; }
        /// <inheritdoc cref="IStatusEffect.Icon"/>
        public Texture2D Icon { get; private set; }
        /// <inheritdoc cref="IStatusEffect.Actions"/>
        public IStatusActions Actions { get; private set; }

        /// <summary>Constructs a runtime effect from its JSON-serialisable
        /// definition. Resolves the icon via
        /// <c>TextureUtils.LoadFromModGroup</c> and the action set via
        /// <c>StatusEffectActionFactory</c>.</summary>
        /// <param name="info">Deserialised effect data from a mod
        /// config.</param>
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

        /// <summary>Parameterless constructor for cases where properties are
        /// set manually (e.g. by <see cref="Copy"/> or deserialisation
        /// paths).</summary>
        public StatusEffectItem() { }
        
        /// <summary>Shallow clone that copies primitives and reuses the
        /// shared <c>Actions</c> reference. Used when the queue
        /// duplicates effects during character cloning.</summary>
        /// <returns>A new <see cref="StatusEffectItem"/> with identical
        /// property values.</returns>
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
