using System;
using Cthangover.Core.Characters;

namespace Engine.Tests.Characters
{
    /// <summary>
    /// Contract tests for the kernel bridge between the core and the
    /// characters engine mod. The two lookup flavors must stay opposite:
    /// <see cref="CharacterTemplateRegistry.Get"/> is the lenient content
    /// lookup (returns null for both "no provider" and "unknown id"), while
    /// <see cref="CharacterTemplateRegistry.GetRequired"/> is the fail-fast
    /// twin for engine-critical paths and distinguishes the two failure modes
    /// with distinct exception types.
    /// </summary>
    [Collection("CharacterTemplateRegistryStatic")]
    public class CharacterTemplateRegistryTests : IDisposable
    {
        private readonly StubCharacterTemplateProvider provider = new StubCharacterTemplateProvider();

        public CharacterTemplateRegistryTests()
        {
            CharacterTemplateRegistry.Provider = provider;
        }

        public void Dispose()
        {
            CharacterTemplateRegistry.Provider = null;
        }

        [Fact]
        public void GetRequired_KnownId_ReturnsTemplate()
        {
            var template = CharacterTemplateRegistry.GetRequired("hero");

            Assert.NotNull(template);
            Assert.Equal("hero", template.ID);
        }

        [Fact]
        public void GetRequired_UnknownId_ThrowsKeyNotFound()
        {
            provider.UnknownIds.Add("ghost");

            Assert.Throws<KeyNotFoundException>(() => CharacterTemplateRegistry.GetRequired("ghost"));
        }

        [Fact]
        public void GetRequired_MissingProvider_ThrowsInvalidOperation()
        {
            CharacterTemplateRegistry.Provider = null;

            Assert.Throws<InvalidOperationException>(() => CharacterTemplateRegistry.GetRequired("hero"));
        }

        [Fact]
        public void Get_MissingProvider_ReturnsNull()
        {
            CharacterTemplateRegistry.Provider = null;

            Assert.Null(CharacterTemplateRegistry.Get("hero"));
        }

        [Fact]
        public void Get_UnknownId_ReturnsNull()
        {
            provider.UnknownIds.Add("ghost");

            Assert.Null(CharacterTemplateRegistry.Get("ghost"));
        }
    }
}
