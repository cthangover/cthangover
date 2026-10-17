namespace Cthangover.Tests.Mods.Unload
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Unload;

    /// <summary>
    /// The ownership rule of the unload sweepers. The test assembly plays the
    /// dying mod; framework types and methods play the surviving code.
    /// </summary>
    public class AssemblyOwnershipTests
    {
        private static AssemblyOwnership Ownership() => new(new[] { typeof(OwnedProbe).Assembly });

        [Fact]
        public void Owns_AssemblyAndTypes()
        {
            var ownership = Ownership();

            Assert.False(ownership.IsEmpty);
            Assert.Single(ownership.Assemblies);
            Assert.True(ownership.Owns(typeof(OwnedProbe).Assembly));
            Assert.False(ownership.Owns(typeof(string).Assembly));
            Assert.True(ownership.Owns(typeof(OwnedProbe)));
            Assert.False(ownership.Owns(typeof(string)));
        }

        [Fact]
        public void Owns_TypesBuiltFromOwnedTypes()
        {
            var ownership = Ownership();

            Assert.True(ownership.Owns(typeof(List<OwnedProbe>)));
            Assert.True(ownership.Owns(typeof(Dictionary<string, List<OwnedProbe>>)));
            Assert.True(ownership.Owns(typeof(OwnedProbe[])));
            Assert.False(ownership.Owns(typeof(List<string>)));
        }

        [Fact]
        public void Owns_Delegates_ByMethodAndByTarget()
        {
            var ownership = Ownership();
            Action staticOwned = OwnedProbe.Ping;
            Action instanceOwned = new OwnedProbe().InstancePing;
            Action foreign = GC.Collect;

            Assert.True(ownership.Owns(staticOwned));
            Assert.True(ownership.Owns(instanceOwned));
            Assert.False(ownership.Owns(foreign));
            Assert.True(ownership.Owns((Action)Delegate.Combine(foreign, staticOwned)));
            Assert.False(ownership.Owns((Delegate)null));
        }

        [Fact]
        public void OwnsValue_InstancesTypesAssembliesMembers()
        {
            var ownership = Ownership();

            Assert.True(ownership.OwnsValue(new OwnedProbe()));
            Assert.True(ownership.OwnsValue(typeof(OwnedProbe)));
            Assert.True(ownership.OwnsValue(typeof(OwnedProbe).Assembly));
            Assert.True(ownership.OwnsValue(typeof(OwnedProbe).GetMethod(nameof(OwnedProbe.Ping))));
            Assert.True(ownership.OwnsValue(new List<OwnedProbe>()));
            Assert.False(ownership.OwnsValue("text"));
            Assert.False(ownership.OwnsValue(typeof(string)));
            Assert.False(ownership.OwnsValue(null));
        }

        [Fact]
        public void Strip_RemovesOwnedEntriesOnly()
        {
            var ownership = Ownership();
            Action foreign = GC.Collect;
            Action staticOwned = OwnedProbe.Ping;
            Action instanceOwned = new OwnedProbe().InstancePing;
            var mixed = (Action)Delegate.Combine(staticOwned, foreign, instanceOwned);

            var stripped = ownership.Strip(mixed, out var changed);

            Assert.True(changed);
            Assert.Equal(new Delegate[] { foreign }, stripped.GetInvocationList());
        }

        [Fact]
        public void Strip_AllOwned_ReturnsNull_NothingOwned_ReturnsSameDelegate()
        {
            var ownership = Ownership();
            Action owned = OwnedProbe.Ping;
            Action foreign = GC.Collect;

            Assert.Null(ownership.Strip(owned, out var ownedChanged));
            Assert.True(ownedChanged);
            Assert.Same(foreign, ownership.Strip(foreign, out var foreignChanged));
            Assert.False(foreignChanged);
            Assert.Null(ownership.Strip(null, out var nullChanged));
            Assert.False(nullChanged);
        }

        [Fact]
        public void EmptyOwnership_OwnsNothing()
        {
            var ownership = new AssemblyOwnership(null);

            Assert.True(ownership.IsEmpty);
            Assert.False(ownership.Owns(typeof(OwnedProbe)));
            Assert.False(ownership.OwnsValue(new OwnedProbe()));
        }
    }
}
