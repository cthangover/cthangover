#if TOOLS
using System.IO;
using System.IO.Compression;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Menu;
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

			// Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.

			TestFolderMod();

			try
			{
				TestZipMod();
			}
			catch (System.Exception ex)
			{
				Fail($"Zip test crashed: {ex.Message}");
			}

			VerifyHotReloadRecovery();
			
			GameLogger.Log("TEST", $"ModTestSetup: {passed} passed, {failed} failed");

			if (failed == 0)
			{
				GameLogger.Log("TEST", "All mod tests PASSED");
			}

			GameLogger.Log("TEST", $"[TEST_RESULT] name=mod passed={passed} failed={failed}");
			QuitAfterFrames(failed == 0 ? 0 : 1);
		}

		/// <summary>
		/// TEMPORARY (2026-10-24, checking the native exit crash 0xC0000005/0xC0000374): quits a few
		/// frames after the last <c>ModManager.Reload</c> instead of in the same frame, so the
		/// unloaded mod contexts and freed nodes settle first. Remove once the crash is understood.
		/// </summary>
		private async void QuitAfterFrames(int exitCode)
		{
			for (var frame = 0; frame < 3; frame++)
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			GetTree().Quit(exitCode);
		}

		/// <summary>
		/// Phase 2.5 hot-reload recovery contract: after the final Reload the
		/// derived state must be rebuilt from the surviving mod set — scene routes
		/// re-registered by mod initializers, manifest-derived UI/main-menu
		/// entries re-collected, and the logical scene table re-scanned.
		/// </summary>
		private void VerifyHotReloadRecovery()
		{
			if (SceneRouteRegistry.TryGet("Battle", out var route) && route.ScenePath == "scenes/battle/battle.tscn")
				Pass("hot-reload: scene route 'Battle' re-registered after Reload");
			else
				Fail("hot-reload: scene route 'Battle' missing after Reload");

			if (MenuRegistry.AllButtons.Count > 0)
				Pass("hot-reload: main-menu entries re-collected after Reload (" + MenuRegistry.AllButtons.Count + ")");
			else
				Fail("hot-reload: main-menu entries are empty after Reload");

			if (UiRegistry.Entries.Count > 0)
				Pass("hot-reload: ui entries re-collected after Reload (" + UiRegistry.Entries.Count + ")");
			else
				Fail("hot-reload: ui entries are empty after Reload");

			var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
			if (sceneManager != null && sceneManager.HasScene("home_kitchen"))
				Pass("hot-reload: logical scene table re-scanned after Reload");
			else
				Fail("hot-reload: logical scene table is missing 'home_kitchen' after Reload");
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

		TestScenarioResolution("test_mod");

		TestBinaryAndFsPath("test_mod", ModManager.Instance.GetMod("test_mod"));
		}

		private void TestZipMod()
		{
			var modsRoot = ModRegistry.ModsPath;
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

				var scenarioEntry = zip.CreateEntry("scenarios/test_scenario.scenario");
				using (var sw = new StreamWriter(scenarioEntry.Open()))
				{
					sw.Write("scene: test_zip_scene\n---\ntext \"Hello from scenario\" key=test/greet\nend");
				}

				var scenarioWithInclude = zip.CreateEntry("scenarios/with_include.scenario");
				using (var sw = new StreamWriter(scenarioWithInclude.Open()))
				{
					sw.Write("scene: test_zip_scene\n---\ntext ${../include/stats.json}");
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

			TestScenarioResolution("test_zip_mod");
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

		private void TestScenarioResolution(string modId)
		{
			var resolved = ScenarioDefs.Resolve("test_scenario");
			if (resolved == null)
			{
				Fail($"[{modId}] ResolveScenario returned null");
				return;
			}

			Pass($"[{modId}] ResolveScenario returns content");
			if (resolved.Contains("Hello from scenario"))
				Pass($"[{modId}] ResolveScenario content matches");
			else
				Fail($"[{modId}] ResolveScenario content mismatch");

			var withInclude = ScenarioDefs.Resolve("with_include");
			if (withInclude == null)
			{
				Fail($"[{modId}] ResolveScenario(with_include) returned null");
				return;
			}

			Pass($"[{modId}] ResolveScenario(with_include) resolved");
			if (withInclude.Contains("100"))
				Pass($"[{modId}] ResolveScenario(with_include) includes resolved");
			else
				Fail($"[{modId}] ResolveScenario(with_include) includes not resolved");

			var notFound = ScenarioDefs.Resolve("nonexistent_scenario");
			if (notFound == null)
				Pass($"[{modId}] ResolveScenario nonexistent returns null (expected)");
			else
				Fail($"[{modId}] ResolveScenario nonexistent should return null");
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
