using Cthangover.Core.Actions;

namespace Engine.Tests.Actions;

public class GDScriptScenarioActionTests
{
    [Fact]
    public void Constructor_StoresName()
    {
        var action = new GDScriptScenarioAction("my.test.action", null);

        Assert.Equal("my.test.action", action.Name);
    }

    [Fact]
    public void Name_ReturnsStoredValue()
    {
        var action = new GDScriptScenarioAction("gds.greet", null);

        var name = action.Name;

        Assert.Equal("gds.greet", name);
    }

    [Fact]
    public void Action_Implements_IScenarioAction()
    {
        var action = new GDScriptScenarioAction("test", null);

        Assert.IsAssignableFrom<IScenarioAction>(action);
    }

    [Fact]
    public void Constructor_AllowsNullGodotObject()
    {
        var action = new GDScriptScenarioAction("null_obj_test", null);

        Assert.Equal("null_obj_test", action.Name);
    }
}
