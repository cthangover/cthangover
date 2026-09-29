using System.Collections.Generic;
using Cthangover.Core.Actions;

namespace Engine.Tests.Actions;

public class MockMapZoneService : IMapZoneService
{
    public List<(string Operation, string Id)> Calls { get; } = new();

    public bool IsOpen(string id) => false;

    public bool IsVisible(string id) => false;

    public void Open(string id) => Calls.Add(("open", id));

    public void Close(string id) => Calls.Add(("close", id));

    public void Show(string id) => Calls.Add(("show", id));

    public void Hide(string id) => Calls.Add(("hide", id));
}
