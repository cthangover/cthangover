using System.Linq;
using Cthangover.Core.Games;

namespace Engine.Tests.Games
{
    using Cthangover.Core.Games.LaunchParameters;
    using Cthangover.Core.Utils;

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

            Assert.Equal(GameLaunchMode.Game, args.Mode);
            Assert.Equal("card_novel", args.Get<GameParameter>().Value);
            Assert.Equal("slot_3", args.Get<LoadParameter>().Value);
            Assert.False(args.Contains<NewGameParameter>());
        }

        [Fact]
        public void Parse_AllModsOverridesGame()
        {
            var args = GameLaunchArgs.Parse(new[] { "--game=card_novel", "--all-mods" });

            Assert.NotEqual(GameLaunchMode.Game, args.Mode);
        }

        [Fact]
        public void Parse_NoArgs_SelectsNothing()
        {
            var args = GameLaunchArgs.Parse(new string[0]);

            Assert.NotEqual(GameLaunchMode.Game, args.Mode);
            Assert.Null(args.Get<GameParameter>().Value);
            Assert.Empty(args.Get<WithModsParameter>().Value);
        }

        [Fact]
        public void Parse_WithMods_SplitsAndTrims()
        {
            var args = GameLaunchArgs.Parse(new[] { "--game=g", "--with-mods= test_runner , validator" });

            Assert.Equal(new[] { "test_runner", "validator" }, args.Get<WithModsParameter>().Value);
        }

        [Theory]
        [InlineData(new string[0], false, false, false)]
        [InlineData(new[] { "--development-control" }, true, false, false)]
        [InlineData(new[] { "--allow-debug-scripts" }, false, false, true)]
        [InlineData(new[] { "--development-control", "--no-development-control", "--allow-debug-scripts" }, true, true, true)]
        [InlineData(new[] { "--development-control-extra", "--allow-debug-scripts=true" }, false, false, false)]
        public void Parse_CollectionContainsOnlyValidDebugSwitches(string[] input, bool enable, bool disable, bool scripts)
        {
            var args = GameLaunchArgs.Parse(input);

            Assert.Equal(enable, args.Contains<DevelopmentControlParameter>());
            Assert.Equal(disable, args.Contains<NoDevelopmentControlParameter>());
            Assert.Equal(scripts, args.Contains<AllowDebugScriptsParameter>());
            Assert.Equal(GameLaunchMode.Frontend, args.Mode);
        }

        [Fact]
        public void WithSelection_KeepsDebugSwitches()
        {
            var args = GameLaunchArgs.Parse(new[] { "--game=before", "--no-development-control", "--allow-debug-scripts" });
            var next = args.WithSelection("after");

            Assert.True(next.Contains<NoDevelopmentControlParameter>());
            Assert.True(next.Contains<AllowDebugScriptsParameter>());
        }

        [Fact]
        public void ParserPassesAnEntireQuotedValueToTheArgumentConstructor()
        {
            var args = GameLaunchArgs.Parse(new[] { "--atlas-out=\"dir with space=1\"", "--test=true", "--itest=a", "--itest=b,c" });

            Assert.Equal("dir with space=1", args.Get<AtlasOutputParameter>().Value);
            Assert.True(args.Get<TestBooleanParameter>().Value);
            Assert.True(args.Contains<TestBooleanParameter>());
            Assert.True(args.WithArguments(new[] { "--test=false" }).Contains<TestBooleanParameter>());
            Assert.False(new TestBooleanParameter("false").Value);
            Assert.False(args.WithArguments(new[] { "--test=not-a-boolean" }).Get<TestBooleanParameter>().IsSpecified);
            Assert.Equal(new[] { "a", "b", "c" }, args.GetAll<IntegrationTestParameter>().SelectMany(p => p.Value));
            Assert.Contains(Reflections.FindAndCreate<LaunchParameter>(), p => p.Key == "test" && p.Format == "boolean");
        }

        [Fact]
        public void EffectiveArgumentsAreDetachedFromTheirSourceAndFromStartup()
        {
            var original = GameLaunchArgs.Startup;
            var input = new[] { "--with-mods=one,two", "--game=example" };
            var parsed = original.WithArguments(input);
            input[0] = "--with-mods=other";
            var returned = parsed.Get<WithModsParameter>().Value;
            returned[0] = "mutated";

            Assert.Equal(new[] { "one", "two" }, parsed.Get<WithModsParameter>().Value);
            Assert.Equal(new[] { "--with-mods=one,two", "--game=example" }, parsed.UserArgs);
            Assert.Same(original, GameLaunchArgs.Startup);
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
            Assert.Equal("card_novel", parsed.Get<GameParameter>().Value);
            Assert.Equal(save, parsed.Get<LoadParameter>().Value);
        }

        [Fact]
        public void WithSelection_Game_ReplacesTheSelection_KeepsDevSwitches()
        {
            var running = GameLaunchArgs.Parse(new[] { "--game=card_novel", "--new", "--itest=new_game", "--with-mods=validator" });

            var next = running.WithSelection("cthangover", "slot_2");

            Assert.Equal(GameLaunchMode.Game, next.Mode);
            Assert.Equal("cthangover", next.Get<GameParameter>().Value);
            Assert.Equal("slot_2", next.Get<LoadParameter>().Value);
            Assert.False(next.Contains<NewGameParameter>());
            Assert.Contains("--itest=new_game", next.UserArgs);
            Assert.Equal(new[] { "validator" }, next.Get<WithModsParameter>().Value);
            Assert.DoesNotContain("--game=card_novel", next.UserArgs);
        }

        [Fact]
        public void WithSelection_NewGame_WhenNoSaveIsGiven()
        {
            var next = GameLaunchArgs.Parse(new string[0]).WithSelection("cthangover");

            Assert.True(next.Contains<NewGameParameter>());
            Assert.Null(next.Get<LoadParameter>().Value);
        }

        [Fact]
        public void WithSelection_NullGame_IsTheFrontEnd_EvenWithDevSwitches()
        {
            var running = GameLaunchArgs.Parse(new[] { "--all-mods", "--itest=menu_flow" });

            var next = running.WithSelection(null);

            Assert.Equal(GameLaunchMode.Frontend, next.Mode);
            Assert.True(next.Contains<FrontendParameter>());
            Assert.False(next.Contains<AllModsParameter>());
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
