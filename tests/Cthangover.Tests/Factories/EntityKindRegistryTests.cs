using Cthangover.Core.Factories;
using Cthangover.Core.Skills;

namespace Engine.Tests.Factories
{
    [EntityKind("registry_test_alpha")]
    public class RegistryTestAlpha : IIdentifiable
    {
        public string ID { get; set; }
    }

    [EntityKind("registry_test_beta")]
    public class RegistryTestBeta : IIdentifiable
    {
        public string ID { get; set; }
    }

    public class EntityKindRegistryTests
    {
        [Fact]
        public void KindOf_AnnotatedType_ReturnsNormalisedKind()
        {
            Assert.Equal("registry_test_alpha", EntityKindRegistry.KindOf<RegistryTestAlpha>());
            Assert.Equal("registry_test_beta", EntityKindRegistry.KindOf(typeof(RegistryTestBeta)));
        }

        [Fact]
        public void KindOf_UnannotatedType_ReturnsNull()
        {
            Assert.Null(EntityKindRegistry.KindOf<EntityKindRegistryTests>());
        }

        [Fact]
        public void TryResolve_AnnotatedTypeInTestAssembly_ReturnsDtoType()
        {
            Assert.True(EntityKindRegistry.TryResolve("registry_test_alpha", out var type));
            Assert.Equal(typeof(RegistryTestAlpha), type);
        }

        [Fact]
        public void TryResolve_CoreDto_ReturnsDtoType()
        {
            Assert.True(EntityKindRegistry.TryResolve("skill", out var type));
            Assert.Equal(typeof(SkillInfo), type);
        }

        [Fact]
        public void TryResolve_UnknownKind_ReturnsFalse()
        {
            Assert.False(EntityKindRegistry.TryResolve("no_such_kind_xyz", out _));
            Assert.False(EntityKindRegistry.IsKnown("no_such_kind_xyz"));
        }
    }
}
