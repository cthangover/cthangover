using Cthangover.Core.Actions;

namespace Engine.Tests.Actions;

internal class MockLightingService : ILightingService
{
    public bool ClearMapCalled { get; private set; }
    public bool? UseTimeValue { get; private set; }

    public void ClearDepthMap() => ClearMapCalled = true;
    public void SetUseTime(bool useTime) => UseTimeValue = useTime;
}
