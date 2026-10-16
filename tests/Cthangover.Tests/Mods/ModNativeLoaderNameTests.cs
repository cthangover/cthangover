using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// A native library shipped by a mod must resolve for the platform-neutral
    /// <c>[DllImport]</c> name on every OS: the Unix <c>lib</c> prefix and the
    /// extension are not part of the import name.
    /// </summary>
    public class ModNativeLoaderNameTests
    {
        [Theory]
        [InlineData("Live2DCubismCore.dll", "Live2DCubismCore")]
        [InlineData("libLive2DCubismCore.so", "Live2DCubismCore")]
        [InlineData("libLive2DCubismCore.dylib", "Live2DCubismCore")]
        [InlineData("native/linux/libLive2DCubismCore.so", "Live2DCubismCore")]
        [InlineData("Live2DCubismCore", "Live2DCubismCore")]
        [InlineData("libLive2DCubismCore", "Live2DCubismCore")]
        public void NormalizeLibraryName_MatchesDllImportName(string input, string expected)
        {
            Assert.Equal(expected, ModNativeLoader.NormalizeLibraryName(input));
        }
    }
}
