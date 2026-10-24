using System;
using System.Collections.Generic;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Picks the active page of an event and builds the event as that page shows it. Page
    /// index 0 is the event's own fields (active while the event's own condition holds),
    /// 1… are <see cref="RpgMapEvent.Pages"/>; the last page whose condition holds wins, -1
    /// means none holds and the event is hidden. Pure: the caller passes the condition check.
    /// </summary>
    public static class RpgEventPages
    {
        /// <summary>The index of the active page, or -1.</summary>
        public static int ActiveIndex(RpgMapEvent mapEvent, Func<string, bool> holds)
        {
            if (mapEvent == null)
                return -1;
            var pages = mapEvent.Pages;
            if (pages != null)
            {
                for (var i = pages.Count - 1; i >= 0; i--)
                {
                    if (pages[i] != null && Holds(pages[i].Condition, holds))
                        return i + 1;
                }
            }
            return Holds(mapEvent.Condition, holds) ? 0 : -1;
        }

        /// <summary>
        /// The event as page <paramref name="index"/> shows it: the event itself for 0, else a
        /// copy with the page's fields over the event's. Null for an index out of range.
        /// </summary>
        public static RpgMapEvent Effective(RpgMapEvent mapEvent, int index)
        {
            if (mapEvent == null || index < 0)
                return null;
            if (index == 0)
                return mapEvent;
            if (mapEvent.Pages == null || index > mapEvent.Pages.Count || mapEvent.Pages[index - 1] == null)
                return null;

            var page = mapEvent.Pages[index - 1];
            var hasLook = page.Sprite != null || page.Tile.HasValue || page.Icon != null;
            return new RpgMapEvent
            {
                Id = mapEvent.Id,
                X = mapEvent.X,
                Y = mapEvent.Y,
                // A page that names any look replaces the whole look, so a chest page with an
                // Icon does not keep the closed chest's Tile underneath it.
                Sprite = hasLook ? page.Sprite : mapEvent.Sprite,
                Tile = hasLook ? page.Tile ?? -1 : mapEvent.Tile,
                Icon = hasLook ? page.Icon : mapEvent.Icon,
                Dir = page.Dir ?? mapEvent.Dir,
                Trigger = page.Trigger ?? mapEvent.Trigger,
                Solid = page.Solid ?? (hasLook ? null : mapEvent.Solid),
                Scenario = page.Scenario ?? mapEvent.Scenario,
                Commands = page.Commands ?? mapEvent.Commands,
                Teleport = page.Teleport ?? mapEvent.Teleport,
                Route = page.Route ?? mapEvent.Route,
                Light = page.Light ?? mapEvent.Light,
                Condition = page.Condition,
                ModId = mapEvent.ModId,
                File = mapEvent.File,
            };
        }

        private static bool Holds(string condition, Func<string, bool> holds)
            => string.IsNullOrWhiteSpace(condition) || (holds != null && holds(condition));
    }
}
