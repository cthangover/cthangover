using Cthangover.Core.Mods.Caches;

namespace Engine.Tests.Mods
{
	public class ModIconCacheTests : IDisposable
	{
		private readonly string _testDir;
		private int _callbacks;
		private Godot.Texture2D _received;

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

		[Theory]
		[InlineData("../../evil")]
		[InlineData("..\\..\\evil")]
		[InlineData("C:\\Users\\x\\evil")]
		[InlineData("/tmp/evil")]
		public void GetIconPath_Throws_WhenModIdLeavesIconsDir(string modId)
		{
			Assert.Throws<ArgumentException>(() => ModIconCache.GetIconPath(modId));
		}

		[Theory]
		[InlineData("../../evil", "https://example.com/icon.png")]
		[InlineData("C:\\Users\\x\\evil", "https://example.com/icon.png")]
		[InlineData("test_mod", "http://example.com/icon.png")]
		[InlineData("test_mod", "file:///etc/passwd")]
		public async Task GetOrDownloadAsync_ReturnsNull_WhenIdOrUrlRefused(string modId, string iconUrl)
		{
			Assert.Null(await ModIconCache.GetOrDownloadAsync(modId, iconUrl));
		}

		[Fact]
		public async Task GetOrDownloadAsync_DoesNotContactLocalAddresses()
		{
			using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
			listener.Start();
			var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

			var result = await ModIconCache.GetOrDownloadAsync($"local_probe_{System.Guid.NewGuid():N}", $"https://127.0.0.1:{port}/icon.png");

			Assert.Null(result);
			Assert.False(listener.Pending());
		}

		[Fact]
		public void GetOrDownload_CallsBackWithNull_WhenModIdRefused()
		{
			ModIconCache.GetOrDownload("../evil", "https://example.com/icon.png", OnIconLoaded);
			Assert.Equal(1, _callbacks);
			Assert.Null(_received);
		}

		private void OnIconLoaded(Godot.Texture2D texture)
		{
			_callbacks++;
			_received = texture;
		}
	}
}
