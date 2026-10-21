using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Utils;
using Xunit;

namespace Engine.Tests.Mods
{
	public class CatalogExportEntryTests
	{
		[Fact]
		public void JsonRoundTrip_AllFields()
		{
			var original = new CatalogExportEntry
			{
				Id = "test_mod",
				Name = "Test Mod",
				Author = "Tester",
				Version = "2.0.0",
				Description = "A test mod with all fields",
				Category = CategoryType.Gameplay,
				Tags = new List<string> { "test", "example" },
				IconUrl = "https://example.com/icon.png",
				Depends = new List<string> { "core>=1", "ui_base>=1?" },
				ConflictsWith = new List<string> { "legacy_mod" },
				Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload
					{
						Version = "2.0.0",
						Urls = new List<string> { "https://example.com/test_mod_v2.0.0.zip", "https://mirror.example.com/test_mod_v2.0.0.zip" },
						SizeBytes = 1048576,
						ArchiveSha256 = "abc123def456",
						InstallSha256 = "7890abcdef12",
						Changelog = "Fixed bugs, added features",
					},
					new CatalogEntryDownload
					{
						Version = "1.0.0",
						Urls = new List<string> { "https://example.com/test_mod_v1.0.0.zip" },
						SizeBytes = 500000,
						ArchiveSha256 = "old_sha256",
						InstallSha256 = "old_install_sha256",
					},
				},
				Status = "stable",
				MinGameVersion = "1.0.0",
				MaxGameVersion = null,
			};

			var json = JsonSerializer.Serialize(original);
			var restored = JsonSerializer.Deserialize<CatalogExportEntry>(json);

			Assert.NotNull(restored);
			Assert.Equal(original.Id, restored.Id);
			Assert.Equal(original.Name, restored.Name);
			Assert.Equal(original.Author, restored.Author);
			Assert.Equal(original.Version, restored.Version);
			Assert.Equal(original.Description, restored.Description);
			Assert.Equal(original.Category, restored.Category);
			Assert.NotNull(restored.Tags);
			Assert.Equal(original.Tags.Count, restored.Tags.Count);
			Assert.Equal(original.Tags[0], restored.Tags[0]);
			Assert.Equal(original.IconUrl, restored.IconUrl);
			Assert.NotNull(restored.Depends);
			Assert.Equal(original.Depends.Count, restored.Depends.Count);
			Assert.Equal(original.Depends[0], restored.Depends[0]);
			Assert.Equal(original.Depends[1], restored.Depends[1]);
			Assert.NotNull(restored.ConflictsWith);
			Assert.Single(restored.ConflictsWith);
			Assert.Equal(original.ConflictsWith[0], restored.ConflictsWith[0]);
			Assert.NotNull(restored.Downloads);
			Assert.Equal(2, restored.Downloads.Count);
			Assert.Equal("2.0.0", restored.Downloads[0].Version);
			Assert.Equal(2, restored.Downloads[0].Urls.Count);
			Assert.Equal("https://example.com/test_mod_v2.0.0.zip", restored.Downloads[0].Urls[0]);
			Assert.Equal("https://mirror.example.com/test_mod_v2.0.0.zip", restored.Downloads[0].Urls[1]);
			Assert.Equal(original.Downloads[0].SizeBytes, restored.Downloads[0].SizeBytes);
			Assert.Equal(original.Downloads[0].ArchiveSha256, restored.Downloads[0].ArchiveSha256);
			Assert.Equal(original.Downloads[0].InstallSha256, restored.Downloads[0].InstallSha256);
			Assert.Equal("1.0.0", restored.Downloads[1].Version);
			Assert.Equal(original.Downloads[0].Changelog, restored.Downloads[0].Changelog);
			Assert.Equal(original.Status, restored.Status);
			Assert.Equal(original.MinGameVersion, restored.MinGameVersion);
			Assert.Null(restored.MaxGameVersion);
		}

		[Fact]
		public void JsonRoundTrip_CombinedCategory()
		{
			var original = new CatalogExportEntry
			{
				Id = "multi_cat_mod",
				Version = "1.0.0",
				Category = CategoryType.Library | CategoryType.Gameplay,
			};

			var json = JsonSerializer.Serialize(original);
			var restored = JsonSerializer.Deserialize<CatalogExportEntry>(json);

			Assert.NotNull(restored);
			Assert.Equal(CategoryType.Library | CategoryType.Gameplay, restored.Category);
		}

