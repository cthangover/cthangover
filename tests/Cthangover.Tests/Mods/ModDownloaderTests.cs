using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
	public class ModDownloaderTests
	{
		[Fact]
		public async void InstallAsync_ReturnsFalse_WhenModIdEmpty()
		{
			var result = await ModDownloader.InstallAsync("");
			Assert.False(result);
		}

		[Fact]
		public async void InstallAsync_ReturnsFalse_WhenModIdNull()
		{
			var result = await ModDownloader.InstallAsync(null);
			Assert.False(result);
		}

		[Fact]
		public async void InstallAsync_ReturnsFalse_WhenCatalogNotLoaded()
		{
			var result = await ModDownloader.InstallAsync("nonexistent_mod");
			Assert.False(result);
		}

		// InstallProgress is a plain DTO with auto-properties and no logic:
		// assigning fields and reading them back verifies only the test's own
		// arrange block, so no test pins it here. Its real usage is covered by
		// the downloader install flows.
	}
}
