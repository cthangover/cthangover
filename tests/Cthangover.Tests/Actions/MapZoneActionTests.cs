using Cthangover.Core.Actions.Atomic;

namespace Engine.Tests.Actions
{
    public class MapZoneActionTests
    {
        [Fact]
        public void Open_DelegatesZoneId()
        {
            var ctx = new MockActionContext();
            var map = new MockMapZoneService();
            ctx.Map = map;
            ctx.SetParam("zone_id", "home_forest");

            new MapOpenZoneAction().Run(ctx);

            Assert.Equal(("open", "home_forest"), map.Calls[0]);
        }

        [Fact]
        public void Close_DelegatesZoneId()
        {
            var ctx = new MockActionContext();
            var map = new MockMapZoneService();
            ctx.Map = map;
            ctx.SetParam("zone_id", "home_forest");

            new MapCloseZoneAction().Run(ctx);

            Assert.Equal(("close", "home_forest"), map.Calls[0]);
        }

        [Fact]
        public void Show_DelegatesZoneId()
        {
            var ctx = new MockActionContext();
            var map = new MockMapZoneService();
            ctx.Map = map;
            ctx.SetParam("zone_id", "home_forest");

            new MapShowZoneAction().Run(ctx);

            Assert.Equal(("show", "home_forest"), map.Calls[0]);
        }

        [Fact]
        public void Hide_DelegatesZoneId()
        {
            var ctx = new MockActionContext();
            var map = new MockMapZoneService();
            ctx.Map = map;
            ctx.SetParam("zone_id", "home_forest");

            new MapHideZoneAction().Run(ctx);

            Assert.Equal(("hide", "home_forest"), map.Calls[0]);
        }

        [Theory]
        [InlineData("map.open_zone")]
        [InlineData("map.close_zone")]
        [InlineData("map.show_zone")]
        [InlineData("map.hide_zone")]
        public void MissingZoneId_DoesNotCallService(string actionName)
        {
            var ctx = new MockActionContext();
            var map = new MockMapZoneService();
            ctx.Map = map;

            var action = actionName switch
            {
                "map.open_zone" => (Cthangover.Core.Actions.IScenarioAction)new MapOpenZoneAction(),
                "map.close_zone" => new MapCloseZoneAction(),
                "map.show_zone" => new MapShowZoneAction(),
                _ => new MapHideZoneAction(),
            };

            action.Run(ctx);

            Assert.Empty(map.Calls);
            Assert.Contains(ctx.Logs, entry => entry.Category == "EVENT");
        }
    }
}
