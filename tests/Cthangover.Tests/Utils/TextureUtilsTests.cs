using Cthangover.Core.Utils;
using Xunit;

namespace Cthangover.Tests.Utils
{
    /// <summary>Plain CLR callers must never enter native image decoding or GPU upload.</summary>
    public sealed class TextureUtilsTests
    {
        /// <summary>Both owned CPU images and texture wrappers return safely when no Godot runtime is present.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("textures/absent.png")]
        public void NonRuntimeRequestsDoNotEnterGodot(string path)
        {
            Assert.False(GameEnvironment.IsRuntime);
            Assert.Null(TextureUtils.LoadImageFromModGroup("textures", path));
            Assert.Null(TextureUtils.LoadFromModGroup("textures", path));
        }
    }
}
