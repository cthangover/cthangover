using System.Linq;
using Cthangover.Core.Games;

namespace Engine.Tests.Games
{
    /// <summary>
    /// The launch args decide what the boot loads: one game (<c>--game</c>),
    /// a save of it or a new run, extra mods, or every mod.
    /// </summary>
    public class GameLaunchArgsTests
    {
        [Fact]
        public void Parse_GameWithSave()
        {
            var args = GameLaunchArgs.Parse(new[] { "--game=card_novel", "--load=slot_3" });

            Assert.True(args.SelectsGame);
            Assert.Equal("card_novel", args.GameId);
            Assert.Equal("slot_3", args.LoadSave);
            Assert.False(args.NewGame);
        }

        [Fact]
        public void Parse_AllModsOverridesGame()
        {
            var args = GameLaunchArgs.Parse(new[] { "--game=card_novel", "--all-mods" });

            Assert.False(args.SelectsGame);
        }

        [Fact]
        public void Parse_NoArgs_SelectsNothing()
        {
            var args = GameLaunchArgs.Parse(new string[0]);

            Assert.False(args.SelectsGame);
            Assert.Null(args.GameId);
            Assert.Empty(args.WithMods);
        }

        [Fact]
        public void Parse_WithMods_SplitsAndTrims()
        {
            var args = GameLaunchArgs.Parse(new[] { "--game=g", "--with-mods= test_runner , validator" });

            Assert.Equal(new[] { "test_runner", "validator" }, args.WithMods.ToArray());
        }

        [Theory]
        [InlineData(new string[0], GameLaunchMode.Frontend)]
        [InlineData(new[] { "--scene=home_kitchen" }, GameLaunchMode.AllMods)]
        [InlineData(new[] { "--run-tests" }, GameLaunchMode.AllMods)]
        [InlineData(new[] { "--game=g", "--timeout=30" }, GameLaunchMode.Game)]
        [InlineData(new[] { "--frontend", "--itest=menu_flow" }, GameLaunchMode.Frontend)]
        [InlineData(new[] { "--game=g", "--all-mods" }, GameLaunchMode.AllMods)]
        public void Mode_PlainLaunchIsFrontend_DevSwitchesLoadEverything(string[] args, GameLaunchMode expected)
        {
            Assert.Equal(expected, GameLaunchArgs.Parse(args).Mode);
        }

        [Theory]
        [InlineData(null, "--new")]
        [InlineData("slot_2", "--load=slot_2")]
        public void Build_RoundTrips(string save, string expectedSecond)
        {
            var built = GameLaunchArgs.Build("card_novel", save);
            var parsed = GameLaunchArgs.Parse(built);

            Assert.Equal(new[] { "--game=card_novel", expectedSecond }, built.ToArray());
            Assert.Equal("card_novel", parsed.GameId);
            Assert.Equal(save, parsed.LoadSave);
        }

        [Fact]
        public void WithSelection_Game_ReplacesTheSelection_KeepsDevSwitches()
        {
            var running = GameLaunchArgs.Parse(new[] { "--game=card_novel", "--new", "--itest=new_game", "--with-mods=validator" });

            var next = running.WithSelection("cthangover", "slot_2");

            Assert.Equal(GameLaunchMode.Game, next.Mode);
            Assert.Equal("cthangover", next.GameId);
            Assert.Equal("slot_2", next.LoadSave);
            Assert.False(next.NewGame);
            Assert.Contains("--itest=new_game", next.UserArgs);
            Assert.Equal(new[] { "validator" }, next.WithMods.ToArray());
            Assert.DoesNotContain("--game=card_novel", next.UserArgs);
        }

        [Fact]
        public void WithSelection_NewGame_WhenNoSaveIsGiven()
        {
            var next = GameLaunchArgs.Parse(new string[0]).WithSelection("cthangover");

            Assert.True(next.NewGame);
            Assert.Null(next.LoadSave);
        }

        [Fact]
        public void WithSelection_NullGame_IsTheFrontEnd_EvenWithDevSwitches()
        {
            var running = GameLaunchArgs.Parse(new[] { "--all-mods", "--itest=menu_flow" });

            var next = running.WithSelection(null);

            Assert.Equal(GameLaunchMode.Frontend, next.Mode);
            Assert.True(next.ForceFrontend);
            Assert.False(next.AllMods);
            Assert.Contains("--itest=menu_flow", next.UserArgs);
        }

        [Fact]
        public void SetCurrent_MakesTheSelectionProcessWide()
        {
            var selection = GameLaunchArgs.Parse(new[] { "--game=card_novel", "--new" });
            try
            {
                GameLaunchArgs.SetCurrent(selection);

                Assert.Same(selection, GameLaunchArgs.Current);
            }
            finally
            {
                GameLaunchArgs.SetCurrent(null);
            }
        }
    }
}
