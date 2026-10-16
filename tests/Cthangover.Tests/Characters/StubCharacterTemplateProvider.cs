using System.Collections.Generic;
using Cthangover.Core.Characters;

namespace Engine.Tests.Characters
{
    /// <summary>
    /// Deterministic <see cref="ICharacterTemplateProvider"/> for unit tests
    /// around the party roster. Every ID resolves to a small template with
    /// preset stats, except the IDs listed in <see cref="UnknownIds"/> — they
    /// model the "typo in scenario content" case that the refusal contract
    /// in <see cref="CharacterData.AddCharacterToParty"/> must survive.
    /// Copy semantics of the real factory are irrelevant here: the roster
    /// code treats the returned <see cref="Character"/> as read-only source
    /// data for the runtime card.
    /// </summary>
    public class StubCharacterTemplateProvider : ICharacterTemplateProvider
    {
        /// <summary>IDs that resolve to null — missing declarations or typos.</summary>
        public HashSet<string> UnknownIds { get; } = new HashSet<string>();

        /// <summary>Health value stamped into every declared template; tests assert it reaches the roster card.</summary>
        public const int TemplateHealth = 42;

        /// <inheritdoc />
        public Character Get(string id)
        {
            if (UnknownIds.Contains(id))
                return null;

            return new Character
            {
                ID = id,
                Name = id,
                Level = 3,
                Exp = 30,
                Attributes = new CharacterAttributes
                {
                    Health = new Cthangover.Core.Characters.Attribute { Value = TemplateHealth, BaseValue = TemplateHealth },
                },
            };
        }
    }
}
