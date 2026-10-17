namespace Cthangover.Tests.Mods.Unload
{
    using System;
    using System.Runtime.CompilerServices;
    using Cthangover.Core.Mods.Unload;

    /// <summary>Verification of released contexts through weak references only.</summary>
    public class ModUnloadTicketTests
    {
        private readonly object kept = new();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference Garbage() => new(new object());

        [Fact]
        public void Collect_ReturnsOnlyStillReferencedEntries_InOrder()
        {
            var ticket = new ModUnloadTicket(new[] { ("gone", Garbage()), ("kept", new WeakReference(kept)) });

            Assert.Equal(new[] { "gone", "kept" }, ticket.ModIds);
            Assert.Equal(new[] { "kept" }, ticket.Collect());
            Assert.Equal(new[] { "kept" }, ticket.Alive);
            GC.KeepAlive(kept);
        }

        [Fact]
        public void NullContexts_IsAnEmptyTicket()
        {
            var ticket = new ModUnloadTicket(null);

            Assert.Empty(ticket.ModIds);
            Assert.Empty(ticket.Alive);
            Assert.Empty(ticket.Collect());
        }
    }
}
