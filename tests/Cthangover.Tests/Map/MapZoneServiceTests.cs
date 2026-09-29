using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Events;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;
using Godot;

namespace Engine.Tests.Map
{
    [Collection("ConditionStateMonitor")]
    public class MapZoneServiceTests : IDisposable
    {
        public MapZoneServiceTests()
        {
            ResetService();
        }

        public void Dispose()
        {
            ResetService();
        }

        private static void ResetService()
        {
            MapZoneService.Instance.ResetOverrides();
            MapZoneService.Instance.LoadZones(new List<MapZone>());
        }

        private static MapZone MakeZone(string id, bool locked = false, bool hidden = false)
        {
            return new MapZone
            {
                ID = id,
                Title = $"zone/{id}",
                Scene = $"scene_{id}",
                Locked = locked,
                Hidden = hidden,
            };
        }

        [Fact]
        public void LoadZones_IndexesById()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a"), MakeZone("b") });

            Assert.Equal(2, MapZoneService.Instance.Zones.Count);
            Assert.NotNull(MapZoneService.Instance.Get("a"));
            Assert.NotNull(MapZoneService.Instance.Get("B"));
            Assert.Null(MapZoneService.Instance.Get("missing"));
        }

        [Fact]
        public void LoadZones_DuplicateId_KeepsFirst()
        {
            MapZoneService.Instance.LoadZones(new[]
            {
                MakeZone("dup"),
                MakeZone("dup", locked: true),
            });

            Assert.Single(MapZoneService.Instance.Zones);
            Assert.False(MapZoneService.Instance.Get("dup").Locked);
        }

