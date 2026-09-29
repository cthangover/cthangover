using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class EntityKindPeekTests
    {
        [Fact]
        public void TryRead_KindFirst_ReturnsKind()
        {
            var json = "{ \"Kind\": \"skill\", \"Items\": [ { \"Id\": \"x\" } ] }";

            var found = EntityKindPeek.TryRead(json, out var kind);

            Assert.True(found);
            Assert.Equal("skill", kind);
        }

        [Fact]
        public void TryRead_KindAfterNestedContainers_ReturnsKind()
        {
            var json = "{ \"Meta\": { \"A\": [1, 2, { \"B\": 3 }] }, \"Kind\": \"Character\", \"Items\": [] }";

            var found = EntityKindPeek.TryRead(json, out var kind);

            Assert.True(found);
            Assert.Equal("character", kind);
        }

        [Fact]
        public void TryRead_KindWithWhitespaceAndUppercase_IsNormalised()
        {
            var found = EntityKindPeek.TryRead("{ \"kind\": \"  Status_Effect  \" }", out var kind);

            Assert.True(found);
            Assert.Equal("status_effect", kind);
        }

        [Fact]
        public void TryRead_NoKind_ReturnsFalse()
        {
            Assert.False(EntityKindPeek.TryRead("{ \"Items\": [ { \"Id\": \"x\" } ] }", out _));
        }

        [Fact]
        public void TryRead_NonStringKind_ReturnsFalse()
        {
            Assert.False(EntityKindPeek.TryRead("{ \"Kind\": 5, \"Items\": [] }", out _));
        }

        [Fact]
        public void TryRead_NonObjectRoot_ReturnsFalse()
        {
            Assert.False(EntityKindPeek.TryRead("[ { \"Kind\": \"skill\" } ]", out _));
        }

        [Fact]
        public void TryRead_InvalidJson_ReturnsFalse()
        {
            Assert.False(EntityKindPeek.TryRead("{ \"Kind\": ", out _));
        }

        [Fact]
        public void TryRead_EmptyText_ReturnsFalse()
        {
            Assert.False(EntityKindPeek.TryRead(null, out _));
            Assert.False(EntityKindPeek.TryRead("   ", out _));
        }

        [Fact]
        public void TryRead_Comments_AreSkipped()
        {
            var json = "{ /* discriminator */ \"Kind\": \"item\" }";

            var found = EntityKindPeek.TryRead(json, out var kind);

            Assert.True(found);
            Assert.Equal("item", kind);
        }
    }
}
