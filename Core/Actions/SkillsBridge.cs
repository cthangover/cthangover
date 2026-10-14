using System;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Core-side bridge for skill grants. The grant logic itself (resolving the
    /// definition and pairing the skill with its granted action card) lives in
    /// the skills_base engine mod; the kernel only knows this delegate slot.
    /// The mod installs its implementation through <see cref="Register"/> when
    /// the mod pipeline reaches <c>IModInitializer.OnModResourcesReady</c>.
    /// Registration is first-wins: reloading the mod re-runs its static
    /// initializers, and a second delegate must never stack on top of the
    /// first. While no sink is bound (kernel built without the skills mod,
    /// or the call happens before mods load) grants are rejected with a
    /// warning instead of throwing, so a scenario cannot crash the runtime.
    /// </summary>
    public static class SkillsBridge
    {
        private static readonly object Gate = new();
        private static Func<string, bool> _grantSkill;

        /// <summary>Installs the grant implementation; first registration wins, re-registrations are logged and ignored.</summary>
        public static void Register(Func<string, bool> grantSkill)
        {
            if (grantSkill == null)
                return;

            lock (Gate)
            {
                if (_grantSkill != null)
                {
                    GameLogger.Log("SKILL", "SkillsBridge: grant sink already registered; ignoring re-registration", LogLevel.Warning);
                    return;
                }

                _grantSkill = grantSkill;
            }
        }

        /// <summary>
        /// Grants a skill through the bound sink. Returns <c>false</c> when no
        /// skills mod is loaded (with a warning) or when the sink rejects the
        /// ID — mirroring the old direct <c>SkillService.GrantSkill</c> result.
        /// </summary>
        public static bool Grant(string id)
        {
            Func<string, bool> sink;
            lock (Gate)
                sink = _grantSkill;

            if (sink == null)
            {
                GameLogger.Log("SKILL", $"SkillsBridge: no grant sink bound (skills_base not loaded); skill '{id}' rejected", LogLevel.Warning);
                return false;
            }

            return sink(id);
        }
    }
}
