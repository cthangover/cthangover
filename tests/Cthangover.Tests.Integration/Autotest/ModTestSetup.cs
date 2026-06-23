#if TOOLS
using System.IO;
using System.IO.Compression;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	public partial class ModTestSetup : Node
	{
		private int passed;
		private int failed;

		public override void _Ready()
		{
			GameLogger.Log("TEST", "ModTestSetup: starting mod integration tests");

			ModManager.Instance.Initialize();

			TestFolderMod();

			try
			{
				TestZipMod();
			}
			catch (System.Exception ex)
			{
				Fail($"Zip test crashed: {ex.Message}");
			}
			
			GameLogger.Log("TEST", $"ModTestSetup: {passed} passed, {failed} failed");

			if (failed == 0)
			{
				GameLogger.Log("TEST", "All mod tests PASSED");
			}

			GetTree().Quit();
		}

		private void TestFolderMod()
		{
			var mod = ModManager.Instance.GetMod("test_mod");
			if (mod == null)
			{
				Fail("Folder mod 'test_mod' not loaded");
				return;
			}

			Pass("Folder mod 'test_mod' is loaded");
			AssertEqual("Test Mod", mod.Name, "manifest.name");
			AssertEqual("Test Author", mod.Author, "manifest.author");
			AssertEqual("A test mod for verification", mod.Description, "manifest.description");
			AssertEqual("Test Mod", mod.DisplayTitle, "DisplayTitle");

			BasicFileOps("test_mod");

			TestIncludeResolution("test_mod");

			TestBinaryAndFsPath("test_mod", ModManager.Instance.GetMod("test_mod"));
		}

		private void TestZipMod()
		{
			var modsRoot = ProjectSettings.GlobalizePath("res://mods/");
			var zipPath = Path.Combine(modsRoot, "test_zip_mod.zip").Replace('\\', '/');

			try
			{
				using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
				{
					var entry = zip.CreateEntry("manifest.json");
					using (var writer = new StreamWriter(entry.Open()))
					{
						writer.Write("{ \"name\": \"Zip Mod\", \"author\": \"Zip Author\", \"description\": \"From zip archive\" }");
					}

					var dataEntry = zip.CreateEntry("data/file.txt");
					using (var dataWriter = new StreamWriter(dataEntry.Open()))
					{
						dataWriter.Write("hello from zip");
					}

					var subEntry = zip.CreateEntry("include/characters.json");
					using (var sw = new StreamWriter(subEntry.Open()))
					{
						sw.Write("{ \"id\": \"hero\", \"desc\": \"${stats}\" }");
					}

					var statsEntry = zip.CreateEntry("include/stats.json");
					using (var sw = new StreamWriter(statsEntry.Open()))
					{
						sw.Write("{ \"hp\": 100 }");
					}
				}

				ModManager.Instance.Reload();

				var mod = ModManager.Instance.GetMod("test_zip_mod");
				if (mod == null)
				{
					Fail("Zip mod 'test_zip_mod' not loaded");
					return;
				}

				Pass("Zip mod 'test_zip_mod' is loaded");
				AssertEqual("Zip Mod", mod.Name, "zip manifest.name");
				AssertEqual("Zip Author", mod.Author, "zip manifest.author");

				BasicFileOps("test_zip_mod");

				TestIncludeResolution("test_zip_mod");
			}
			finally
			{
				var zipMod = ModManager.Instance.GetMod("test_zip_mod");
				zipMod?.FileProvider?.Dispose();
				if (File.Exists(zipPath))
					File.Delete(zipPath);
				ModManager.Instance.Reload();
			}
		}

		private void BasicFileOps(string modId)
		{
			var files = ModManager.Instance.ListFiles(modId).ToList();
			if (files.Count > 0)
				Pass($"[{modId}] ListFiles returns {files.Count} entry(ies): [{string.Join(", ", files)}]");
			else
				Fail($"[{modId}] ListFiles returned empty");

			var manifestContent = ModManager.Instance.ReadFileText(modId, "manifest.json");
			if (!string.IsNullOrEmpty(manifestContent))
				Pass($"[{modId}] ReadFileText(manifest.json) returns content");
			else
				Fail($"[{modId}] ReadFileText(manifest.json) returned null/empty");

			if (ModManager.Instance.FileExists(modId, "manifest.json"))
				Pass($"[{modId}] FileExists(manifest.json) true");
			else
				Fail($"[{modId}] FileExists(manifest.json) false");
		}

		private void TestIncludeResolution(string modId)
		{
			var resolved = ModManager.Instance.ReadResolvedText(modId, "include/characters.json");
			if (resolved == null)
			{
				Fail($"[{modId}] ReadResolvedText returned null");
				return;
			}

			Pass($"[{modId}] ReadResolvedText resolves includes");
			if (resolved.Contains("100"))
				Pass($"[{modId}] resolved content has nested include value");
			else
				Fail($"[{modId}] resolved content missing nested include value");

			if (!resolved.Contains("${"))
				Pass($"[{modId}] all includes fully expanded (no ${{}} left)");
			else
				Fail($"[{modId}] some includes not expanded");

			var json = ModManager.Instance.ReadJson<ModManifest>(modId, "manifest.json");
			if (json != null)
			{
				Pass($"[{modId}] ReadJson<ModManifest> deserializes manifest");
				if (!string.IsNullOrEmpty(json.Name))
					Pass($"[{modId}] ReadJson manifest.name = '{json.Name}'");
			}
			else
			{
				Fail($"[{modId}] ReadJson<ModManifest> returned null");
			}
		}

		private void TestBinaryAndFsPath(string modId, IModInfo mod)
		{
			var binary = ModManager.Instance.ReadFileBinary(modId, "manifest.json");
			if (binary != null && binary.Length > 0)
				Pass($"[{modId}] ReadFileBinary returns {binary.Length} bytes");
			else
				Fail($"[{modId}] ReadFileBinary returned null/empty");

			var fsPath = mod.FileProvider.GetFileSystemPath("manifest.json");
			if (mod.FileProvider is FolderModFileProvider)
			{
				if (!string.IsNullOrEmpty(fsPath) && File.Exists(fsPath))
					Pass($"[{modId}] GetFileSystemPath returns valid path: {fsPath}");
				else
					Fail($"[{modId}] GetFileSystemPath returned invalid path");
			}
			else
			{
				if (fsPath == null)
					Pass($"[{modId}] GetFileSystemPath returns null for zip (expected)");
				else
					Fail($"[{modId}] GetFileSystemPath should be null for zip");
			}
		}

		private void AssertEqual(string expected, string actual, string label)
		{
			if (expected == actual)
				Pass($"{label}: '{expected}'");
			else
				Fail($"{label}: expected '{expected}', got '{actual}'");
		}

		private void Pass(string msg)
		{
			passed++;
			GameLogger.Log("TEST", $"  [PASS] {msg}");
		}

		private void Fail(string msg)
		{
			failed++;
			GameLogger.Log("TEST", $"  [FAIL] {msg}", LogLevel.Error);
		}
	}
}
#endif
