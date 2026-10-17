namespace Cthangover.Tests.Mods.Unload
{
    /// <summary>A type and methods of the "dying" assembly in the ownership tests (the test assembly plays the mod).</summary>
    public class OwnedProbe
    {
        public static int Calls;

        public static void Ping() => Calls++;

        public void InstancePing() => Calls++;
    }
}
