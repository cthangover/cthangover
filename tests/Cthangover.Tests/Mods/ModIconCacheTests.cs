using Cthangover.Core.Mods.Caches;

namespace Engine.Tests.Mods
{
	public class ModIconCacheTests : IDisposable
	{
		private readonly string _testDir;

		public ModIconCacheTests()
		{
			_testDir = Path.Combine(Path.GetTempPath(), $"modiconcache_test_{System.Guid.NewGuid():N}");
			Directory.CreateDirectory(_testDir);
		}

		public void Dispose()
		{
			if (Directory.Exists(_testDir))
				Directory.Delete(_testDir, recursive: true);
		}

		[Fact]
		public async void GetOrDownloadAsync_ReturnsNull_WhenModIdEmpty()
		{
			var result = await ModIconCache.GetOrDownloadAsync("", "https://example.com/icon.png");
			Assert.Null(result);
		}

		[Fact]
		public async void GetOrDownloadAsync_ReturnsNull_WhenIconUrlEmpty()
		{
			var result = await ModIconCache.GetOrDownloadAsync("test_mod", "");
			Assert.Null(result);
		}

		[Fact]
		public async void GetOrDownloadAsync_ReturnsNull_WhenIconUrlNull()
		{
			var result = await ModIconCache.GetOrDownloadAsync("test_mod", null);
			Assert.Null(result);
		}

		[Fact]
		public void GetIconsDir_ReturnsPathUnderCacheRoot()
		{
			var dir = ModIconCache.GetIconsDir();
			Assert.EndsWith("icons", dir);
		}

		[Fact]
		public void GetIconPath_IncludesModId()
		{
			var path = ModIconCache.GetIconPath("my_mod");
			Assert.EndsWith("my_mod.png", path);
		}
	}
}
