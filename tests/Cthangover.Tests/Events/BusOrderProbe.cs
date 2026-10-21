namespace Cthangover.Tests.Events
{
    using System.Collections.Generic;

    /// <summary>One subscriber of the ordering test: appends its id to the shared log when called.</summary>
    public class BusOrderProbe
    {
        private readonly int id;
        private readonly List<int> log;

        public BusOrderProbe(int id, List<int> log)
        {
            this.id = id;
            this.log = log;
        }

        public void Handle(BusLifecycleProbeEvent e) => log.Add(id);
    }
}
