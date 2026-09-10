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

		[Fact]
		public void InstallProgress_AllFieldsSet()
		{
			var progress = new InstallProgress
			{
				Step = InstallStep.Downloading,
				Progress = 0.5f,
				Message = "Downloading... 50%",
			};

			Assert.Equal(InstallStep.Downloading, progress.Step);
			Assert.Equal(0.5f, progress.Progress);
			Assert.Equal("Downloading... 50%", progress.Message);
		}
	}
}
