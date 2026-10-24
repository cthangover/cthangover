namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Event pages pick the last page whose condition holds; routes resolve their steps; a page light replaces the event's.</summary>
    public class RpgEventPagesTests
    {
        private static RpgMapEvent Chest() => JsonSerializer.Deserialize<RpgMapEvent>(@"{
            ""Id"": ""chest"", ""X"": 3, ""Y"": 2, ""Tile"": 40, ""Commands"": [""flag chest_open 1""],
            ""Pages"": [
              { ""Condition"": ""chest_open == 1"", ""Icon"": ""rpg_demo/coin"", ""Commands"": [""text \""Empty.\""""] },
              { ""Condition"": ""guard_back == 1"", ""Sprite"": ""village/guard"", ""Dir"": ""left"", ""Solid"": true }
            ]
        }", JsonOptionsProvider.ReadOptions);

        private static Func<string, bool> Flags(params string[] holding) => c => Array.IndexOf(holding, c) >= 0;

        [ModTest("pages/last_holding_page_wins")]
        static void LastHoldingPageWins(ModTestContext ctx)
        {
            var chest = Chest();
            ModAssert.Equal(0, RpgEventPages.ActiveIndex(chest, Flags()), "no page holds: the event itself");
            ModAssert.Equal(1, RpgEventPages.ActiveIndex(chest, Flags("chest_open == 1")), "the opened page");
            ModAssert.Equal(2, RpgEventPages.ActiveIndex(chest, Flags("chest_open == 1", "guard_back == 1")), "the later page wins");
        }

        [ModTest("pages/own_condition_only_turns_off_page_zero")]
        static void OwnConditionOnlyTurnsOffPageZero(ModTestContext ctx)
        {
            var chest = Chest();
            chest.Condition = "chest_here == 1";
            ModAssert.Equal(-1, RpgEventPages.ActiveIndex(chest, Flags()), "nothing holds: hidden");
            ModAssert.Equal(1, RpgEventPages.ActiveIndex(chest, Flags("chest_open == 1")), "a page shows it without the own condition");
            ModAssert.Equal(0, RpgEventPages.ActiveIndex(new RpgMapEvent { Id = "plain" }, Flags()), "an event without pages and condition");
        }

        [ModTest("pages/effective_merges_page_over_event")]
        static void EffectiveMergesPageOverEvent(ModTestContext ctx)
        {
            var chest = Chest();
            ModAssert.True(ReferenceEquals(chest, RpgEventPages.Effective(chest, 0)), "page 0 is the event itself");

            var open = RpgEventPages.Effective(chest, 1);
            ModAssert.Equal("chest", open.Id, "id kept");
            ModAssert.Equal(3, open.X, "cell kept");
            ModAssert.Equal("rpg_demo/coin", open.Icon, "the page's icon");
            ModAssert.Equal(-1, open.Tile, "a page look replaces the whole look");
            ModAssert.Equal("text \"Empty.\"", open.Commands[0], "the page's commands");
            ModAssert.True(open.Trigger == RpgEventTrigger.Action, "trigger inherited");

            var guard = RpgEventPages.Effective(chest, 2);
            ModAssert.Equal("village/guard", guard.Sprite, "charset of page 2");
            ModAssert.True(guard.Dir == RpgDirection.Left, "facing of page 2");
            ModAssert.True(guard.IsSolid, "solid");
            ModAssert.Equal("flag chest_open 1", guard.Commands[0], "commands inherited from the event");
            ModAssert.Null(RpgEventPages.Effective(chest, 3), "out of range");
        }

        [ModTest("routes/steps_resolve")]
        static void StepsResolve(ModTestContext ctx)
        {
            var self = new Vector2I(5, 5);
            var player = new Vector2I(2, 6);
            var left = RpgRouteStep.Resolve("Left", self, player, null);
            ModAssert.True(left.Moves && left.Direction == RpgDirection.Left, "a walk, any case");
            var face = RpgRouteStep.Resolve("face_up", self, player, null);
            ModAssert.True(!face.Moves && face.Direction == RpgDirection.Up, "a turn");
            ModAssert.Null(RpgRouteStep.Resolve("wait", self, player, null).Direction, "wait stands");
            ModAssert.Null(RpgRouteStep.Resolve("dance", self, player, null).Direction, "an unknown word stands");
            ModAssert.True(RpgRouteStep.Resolve("toward", self, player, null).Direction == RpgDirection.Left, "toward along the longer axis");
            ModAssert.True(RpgRouteStep.Resolve("away", self, player, null).Direction == RpgDirection.Right, "away is the opposite");
            ModAssert.Null(RpgRouteStep.Toward(self, self), "already there");
            var random = RpgRouteStep.Resolve("random", self, player, new Random(7));
            ModAssert.True(random.Moves && random.Direction.HasValue, "random walks somewhere");
        }

        [ModTest("routes/json_defaults")]
        static void JsonDefaults(ModTestContext ctx)
        {
            var mapEvent = JsonSerializer.Deserialize<RpgMapEvent>(@"{ ""Id"": ""cat"", ""Route"": { ""Steps"": [""random""] } }", JsonOptionsProvider.ReadOptions);
            ModAssert.CloseTo(1.0, mapEvent.Route.Every, what: "a step a second by default");
            ModAssert.True(mapEvent.Route.Repeat, "repeats by default");
            ModAssert.Null(mapEvent.Pages, "no pages");
        }

        [ModTest("lights/page_light_over_event_light_round_trip")]
        static void PageLightOverEventLight(ModTestContext ctx)
        {
            var lamp = JsonSerializer.Deserialize<RpgMapEvent>(@"{
                ""Id"": ""lamp"", ""Tile"": 12, ""Light"": { ""Radius"": 2.5, ""Color"": ""#ff8040"" },
                ""Pages"": [ { ""Condition"": ""lamp_out == 1"", ""Tile"": 13, ""Light"": { ""Radius"": 1 } }, { ""Condition"": ""door == 1"" } ]
            }", JsonOptionsProvider.ReadOptions);
            ModAssert.CloseTo(2.5, lamp.Light.Radius, what: "the event's light");
            ModAssert.Equal("#ff8040", lamp.Light.Color, "the event's colour");

            var dim = RpgEventPages.Effective(lamp, 1);
            ModAssert.CloseTo(1, dim.Light.Radius, what: "the page's light replaces it");
            ModAssert.Equal("#ffd8a0", dim.Light.Color, "a page light without a colour is warm by default");
            ModAssert.CloseTo(2.5, RpgEventPages.Effective(lamp, 2).Light.Radius, what: "a page without a light keeps the event's");

            var again = JsonSerializer.Deserialize<RpgMapEvent>(JsonSerializer.Serialize(lamp), JsonOptionsProvider.ReadOptions);
            ModAssert.CloseTo(2.5, again.Light.Radius, what: "radius survives the round trip");
            ModAssert.Equal("#ff8040", again.Light.Color, "colour survives the round trip");
            ModAssert.CloseTo(1, again.Pages[0].Light.Radius, what: "the page light survives the round trip");
            ModAssert.Null(JsonSerializer.Deserialize<RpgMapEvent>(@"{ ""Id"": ""dark"" }", JsonOptionsProvider.ReadOptions).Light, "no light by default");
        }
    }
}
