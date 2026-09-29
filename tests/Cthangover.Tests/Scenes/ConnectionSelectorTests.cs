using Cthangover.Core.Scenes.Patching;
using Cthangover.Core.Scenes.TscnParsing;

namespace Engine.Tests.Scenes
{
    public class ConnectionSelectorTests
    {
        private static ParsedConnection Connection(string from, string to, string signal, string method, int flags = 0)
            => new() { From = from, To = to, Signal = signal, Method = method, Flags = flags };

        [Fact]
        public void TryParse_FullSelector_ExtractsEveryField()
        {
            var ok = ConnectionSelector.TryParse(
                "from=MenuContainer/LoadBtn;to=.;signal=pressed;method=_on_pressed;flags=1",
                out var selector, out var error);

            Assert.True(ok, error);
            Assert.Equal("MenuContainer/LoadBtn", selector.From);
            Assert.Equal(".", selector.To);
            Assert.Equal("pressed", selector.Signal);
            Assert.Equal("_on_pressed", selector.Method);
            Assert.Equal(1, selector.Flags);
        }

        [Fact]
        public void TryParse_MinimalSelector_RequiresSignalAndEndpoint()
        {
            Assert.True(ConnectionSelector.TryParse("from=Panel/Btn;signal=pressed", out _, out _));
            Assert.True(ConnectionSelector.TryParse("to=.;signal=pressed", out _, out _));
            Assert.False(ConnectionSelector.TryParse("signal=pressed", out _, out _));
            Assert.False(ConnectionSelector.TryParse("from=Panel/Btn", out _, out _));
        }

        [Fact]
        public void TryParse_InvalidInput_ReportsError()
        {
            Assert.False(ConnectionSelector.TryParse("", out _, out _));
            Assert.False(ConnectionSelector.TryParse("pressed", out _, out _));
            Assert.False(ConnectionSelector.TryParse("bogus=x;from=a;signal=s", out _, out _));
            Assert.False(ConnectionSelector.TryParse("from=a;signal=s;flags=abc", out _, out _));
        }

        [Fact]
        public void Matches_ByFromAndSignal()
        {
            var selector = Parse("from=Panel/Btn;signal=pressed");

            Assert.True(selector.Matches(Connection("Panel/Btn", ".", "pressed", "_on_pressed")));
            Assert.False(selector.Matches(Connection("Panel/Other", ".", "pressed", "_on_pressed")));
            Assert.False(selector.Matches(Connection("Panel/Btn", ".", "toggled", "_on_pressed")));
        }

        [Fact]
        public void Matches_ByToAndFlags()
        {
            var selector = Parse("to=.;signal=toggled;flags=1");

            Assert.True(selector.Matches(Connection("Panel/Btn", ".", "toggled", "_on", 1)));
            Assert.False(selector.Matches(Connection("Panel/Btn", ".", "toggled", "_on", 0)));
        }

        [Fact]
        public void Matches_ByMethodWildcard()
        {
            var selector = Parse("from=Panel/Btn;signal=pressed");

            Assert.True(selector.Matches(Connection("Panel/Btn", ".", "pressed", "whatever")));
        }

        private static ConnectionSelector Parse(string text)
        {
            Assert.True(ConnectionSelector.TryParse(text, out var selector, out var error), error);
            return selector;
        }
    }
}
