using System;
using System.Collections.Generic;
using Cthangover.Core.Actions;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods;

public class ModGDScriptLoaderTests
{
    [Fact]
    public void LoadGDScripts_NoGdSources_DoesNotThrow()
    {
        var mods = new Dictionary<string, IModInfo>
        {
            ["no_gd"] = new ModInfo
            {
                Id = "no_gd",
                Manifest = new ModManifest
                {
                    Id = "no_gd",
                    GdSources = null
                },
                FileProvider = new MockModFileProvider("no_gd")
            }
        };

        var ex = Record.Exception(() => ModGDScriptLoader.LoadGDScripts(mods));
        Assert.Null(ex);
    }

    [Fact]
    public void LoadGDScripts_EmptyGdSources_DoesNotThrow()
    {
        var mods = new Dictionary<string, IModInfo>
        {
            ["empty"] = new ModInfo
            {
                Id = "empty",
                Manifest = new ModManifest
                {
                    Id = "empty",
                    GdSources = new List<string>()
                },
                FileProvider = new MockModFileProvider("empty")
            }
        };

        var ex = Record.Exception(() => ModGDScriptLoader.LoadGDScripts(mods));
        Assert.Null(ex);
    }

    [Fact]
    public void LoadGDScripts_NullManifest_DoesNotThrow()
    {
        var mods = new Dictionary<string, IModInfo>
        {
            ["no_manifest"] = new ModInfo
            {
                Id = "no_manifest",
                Manifest = null,
                FileProvider = new MockModFileProvider("no_manifest")
            }
        };

        var ex = Record.Exception(() => ModGDScriptLoader.LoadGDScripts(mods));
        Assert.Null(ex);
    }

    [Fact]
    public void LoadGDScripts_MultipleMods_SomeWithGdSources()
    {
        var mods = new Dictionary<string, IModInfo>
        {
            ["base"] = new ModInfo
            {
                Id = "base",
                Manifest = new ModManifest { Id = "base", GdSources = null },
                FileProvider = new MockModFileProvider("base")
            },
            ["has_gd"] = new ModInfo
            {
                Id = "has_gd",
                Manifest = new ModManifest
                {
                    Id = "has_gd",
                    GdSources = new List<string> { "scripts/*.gd" }
                },
                FileProvider = new MockModFileProvider("has_gd")
            },
            ["also_null"] = new ModInfo
            {
                Id = "also_null",
                Manifest = new ModManifest { Id = "also_null" },
                FileProvider = new MockModFileProvider("also_null")
            }
        };

        var ex = Record.Exception(() => ModGDScriptLoader.LoadGDScripts(mods));
        Assert.Null(ex);
    }

    [Fact]
    public void GDScriptScenarioAction_RegistersInFactory()
    {
        var action = new GDScriptScenarioAction("gd.test.action", null);
        ScenarioActionFactory.Instance.Register(action);

        var found = ScenarioActionFactory.Instance.Get("gd.test.action");
        Assert.NotNull(found);
        Assert.Equal("gd.test.action", found.Name);
        Assert.IsType<GDScriptScenarioAction>(found);
    }

    [Fact]
    public void GDScriptScenarioAction_RegisterDuplicateName_LaterReplaces()
    {
        var action1 = new GDScriptScenarioAction("gd.dup.action", null);
        var action2 = new GDScriptScenarioAction("gd.dup.action", null);

        ScenarioActionFactory.Instance.Register(action1);
        ScenarioActionFactory.Instance.Register(action2);

        var found = ScenarioActionFactory.Instance.Get("gd.dup.action");
        Assert.NotNull(found);
        Assert.Same(action2, found);
    }

    [Fact]
    public void GDScriptModInitializer_ImplementsInterface()
    {
        var init = new GDScriptModInitializer(null);

        Assert.IsAssignableFrom<IModInitializer>(init);
    }
}
