using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the pure half of the collection text convention plus the graceful
    /// out-of-runtime path of <see cref="CollectionText.Resolve"/>. The
    /// Godot-backed translation itself is exercised only through translator
    /// test doubles at the resolver level — no Godot runtime is involved here.
    /// </summary>
    public class CollectionTextTests
    {
        [Fact]
        public void IsKey_AtPrefixedValue_True()
        {
            Assert.True(CollectionText.IsKey("@collections/card_novel/name"));
            Assert.True(CollectionText.IsKey("@"));
        }

        [Fact]
        public void IsKey_PlainOrNullValues_False()
        {
            Assert.False(CollectionText.IsKey("Card Novel"));
            Assert.False(CollectionText.IsKey(string.Empty));
            Assert.False(CollectionText.IsKey(null));
        }

        [Fact]
        public void StripKey_RemovesPrefixAndKeepsRest()
        {
            Assert.Equal("collections/card_novel/name", CollectionText.StripKey("@collections/card_novel/name"));
            Assert.Equal(string.Empty, CollectionText.StripKey("@"));
        }

        [Fact]
        public void StripKey_NotAKey_ReturnsNull()
        {
            Assert.Null(CollectionText.StripKey("Card Novel"));
            Assert.Null(CollectionText.StripKey(string.Empty));
            Assert.Null(CollectionText.StripKey(null));
        }

        [Fact]
        public void Resolve_OutsideRuntime_KeyDegradesToStrippedText()
        {
            Assert.False(GameEnvironment.IsRuntime);

            Assert.Equal("collections/card_novel/name", CollectionText.Resolve("@collections/card_novel/name"));
        }

        [Fact]
        public void Resolve_OutsideRuntime_PlainTextUnchanged()
        {
            Assert.Equal("Card Novel", CollectionText.Resolve("Card Novel"));
            Assert.Null(CollectionText.Resolve(null));
            Assert.Equal(string.Empty, CollectionText.Resolve(string.Empty));
        }
    }
}