        [Fact]
        public void IsOpen_UsesAuthoringDefault()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("open"), MakeZone("closed", locked: true) });

            Assert.True(MapZoneService.Instance.IsOpen("open"));
            Assert.False(MapZoneService.Instance.IsOpen("closed"));
        }

        [Fact]
        public void IsVisible_UsesAuthoringDefault()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("shown"), MakeZone("secret", hidden: true) });

            Assert.True(MapZoneService.Instance.IsVisible("shown"));
            Assert.False(MapZoneService.Instance.IsVisible("secret"));
        }

        [Fact]
        public void OpenClose_OverrideAuthoringDefault()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a", locked: true) });

            MapZoneService.Instance.Open("a");
            Assert.True(MapZoneService.Instance.IsOpen("a"));

            MapZoneService.Instance.Close("a");
            Assert.False(MapZoneService.Instance.IsOpen("a"));
        }

        [Fact]
        public void ShowHide_OverrideAuthoringDefault()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a", hidden: true) });

            MapZoneService.Instance.Show("a");
            Assert.True(MapZoneService.Instance.IsVisible("a"));

            MapZoneService.Instance.Hide("a");
            Assert.False(MapZoneService.Instance.IsVisible("a"));
        }

        [Fact]
        public void UnknownZone_IsClosedAndInvisible()
        {
            Assert.False(MapZoneService.Instance.IsOpen("ghost"));
            Assert.False(MapZoneService.Instance.IsVisible("ghost"));
        }

        [Fact]
        public void UnknownZone_Open_DoesNotThrowAndStaysClosed()
        {
            MapZoneService.Instance.Open("ghost");

            Assert.False(MapZoneService.Instance.IsOpen("ghost"));
        }

        [Fact]
        public void ResetZone_ReturnsToAuthoringDefault()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a", locked: true, hidden: true) });
            MapZoneService.Instance.Open("a");
            MapZoneService.Instance.Show("a");

            MapZoneService.Instance.ResetZone("a");

            Assert.False(MapZoneService.Instance.IsOpen("a"));
            Assert.False(MapZoneService.Instance.IsVisible("a"));
        }

        [Fact]
        public void ResetOverrides_ReturnsAllToAuthoringDefaults()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a", locked: true), MakeZone("b", hidden: true) });
            MapZoneService.Instance.Open("a");
            MapZoneService.Instance.Show("b");

            MapZoneService.Instance.ResetOverrides();

            Assert.False(MapZoneService.Instance.IsOpen("a"));
            Assert.False(MapZoneService.Instance.IsVisible("b"));
        }

        [Fact]
        public void ReloadFromMods_OutsideRuntime_KeepsOverrides()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a", locked: true) });
            MapZoneService.Instance.Open("a");

            MapZoneService.Instance.ReloadFromMods();

            Assert.True(MapZoneService.Instance.IsOpen("a"));
        }

        [Fact]
        public void Reload_OutsideRuntime_KeepsOverrides()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a", locked: true) });
            MapZoneService.Instance.Open("a");

            MapZoneService.Instance.Reload();

            Assert.True(MapZoneService.Instance.IsOpen("a"));
        }

        [Fact]
        public void ExportImport_Roundtrip()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a"), MakeZone("b") });
            MapZoneService.Instance.Close("a");
            MapZoneService.Instance.Hide("b");

            var exported = MapZoneService.Instance.ExportState();

            MapZoneService.Instance.ResetOverrides();
            MapZoneService.Instance.ImportState(exported);

            Assert.False(MapZoneService.Instance.IsOpen("a"));
            Assert.False(MapZoneService.Instance.IsVisible("b"));
        }

        [Fact]
        public void Import_ReplacesPreviousOverrides()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a"), MakeZone("b") });
            MapZoneService.Instance.Open("a");
            MapZoneService.Instance.Hide("b");

            var data = new MapZoneStateData();
            MapZoneService.Instance.ImportState(data);

            Assert.True(MapZoneService.Instance.IsOpen("a"));
            Assert.True(MapZoneService.Instance.IsVisible("b"));
        }

        [Fact]
        public void Import_NullData_ResetsOverrides()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a") });
            MapZoneService.Instance.Close("a");

            MapZoneService.Instance.ImportState(null);

            Assert.True(MapZoneService.Instance.IsOpen("a"));
        }

        [Fact]
        public void StateChanges_PublishEvent()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a") });
            var events = new List<MapZoneChangedEvent>();
            Action<MapZoneChangedEvent> handler = events.Add;
            ModEventBus.Subscribe(handler);

            try
            {
                MapZoneService.Instance.Open("a");
                MapZoneService.Instance.Hide("a");
            }
            finally
            {
                ModEventBus.Unsubscribe(handler);
            }

            Assert.Equal(2, events.Count);
            Assert.Equal(MapZoneChangeKind.Open, events[0].Change);
            Assert.Equal("a", events[0].ZoneId);
            Assert.Equal(MapZoneChangeKind.Hide, events[1].Change);
        }

        [Fact]
        public void StateChange_ToSameValue_DoesNotPublish()
        {
            MapZoneService.Instance.LoadZones(new[] { MakeZone("a") });
            var count = 0;
            Action<MapZoneChangedEvent> handler = _ => count++;
            ModEventBus.Subscribe(handler);

            try
            {
                MapZoneService.Instance.Open("a");
                MapZoneService.Instance.Open("a");
            }
            finally
            {
                ModEventBus.Unsubscribe(handler);
            }

            Assert.Equal(1, count);
        }

        [Fact]
        public void MapZone_JsonRoundtrip_WithOptionalFields()
        {
            var original = new MapZone
            {
                ID = "home_forest",
                Points = new List<Vector2> { new(0.1f, 0.2f), new(0.3f, 0.2f), new(0.3f, 0.4f) },
                Title = "ui/map/zone/home_forest",
                Scene = "home_forest",
                Description = "ui/map/zone/home_forest/desc",
                Image = "forest_icon",
                ImagePos = new Vector2(0.2f, 0.3f),
                LabelPos = new Vector2(0.25f, 0.55f),
                Locked = true,
                Hidden = true,
            };

            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteCompactOptions);
            var restored = JsonSerializer.Deserialize<MapZone>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(restored);
            Assert.Equal(original.ID, restored.ID);
            Assert.Equal(original.Title, restored.Title);
            Assert.Equal(original.Scene, restored.Scene);
            Assert.Equal(original.Description, restored.Description);
            Assert.Equal(original.Image, restored.Image);
            Assert.Equal(original.ImagePos, restored.ImagePos);
            Assert.Equal(original.LabelPos, restored.LabelPos);
            Assert.True(restored.Locked);
            Assert.True(restored.Hidden);
            Assert.Equal(original.Points, restored.Points);
        }

        [Fact]
        public void MapZone_JsonRoundtrip_WithoutLabelPos_KeepsNull()
        {
            var original = new MapZone { ID = "plain", ImagePos = new Vector2(0.5f, 0.5f) };

            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteCompactOptions);
            var restored = JsonSerializer.Deserialize<MapZone>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(restored);
            Assert.Null(restored.LabelPos);
            Assert.Equal(original.ImagePos, restored.ImagePos);
        }
    }
}
