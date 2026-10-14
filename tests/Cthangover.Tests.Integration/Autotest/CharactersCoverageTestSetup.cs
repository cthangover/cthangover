#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Factories;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Integration coverage for the characters subsystem migrated into the
    /// characters_base engine mod. The core keeps only the provider contract
    /// (<c>ICharacterTemplateProvider</c> / <c>IActionTemplateProvider</c> /
    /// <c>IAvatarProvider</c> plus the static registries); the factories and
    /// the panel live in the mod assembly, so every check here proves the
    /// core-to-mod wiring at engine runtime:
    /// <list type="bullet">
    /// <item>the mod initializer published the three providers into the core
    /// registries during <c>OnModLoaded</c>;</item>
    /// <item>templates resolve through the registry (Marao, physics/attack)
    /// and the character factory returns a copy, not the cached template;</item>
    /// <item>migrated types (ActionAssignmentReset) are loaded from a mod
    /// assembly, and the core assembly no longer defines them.</item>
    /// </list>
    /// The machine-readable line is parsed by <c>tests/Cthangover.TestRunner</c>.
    /// </summary>
    public partial class CharactersCoverageTestSetup : Node
    {
        /// <summary>Log category for all lines of this scene.</summary>
        private const string Category = "CHARACTERS_COVERAGE";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready,
            // so CharactersModInitializer.OnModLoaded has already run and published the providers.

            var kernelAssembly = typeof(GameData).Assembly;

            Check("characters mod publishes a character template provider", CharacterTemplateRegistry.Provider != null);
            Check("characters mod publishes an action template provider", ActionTemplateRegistry.Provider != null);
            Check("characters mod publishes an avatar provider", AvatarProviderRegistry.Provider != null);

            Check("template provider is registered from a mod assembly, not the core",
                CharacterTemplateRegistry.Provider != null
                && CharacterTemplateRegistry.Provider.GetType().Assembly != kernelAssembly);

            var marao = SafeGet(() => CharacterTemplateRegistry.Get("Marao"));
            Check("character template 'Marao' resolves through the registry", marao != null && marao.ID == "Marao");
            Check("character template lookup returns a copy, not the cached template",
                !ReferenceEquals(marao, SafeGet(() => CharacterTemplateRegistry.Get("Marao"))));

            var attack = SafeGet(() => ActionTemplateRegistry.Get("physics/attack"));
            Check("action template 'physics/attack' resolves through the registry", attack != null);

            // The migrated sources keep their core namespace, but the types must
            // be loaded from the mod assembly compiled at runtime, not from the core.
            var reset = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly)
                .SelectMany(a => SafeTypes(a).Where(t => t.FullName == "Cthangover.Core.Characters.ActionAssignmentReset"))
                .FirstOrDefault();
            Check("migrated type ActionAssignmentReset is loaded from a mod assembly", reset != null);
            Check("core assembly no longer defines ActionAssignmentReset",
                kernelAssembly.GetType("Cthangover.Core.Characters.ActionAssignmentReset", throwOnError: false) == null);

            // ─── Roster persistence: CharactersSaveable roundtrip (task-11) ───
            // The roster moved from the __core__ slot ("Characters"/"BattleSet"
            // blobs) to the characters_base mod slot via IModSaveable. Prove the
            // mod saveable marshals the runtime roster out and back.
            var saveableType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly)
                .SelectMany(a => SafeTypes(a).Where(t => t.FullName == "Cthangover.Mods.Characters.CharactersSaveable"))
                .FirstOrDefault();
            Check("CharactersSaveable is compiled into the characters_base mod assembly", saveableType != null);
            Check("core assembly no longer defines CharactersSaveable",
                kernelAssembly.GetType("Cthangover.Mods.Characters.CharactersSaveable", throwOnError: false) == null);

            if (saveableType != null)
            {
                var runtime = GameData.Instance.Runtime;
                runtime.CharacterData.Characters.Clear();
                runtime.CharacterData.BattleSet.Clear();
                runtime.CharacterData.AddCharacterToParty("Marao");

                var saveable = (IModSaveable)Activator.CreateInstance(saveableType);
                var slot = new ModSlot();
                saveable.OnSave(slot);
                Check("roster save writes Characters and BattleSet blobs",
                    slot.Data.ContainsKey("Characters") && slot.Data.ContainsKey("BattleSet"));
                Check("saved battle set contains the seeded party member",
                    slot.Data["BattleSet"].ReadJson<List<string>>()?.Contains("Marao") == true);

                runtime.CharacterData.Characters.Clear();
                runtime.CharacterData.BattleSet.Clear();

                var loadError = "";
                var loaded = saveable.OnLoad(slot, out loadError);
                Check("roster load succeeds without error", loaded && loadError == null);
                Check("roster load restores the party roster",
                    runtime.CharacterData.Characters.ContainsKey("Marao"));
                Check("roster load restores the battle set",
                    runtime.CharacterData.BattleSet.Contains("Marao"));
            }

            // The core save slot must no longer carry the roster (task-11):
            // BuildCoreSlot keeps Time/Lamp/ActionPool/Skills/completions only.
            var coreSlot = RuntimeSerializer.BuildCoreSlot(GameData.Instance.Runtime, "1.0.0", "test", new HashSet<string>());
            Check("core save slot no longer carries the roster blobs",
                coreSlot.Data.ContainsKey("Time")
                && !coreSlot.Data.ContainsKey("Characters")
                && !coreSlot.Data.ContainsKey("BattleSet"));

            // ─── Character panel scene contract (tscn migration) ───
            // The panel layout lives in character_panel.tscn; the behaviour
            // resolves its nodes with hard GetNode paths inside OnceConstruct.
            // Instantiate the scene the same way the game does and show it:
            // any broken scene<->code contract fails loudly here instead of
            // silently deforming the panel at runtime.
            CallDeferred(nameof(RunPanelSceneCheck));
        }

        private void RunPanelSceneCheck()
        {
            var ok = false;
            var error = "";
            try
            {
                var panel = new ModScenePrefab("scenes/ui/character_panel.tscn").Instantiate<Control>();
                AddChild(panel);
                // Widget.Show is a new-virtual method hidden behind the Control
                // reference — resolve and invoke it on the actual mod type so
                // the widget lifecycle (OnceConstruct) actually runs.
                panel.GetType().GetMethod("Show", Type.EmptyTypes)?.Invoke(panel, null);

                var hasLegend = panel.GetNodeOrNull("Panel/Margin/Root/Header/Legend") != null;
                var poolHost = panel.GetNodeOrNull<Control>("Panel/Margin/Root/Body/Right/ActionPool");
                var poolBuilt = poolHost != null && poolHost.GetChildCount() > 0;
                var hasLegendBadges = hasLegend && panel.GetNode<HFlowContainer>("Panel/Margin/Root/Header/Legend").GetChildCount() > 0;

                panel.Hide();
                panel.QueueFree();

                error = $"legend={hasLegend} poolBuilt={poolBuilt} badges={hasLegendBadges} (poolHost={(poolHost != null ? poolHost.GetChildCount().ToString() : "null")})";
                ok = hasLegend && poolBuilt && hasLegendBadges;
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            Check("character panel scene instantiates and builds" + (error != "" ? ": " + error : ""), ok);

            GameLogger.Log(Category, $"[TEST_RESULT] name=coverage_characters passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private void Check(string what, bool ok)
        {
            if (ok)
            {
                _passed++;
                GameLogger.Log(Category, $"PASS {what}");
            }
            else
            {
                _failed++;
                GameLogger.Log(Category, $"FAIL {what}", LogLevel.Error);
            }
        }

        private static T SafeGet<T>(Func<T> f) where T : class
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, $"lookup failed: {e.Message}", LogLevel.Error); return null; }
        }

        private static Type[] SafeTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }
    }
}
#endif
