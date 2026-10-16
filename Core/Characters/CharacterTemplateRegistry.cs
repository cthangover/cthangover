using System;
using System.Collections.Generic;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Kernel access point for character template resolution, decoupling the
    /// core from the engine mod that owns the factory
    /// (<c>mods/characters_base</c>). The mod's initializer assigns
    /// <see cref="Provider"/> during <c>IModInitializer.OnModLoaded</c>; all
    /// core-side consumers (<see cref="CharacterData"/>, inventory and skills
    /// UI) resolve templates through <see cref="Get"/>. Before the mod
    /// registers — tests, tools, very early boot — <see cref="Get"/> returns
    /// <c>null</c>, which <see cref="CharacterData.Create"/> already treats as
    /// the unknown-ID fallback path.
    /// <para/>
    /// The registry deliberately exposes two flavors, mirroring the project's
    /// lookup policy: <see cref="Get"/> is the lenient content lookup (a typo
    /// in scenario content degrades UI gracefully), while
    /// <see cref="GetRequired"/> is the fail-fast twin for engine-critical
    /// paths — boot wiring, tools, tests — where continuing without the
    /// template would hide the defect. The same split exists in the scenario
    /// action registry (<c>ScenarioActionFactory.Get/GetRequired</c>) and in
    /// <c>BattleCoreRegistry</c>, which throws on unknown core IDs.
    /// </summary>
    public static class CharacterTemplateRegistry
    {
        /// <summary>Provider published by the characters engine mod; <c>null</c> until the mod loads.</summary>
        public static ICharacterTemplateProvider Provider { get; set; }

        /// <summary>Returns an independent copy of the character template, or <c>null</c> when no provider is registered or the ID is unknown.</summary>
        public static Character Get(string id) => Provider?.Get(id);

        /// <summary>
        /// Resolves a character template and fails fast when it cannot: either
        /// no provider is registered yet (the characters engine mod has not
        /// loaded — a kernel wiring bug, not a content problem) or the ID is
        /// not declared in any loaded mod. Never returns null.
        /// </summary>
        /// <exception cref="InvalidOperationException">No provider registered — <see cref="Provider"/> is still null.</exception>
        /// <exception cref="KeyNotFoundException">The ID has no template declaration.</exception>
        public static Character GetRequired(string id)
        {
            if (Provider == null)
                throw new InvalidOperationException(
                    "CharacterTemplateRegistry has no provider: the characters engine mod has not published CharacterFactory yet.");

            return Provider.Get(id)
                ?? throw new KeyNotFoundException(
                    $"Character template '{id}' is not declared in any loaded mod. " +
                    "Check the Id field in the character JSON or the reference spelling.");
        }
    }
}