		[Fact]
		public void JsonRoundTrip_WithLocales()
		{
			var original = new CatalogExportEntry
			{
				Id = "localized_mod",
				Name = "Default Name",
				Description = "Default Desc",
				Version = "1.0",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "en", Name = "English Name", Description = "English Desc" },
					new CatalogLocaleEntry { Lang = "ru", Name = "Русское Имя", Description = "Русское Описание" },
				},
			};

			var json = JsonSerializer.Serialize(original);
			var restored = JsonSerializer.Deserialize<CatalogExportEntry>(json);

			Assert.NotNull(restored);
			Assert.NotNull(restored.Locale);
			Assert.Equal(2, restored.Locale.Count);
			Assert.Equal("en", restored.Locale[0].Lang);
			Assert.Equal("English Name", restored.Locale[0].Name);
			Assert.Equal("English Desc", restored.Locale[0].Description);
			Assert.Equal("ru", restored.Locale[1].Lang);
			Assert.Equal("Русское Имя", restored.Locale[1].Name);
			Assert.Equal("Русское Описание", restored.Locale[1].Description);
		}

		[Fact]
		public void JsonRoundTrip_MinimalFields()
		{
			var original = new CatalogExportEntry
			{
				Id = "minimal",
				Version = "1.0",
			};

			var json = JsonSerializer.Serialize(original);
			var restored = JsonSerializer.Deserialize<CatalogExportEntry>(json);

			Assert.NotNull(restored);
			Assert.Equal("minimal", restored.Id);
			Assert.Equal("1.0", restored.Version);
			Assert.Null(restored.Name);
			Assert.Null(restored.Locale);
			Assert.Null(restored.Depends);
			Assert.Null(restored.Downloads);
		}

		[Fact]
		public void JsonRoundTrip_SourceUrlsAndVariantConflict()
		{
			var original = new CatalogExportEntry
			{
				Id = "mod",
				Version = "1.0",
				SourceUrls = new List<string>
				{
					"https://catalog-a.example/catalog.json",
					"https://catalog-b.example/catalog.json",
				},
				VariantConflict = true,
			};

			var json = JsonSerializer.Serialize(original);
			var restored = JsonSerializer.Deserialize<CatalogExportEntry>(json);

			Assert.NotNull(restored.SourceUrls);
			Assert.Equal(2, restored.SourceUrls.Count);
			Assert.Equal("https://catalog-a.example/catalog.json", restored.SourceUrls[0]);
			Assert.Equal("https://catalog-b.example/catalog.json", restored.SourceUrls[1]);
			Assert.True(restored.VariantConflict);
		}

		[Fact]
		public void JsonRoundTrip_NormalEntry_OmitsProvenance()
		{
			var original = new CatalogExportEntry { Id = "mod", Version = "1.0" };

			var exportJson = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);

			Assert.DoesNotContain("source_urls", exportJson);
			Assert.DoesNotContain("variant_conflict", exportJson);

			var restored = JsonSerializer.Deserialize<CatalogExportEntry>(exportJson);
			Assert.Null(restored.SourceUrls);
			Assert.Null(restored.VariantConflict);
		}

		[Fact]
		public void GetLocalizedName_RequestedLanguage()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "en", Name = "English" },
					new CatalogLocaleEntry { Lang = "ru", Name = "Русский" },
				},
			};
			Assert.Equal("Русский", entry.GetLocalizedName("ru"));
		}

		[Fact]
		public void GetLocalizedName_FallbackToEnglish()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "en", Name = "English" },
					new CatalogLocaleEntry { Lang = "ru", Name = "Русский" },
				},
			};
			Assert.Equal("English", entry.GetLocalizedName("zh"));
		}

		[Fact]
		public void GetLocalizedName_FallbackToFirstAvailable()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "ru", Name = "Русский" },
					new CatalogLocaleEntry { Lang = "fr", Name = "Français" },
				},
			};
			Assert.Equal("Русский", entry.GetLocalizedName("zh"));
		}

		[Fact]
		public void GetLocalizedName_FallbackToFlatName()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Name = "Flat Name",
			};
			Assert.Equal("Flat Name", entry.GetLocalizedName("ru"));
		}

		[Fact]
		public void GetLocalizedName_FallbackToId()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod_id_only",
			};
			Assert.Equal("mod_id_only", entry.GetLocalizedName("ru"));
		}

		[Fact]
		public void GetLocalizedName_LocaleEntryWithEmptyName_FallsBack()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Name = "Flat Name",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "en", Name = "" },
				},
			};
			Assert.Equal("Flat Name", entry.GetLocalizedName("en"));
		}

		[Fact]
		public void GetLocalizedDescription_RequestedLanguage()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "en", Description = "English desc" },
					new CatalogLocaleEntry { Lang = "ru", Description = "Русское описание" },
				},
			};
			Assert.Equal("Русское описание", entry.GetLocalizedDescription("ru"));
		}

		[Fact]
		public void GetLocalizedDescription_FallbackToEnglish()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "en", Description = "English desc" },
					new CatalogLocaleEntry { Lang = "ru", Description = "Русское описание" },
				},
			};
			Assert.Equal("English desc", entry.GetLocalizedDescription("zh"));
		}

		[Fact]
		public void GetLocalizedDescription_FallbackToFirstAvailable()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "ru", Description = "Русское описание" },
				},
			};
			Assert.Equal("Русское описание", entry.GetLocalizedDescription("zh"));
		}

		[Fact]
		public void GetLocalizedDescription_FallbackToFlatDescription()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Description = "Flat desc",
			};
			Assert.Equal("Flat desc", entry.GetLocalizedDescription("ru"));
		}

		[Fact]
		public void GetLocalizedDescription_EmptyByDefault()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
			};
			Assert.Equal(string.Empty, entry.GetLocalizedDescription("ru"));
		}

		[Fact]
		public void GetLocalizedDescription_LocaleEntryWithEmptyDesc_FallsBack()
		{
			var entry = new CatalogExportEntry
			{
				Id = "mod",
				Description = "Flat desc",
				Locale = new List<CatalogLocaleEntry>
				{
					new CatalogLocaleEntry { Lang = "en", Description = "" },
				},
			};
			Assert.Equal("Flat desc", entry.GetLocalizedDescription("en"));
		}

		[Fact]
		public void CatalogRoot_RoundTrip()
		{
			var original = new CatalogRoot
			{
				Meta = new CatalogMeta { Version = 1, UpdatedAt = "2026-01-01T00:00:00Z" },
				Mods = new List<CatalogExportEntry>
				{
					new CatalogExportEntry { Id = "mod_a", Version = "1.0" },
					new CatalogExportEntry { Id = "mod_b", Version = "2.0" },
				},
			};

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
			};
			var json = JsonSerializer.Serialize(original);
			var restored = JsonSerializer.Deserialize<CatalogRoot>(json, options);

			Assert.NotNull(restored);
			Assert.NotNull(restored.Meta);
			Assert.Equal(1, restored.Meta.Version);
			Assert.NotNull(restored.Mods);
			Assert.Equal(2, restored.Mods.Count);
			Assert.Equal("mod_a", restored.Mods[0].Id);
			Assert.Equal("mod_b", restored.Mods[1].Id);
		}

		[Fact]
		public void Depends_PreservesVersionOperators()
		{
			var original = new CatalogExportEntry
			{
				Id = "test",
				Depends = new List<string> { "core>=1.0.0", "ui_base>=2", "opt_mod>=1?" },
			};

			var json = JsonSerializer.Serialize(original);
			var restored = JsonSerializer.Deserialize<CatalogExportEntry>(json);

			Assert.NotNull(restored);
			Assert.NotNull(restored.Depends);
			Assert.Equal(3, restored.Depends.Count);
			Assert.Equal("core>=1.0.0", restored.Depends[0]);
			Assert.Equal("ui_base>=2", restored.Depends[1]);
			Assert.Equal("opt_mod>=1?", restored.Depends[2]);
		}
	}

	public class ModHasherTests : IDisposable
	{
		private readonly string _testDir;

		public ModHasherTests()
		{
			_testDir = Path.Combine(Path.GetTempPath(), $"modhasher_test_{Guid.NewGuid():N}");
			Directory.CreateDirectory(_testDir);
		}

		public void Dispose()
		{
			if (Directory.Exists(_testDir))
				Directory.Delete(_testDir, recursive: true);
		}

		[Fact]
		public void ExportFromFolder_ArchiveLeavesOutHiddenEntries()
		{
			var mod = Path.Combine(_testDir, "mod");
			Directory.CreateDirectory(Path.Combine(mod, "data"));
			Directory.CreateDirectory(Path.Combine(mod, ".git", "objects"));
			File.WriteAllText(Path.Combine(mod, "manifest.json"), "{ \"id\": \"mod\" }");
			File.WriteAllText(Path.Combine(mod, "data", "a.json"), "{}");
			File.WriteAllText(Path.Combine(mod, "data", ".cache"), "x");
			File.WriteAllText(Path.Combine(mod, ".git", "objects", "pack"), "x");
			var info = new ModInfo { Id = "mod", FileProvider = new FolderModFileProvider(mod, "mod") };

			var json = ModCatalogExporter.ExportFromFolder(info, new CatalogExportEntry { Id = "mod", Version = "1.0.0" }, Path.Combine(_testDir, "out"));

			using var zip = ZipFile.OpenRead(Path.ChangeExtension(json, ".zip"));
			Assert.Equal(new[] { "data/a.json", "manifest.json" }, zip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal));
		}

		[Fact]
		public void ComputeInstallSha256_EmptyDir_ReturnsHash()
		{
			var hash = ModHasher.ComputeInstallSha256(_testDir);
			Assert.NotNull(hash);
			Assert.Equal(64, hash.Length);
		}

		[Fact]
		public void ComputeInstallSha256_SameContent_SameHash()
		{
			var dir1 = Path.Combine(_testDir, "a");
			var dir2 = Path.Combine(_testDir, "b");
			Directory.CreateDirectory(dir1);
			Directory.CreateDirectory(dir2);

			File.WriteAllText(Path.Combine(dir1, "file.txt"), "hello");
			File.WriteAllText(Path.Combine(dir2, "file.txt"), "hello");

			var hash1 = ModHasher.ComputeInstallSha256(dir1);
			var hash2 = ModHasher.ComputeInstallSha256(dir2);

			Assert.Equal(hash1, hash2);
		}

		[Fact]
		public void ComputeInstallSha256_DifferentContent_DifferentHash()
		{
			var dir1 = Path.Combine(_testDir, "a");
			var dir2 = Path.Combine(_testDir, "b");
			Directory.CreateDirectory(dir1);
			Directory.CreateDirectory(dir2);

			File.WriteAllText(Path.Combine(dir1, "file.txt"), "hello");
			File.WriteAllText(Path.Combine(dir2, "file.txt"), "world");

			var hash1 = ModHasher.ComputeInstallSha256(dir1);
			var hash2 = ModHasher.ComputeInstallSha256(dir2);

			Assert.NotEqual(hash1, hash2);
		}

		[Fact]
		public void ComputeInstallSha256_IgnoresDotFiles()
		{
			var dir = Path.Combine(_testDir, "mod");
			var subDir = Path.Combine(dir, ".godot");
			Directory.CreateDirectory(subDir);

			File.WriteAllText(Path.Combine(dir, "manifest.json"), "{}");
			File.WriteAllText(Path.Combine(subDir, "cache.txt"), "cached");

			var hash = ModHasher.ComputeInstallSha256(dir);

			Assert.NotNull(hash);
			Assert.Equal(64, hash.Length);
		}

		[Fact]
		public void ComputeInstallSha256_FileAdded_ChangesHash()
		{
			var dir = Path.Combine(_testDir, "mod");
			Directory.CreateDirectory(dir);

			File.WriteAllText(Path.Combine(dir, "a.txt"), "content");

			var hash1 = ModHasher.ComputeInstallSha256(dir);

			File.WriteAllText(Path.Combine(dir, "b.txt"), "more");

			var hash2 = ModHasher.ComputeInstallSha256(dir);

			Assert.NotEqual(hash1, hash2);
		}

		[Fact]
		public void ComputeInstallSha256_FolderAndZip_SameContent_SameHash()
		{
			var dir = Path.Combine(_testDir, "mod");
			Directory.CreateDirectory(Path.Combine(dir, "dir"));
			File.WriteAllText(Path.Combine(dir, "dir", "main.txt"), "hello");
			File.WriteAllText(Path.Combine(dir, "manifest.json"), "{}");

			var zipPath = Path.Combine(_testDir, "mod.zip");
			using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
			{
				WriteZipEntry(zip, "dir/main.txt", "hello");
				WriteZipEntry(zip, "manifest.json", "{}");
			}

			using var folderProvider = new FolderModFileProvider(dir, "mod");
			using var zipProvider = new ZipModFileProvider(zipPath, "mod");

			Assert.Equal(
				ModHasher.ComputeInstallSha256(folderProvider),
				ModHasher.ComputeInstallSha256(zipProvider));
		}

		[Fact]
		public void ComputeArchiveSha256_KnownContent()
		{
			var filePath = Path.Combine(_testDir, "test.bin");
			File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes("known content"));

			var hash = ModHasher.ComputeArchiveSha256(filePath);

			Assert.NotNull(hash);
			Assert.Equal(64, hash.Length);
			Assert.Matches("^[0-9a-f]+$", hash);
		}

		private static void WriteZipEntry(ZipArchive archive, string path, string content)
		{
			var entry = archive.CreateEntry(path);
			using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
			writer.Write(content);
		}
	}

	public class CatalogServiceTests
	{
		[Fact]
		public void GetRemoteModStatus_NotInstalled_WhenNullLocalVersion()
		{
			var remote = new CatalogExportEntry { Id = "mod_a", Version = "1.0" };
			var status = CatalogService.GetRemoteModStatus(null, null, remote);
			Assert.Equal(RemoteModStatus.NotInstalled, status);
		}

		[Fact]
		public void GetRemoteModStatus_Installed_WhenSameVersionSameHash()
		{
			var remote = new CatalogExportEntry
			{
				Id = "mod_a",
				Version = "1.0.0",
				Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload { Version = "1.0.0", InstallSha256 = "abc123" },
				},
			};
			var status = CatalogService.GetRemoteModStatus("1.0.0", "abc123", remote);
			Assert.Equal(RemoteModStatus.Installed, status);
		}

		[Fact]
		public void GetRemoteModStatus_UpdateAvailable_WhenNewerRemote()
		{
			var remote = new CatalogExportEntry
			{
				Id = "mod_a",
				Version = "2.0.0",
				Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload { Version = "1.0.0", InstallSha256 = "def456" },
				},
			};
			var status = CatalogService.GetRemoteModStatus("1.0.0", "def456", remote);
			Assert.Equal(RemoteModStatus.UpdateAvailable, status);
		}

		[Fact]
		public void GetRemoteModStatus_ModifiedLocally_WhenHashDiffers()
		{
			var remote = new CatalogExportEntry
			{
				Id = "mod_a",
				Version = "1.0.0",
				Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload { Version = "1.0.0", InstallSha256 = "remote_hash" },
				},
			};
			var status = CatalogService.GetRemoteModStatus("1.0.0", "local_hash", remote);
			Assert.Equal(RemoteModStatus.ModifiedLocally, status);
		}

		[Fact]
		public void GetRemoteModStatus_Installed_WhenHashEmpty()
		{
			var remote = new CatalogExportEntry
			{
				Id = "mod_a",
				Version = "1.0.0",
				Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload { Version = "1.0.0", InstallSha256 = null },
				},
			};
			var status = CatalogService.GetRemoteModStatus("1.0.0", "local_hash", remote);
			Assert.Equal(RemoteModStatus.Installed, status);
		}

		[Fact]
		public void GetRemoteModStatus_NotModified_WhenNoLocalHash()
		{
			var remote = new CatalogExportEntry
			{
				Id = "mod_a",
				Version = "1.0.0",
				Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload { Version = "1.0.0", InstallSha256 = "remote_hash" },
				},
			};
			var status = CatalogService.GetRemoteModStatus("1.0.0", null, remote);
			Assert.Equal(RemoteModStatus.Installed, status);
		}

		[Fact]
		public void GetRemoteModStatus_Installed_WhenRemoteVersionNull()
		{
			var remote = new CatalogExportEntry
			{
				Id = "mod_a",
				Version = null,
				Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload { Version = "1.0.0", InstallSha256 = "abc" },
				},
			};
			var status = CatalogService.GetRemoteModStatus("1.0.0", "abc", remote);
			Assert.Equal(RemoteModStatus.Installed, status);
		}

		[Fact]
		public async Task RefreshAsync_PreCancelledToken_ThrowsAndFiresNoEvents()
		{
			var refreshed = false;
			var failed = false;
			Action onRefreshed = () => refreshed = true;
			Action<string> onFailed = _ => failed = true;
			CatalogService.OnRefreshed += onRefreshed;
			CatalogService.OnRefreshFailed += onFailed;

			try
			{
				using var cts = new CancellationTokenSource();
				cts.Cancel();

				await Assert.ThrowsAnyAsync<OperationCanceledException>(
					() => CatalogService.RefreshAsync(forceRefresh: true, cts.Token));

				Assert.False(refreshed);
				Assert.False(failed);
				Assert.False(CatalogService.IsFetching);
			}
			finally
			{
				CatalogService.OnRefreshed -= onRefreshed;
				CatalogService.OnRefreshFailed -= onFailed;
			}
		}
	}
}
