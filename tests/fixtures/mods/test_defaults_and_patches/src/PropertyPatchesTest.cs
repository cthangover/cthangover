using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.TestDefaultsAndPatches
{
    using Cthangover.Core.Games;

    /// <summary>
    /// Entry point of the property patches itest: active only with
    /// <c>--itest=property_patches</c>. The fixture itself is loaded only by
    /// <c>--with-mods=test_defaults_and_patches</c> (manifest <c>fixture</c>).
    /// </summary>
    public class PropertyPatchesTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !GameLaunchArgs.Current.HasIntegrationTest("property_patches"))
                return;
            queued = true;
            GameLogger.Log("TEST", "PropertyPatchesTest: activating");
            ModRoot.AddDeferred(new PropertyPatchesTestDriverNode());
        }
    }

    /// <summary>
    /// Checks the merge of <c>patches/character.json</c> and
    /// <c>patches/item.json</c> into the real entities of other mods, as the
    /// game resolves them: a targeted patch wins over the wildcard, the
    /// wildcard reaches every entity of the kind, and untouched typed fields
    /// survive; patch ids match case-insensitively, a broken patch is skipped
    /// alone, <c>$delete</c> removes an entity, <c>Extends</c> inherits from a
    /// parent resolved with its own patches and a cycle skips its members. Prints <c>[TEST_RESULT] name=property_patches</c> and quits.
    /// </summary>
    public partial class PropertyPatchesTestDriverNode : Node
    {
        private const string ResultName = "property_patches";
        private const double TimeoutSeconds = 60;

        private int passed;
        private int failed;
        private double elapsed;
        private bool done;

        public override void _Process(double delta)
        {
            if (done)
                return;
            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                Check("boot finished in time", false);
                Finish();
                return;
            }

            if (!Boot.IsReady)
                return;

            try
            {
                Run();
            }
            catch (Exception ex)
            {
                Check($"no exception ({ex.GetType().Name}: {ex.Message})", false);
            }

            Finish();
        }

        private void Run()
        {
            var characterPatches = Patches.Collect("character");
            Check("character patches collected (Marao + *)", characterPatches != null && characterPatches.ContainsKey("Marao") && characterPatches.ContainsKey("*"));
            var itemPatches = Patches.Collect("item");
            Check("item patches collected (food/ration + *)", itemPatches != null && itemPatches.ContainsKey("food/ration") && itemPatches.ContainsKey("*"));

            var characters = Entities.Collect<CharacterInfo>();
            Check("Marao resolved", characters.ContainsKey("Marao"));
            if (characters.TryGetValue("Marao", out var marao))
            {
                Expect(marao.ExtraProperties, "TestPatched", "42", "Marao: targeted value");
                Expect(marao.ExtraProperties, "TestDefault", "100", "Marao: targeted beats wildcard");
                Expect(marao.ExtraProperties, "TestGlobalPatch", "1", "Marao: wildcard-only key");
            }

            var other = characters.Values.FirstOrDefault(c => c.ID != "Marao");
            Check("another character exists", other != null);
            if (other != null)
            {
                Expect(other.ExtraProperties, "TestDefault", "-20", $"{other.ID}: wildcard value");
                Check($"{other.ID}: no targeted key", other.ExtraProperties == null || !other.ExtraProperties.ContainsKey("TestPatched"));
            }

            var items = Entities.Collect<ItemInfo>();
            if (items.TryGetValue("food/ration", out var ration))
            {
                Expect(ration.ExtraProperties, "Durability", "99", "ration: targeted beats wildcard");
                Expect(ration.ExtraProperties, "SpecialProp", "true", "ration: targeted-only key");
                Expect(ration.ExtraProperties, "GlobalItemProp", "patched", "ration: wildcard-only key");
                Check("ration: typed field survives the merge", !string.IsNullOrEmpty(ration.Name));
            }
            else
                Check("food/ration resolved", false);

            if (items.TryGetValue("food/wolf_meat", out var meat))
            {
                Expect(meat.ExtraProperties, "Durability", "50", "wolf_meat: wildcard value");
                Check($"wolf_meat: patch id is case-insensitive (FOOD/WOLF_MEAT → Cost 77, got {meat.Cost})", meat.Cost == 77);
                Check("wolf_meat: broken patches (Cost \"many\", Name$add) are skipped, the item keeps its name",
                    meat.Name == "items/food/wolf_meat/name");
            }
            else
                Check("food/wolf_meat resolved (a broken patch must not drop the kind)", false);

            Check("$delete removes test_patches/doomed", !items.ContainsKey("test_patches/doomed"));
            Check("an Extends cycle skips both entities", !items.ContainsKey("test_patches/loop_a") && !items.ContainsKey("test_patches/loop_b"));
            if (items.TryGetValue("test_patches/child", out var child))
            {
                Check("Extends: child inherits Name", child.Name == "items/food/ration/name");
                Check($"Extends: child's own Cost wins (got {child.Cost})", child.Cost == 20);
                Check($"Extends: the parent is resolved with its patches first (Sprite {child.Sprite})", child.Sprite == "food/wolf_meat");
                var shelf = child.ExtraProperties != null && child.ExtraProperties.TryGetValue("Shelf", out var value) && value is JsonElement element
                    && element.ValueKind == JsonValueKind.Object ? element : default;
                Check($"Extends: nested objects deep-merge (Shelf {(shelf.ValueKind == JsonValueKind.Object ? shelf.GetRawText() : "missing")})",
                    shelf.ValueKind == JsonValueKind.Object && shelf.TryGetProperty("Row", out var row) && row.GetInt32() == 1
                    && shelf.TryGetProperty("Col", out var col) && col.GetInt32() == 5);
                Expect(child.ExtraProperties, "Durability", "50", "child: wildcard patch applies after Extends");
            }
            else
                Check("test_patches/child resolved", false);

            ExplainChild();
        }

        /// <summary>
        /// <c>EntityResolver.Explain</c> on an <c>Extends</c> chain with patches on the parent, the
        /// child (a second file) and a wildcard: every field names its layer, entity, mod and file.
        /// </summary>
        private void ExplainChild()
        {
            const string mod = "test_defaults_and_patches";
            const string data = "items/test_patches.json";
            var resolver = Cthangover.Core.Mods.ModManager.Instance.GetResolver<EntityResolver>();
            var explanation = resolver?.Explain("item", "test_patches/child");
            Check("Explain(item, test_patches/child) returns a value", explanation?.Value != null);
            if (explanation?.Value == null)
                return;

            Check($"Explain value matches Collect (Cost {explanation.Value["Cost"]})", explanation.Value["Cost"]?.GetValue<int>() == 20);
            ExpectOrigin(explanation, "Name", EntityLayer.Definition, "test_patches/base", mod, data);
            ExpectOrigin(explanation, "Cost", EntityLayer.Definition, "test_patches/child", mod, data);
            ExpectOrigin(explanation, "Shelf.Row", EntityLayer.Definition, "test_patches/base", mod, data);
            ExpectOrigin(explanation, "Shelf.Col", EntityLayer.Definition, "test_patches/child", mod, data);
            ExpectOrigin(explanation, "Sprite", EntityLayer.Patch, "test_patches/base", mod, "patches/item.json");
            ExpectOrigin(explanation, "Description", EntityLayer.Patch, "test_patches/child", mod, "patches/item/explain.json");
            ExpectOrigin(explanation, "Durability", EntityLayer.Patch, "*", mod, "patches/item.json");
            Check("Explain of a $delete-d entity says Deleted", resolver.Explain("item", "test_patches/doomed")?.Deleted == true);
            Check("Explain of an Extends cycle member is null", resolver.Explain("item", "test_patches/loop_a") == null);
        }

        private void ExpectOrigin(EntityExplanation explanation, string path, EntityLayer layer, string entity, string mod, string file)
        {
            explanation.Fields.TryGetValue(path, out var origin);
            var actual = origin == null ? "default" : $"{origin.Layer} {origin.EntityId} {origin.ModId}/{origin.File}";
            Check($"Explain {path}: {layer} {entity} {mod}/{file} (got {actual})",
                origin != null && origin.Layer == layer && origin.EntityId == entity && origin.ModId == mod && origin.File == file);
        }

        private void Expect(Dictionary<string, object> properties, string key, string expected, string what)
        {
            var actual = properties != null && properties.TryGetValue(key, out var value) ? Text(value) : null;
            Check($"{what}: {key}={expected}" + (actual == expected ? string.Empty : $" (got {actual ?? "nothing"})"), actual == expected);
        }

        private static string Text(object value) => value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } s => s.GetString(),
            JsonElement e => e.GetRawText(),
            bool b => b ? "true" : "false",
            null => null,
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
        };

        private void Check(string what, bool ok)
        {
            if (ok)
                passed++;
            else
                failed++;
            GameLogger.Log("TEST", $"  [{(ok ? "PASS" : "FAIL")}] {what}");
        }

        private void Finish()
        {
            done = true;
            GameLogger.Log("TEST", $"[TEST_RESULT] name={ResultName} passed={passed} failed={failed}");
            Cthangover.Core.Bootstrap.ApplicationExit.Quit(GetTree(), failed == 0 ? 0 : 1);
        }
    }
}
