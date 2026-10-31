namespace Engine.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Patches;
    using Engine.Tests.Arch;
    using Engine.Tests.Utils.PatchSamples;

    public class JsonPatchDiffTests
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public void EqualObjects_GiveAnEmptyPatch()
        {
            Assert.Empty(JsonPatchDiff.Diff(Obj("""{"A":1,"B":{"C":[1,2]},"D":null}"""), Obj("""{"a":1.0,"B":{"C":[1,2]}}"""), null));
        }

        [Fact]
        public void ChangedNestedScalar_GivesAPartialObject_RemovedFieldGivesNull()
        {
            var patch = JsonPatchDiff.Diff(Obj("""{"Spawn":{"Map":"v","X":1,"Y":4},"Name":"n"}"""), Obj("""{"Spawn":{"Map":"v","X":16,"Y":4}}"""), null);

            Assert.True(JsonSemantic.Equal(Obj("""{"Spawn":{"X":16}}"""), patch));
            Assert.True(patch.ContainsKey("Name") && patch["Name"] == null);
        }

        [Fact]
        public void KeyedArray_GivesRemoveByKeyAndAddOfChangedFieldsOnly()
        {
            var before = Typed(new PatchObjectSample
            {
                Id = "house",
                Parts = new List<PatchPartSample> { new() { Id = "roof", Image = "r", X = 1 }, new() { Id = "walls", Image = "w", X = 2 } },
            });
            var after = Typed(new PatchObjectSample
            {
                Id = "house",
                Parts = new List<PatchPartSample> { new() { Id = "walls", Image = "w2", X = 2 }, new() { Id = "door", Image = "d" } },
            });

            var patch = JsonPatchDiff.Diff(before, after, typeof(PatchObjectSample));

            Assert.True(JsonSemantic.Equal(Obj("""{"Parts$remove":["roof"],"Parts$add":[{"Id":"walls","Image":"w2"},{"Id":"door","Image":"d","X":0}]}"""), patch));
            AssertRoundTrip(before, after, typeof(PatchObjectSample));
        }

        [Fact]
        public void UnkeyedArray_GrownAtTheEnd_GivesAdd_ShrunkGivesRemove_ReorderedIsReplaced()
        {
            Assert.True(JsonSemantic.Equal(Obj("""{"C$add":["c"]}"""), JsonPatchDiff.Diff(Obj("""{"C":["a","b"]}"""), Obj("""{"C":["a","b","c"]}"""), null)));
            Assert.True(JsonSemantic.Equal(Obj("""{"C$remove":["a"]}"""), JsonPatchDiff.Diff(Obj("""{"C":["a","b","a"]}"""), Obj("""{"C":["b"]}"""), null)));
            Assert.True(JsonSemantic.Equal(Obj("""{"C":["b","a"]}"""), JsonPatchDiff.Diff(Obj("""{"C":["a","b"]}"""), Obj("""{"C":["b","a"]}"""), null)));
        }

        [Fact]
        public void KeyedArray_Reordered_FallsBackToReplacement()
        {
            var before = Obj("""{"Parts":[{"Id":"a"},{"Id":"b"}]}""");
            var after = Obj("""{"Parts":[{"Id":"b"},{"Id":"a"}]}""");

            Assert.True(JsonSemantic.Equal(after, JsonPatchDiff.Diff(before, after, typeof(PatchObjectSample))));
        }

        [Fact]
        public void CompositeKey_RemoveUsesKeyObjects()
        {
            var before = Obj("""{"Overrides":[{"Layer":"Main","X":1,"Y":0,"Order":"A"},{"Layer":"Main","X":2,"Y":0,"Order":"B"}]}""");
            var after = Obj("""{"Overrides":[{"Layer":"Main","X":2,"Y":0,"Order":"B"}]}""");

            var patch = JsonPatchDiff.Diff(before, after, typeof(PatchObjectSample));

            Assert.True(JsonSemantic.Equal(Obj("""{"Overrides$remove":[{"Layer":"Main","X":1,"Y":0}]}"""), patch));
        }

        [Fact]
        public void TypeChanges_AreReplacements()
        {
            AssertRoundTrip(Obj("""{"A":{"B":1},"C":[1],"D":"x"}"""), Obj("""{"A":[1],"C":{"B":1},"D":5}"""), null);
        }

        [Fact]
        public void NumbersRoundTrip_UnderACommaCulture()
        {
            var saved = CultureInfo.CurrentCulture;
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            try
            {
                CultureInfo.CurrentCulture = comma;
                var before = Obj("""{"Light":{"Radius":2.5,"Color":"#fff"},"Steps":[0.5,1.25]}""");
                var after = Obj("""{"Light":{"Radius":6.75,"Color":"#fff"},"Steps":[0.5,1.25,3.5]}""");

                var patch = JsonPatchDiff.Diff(before, after, null);

                Assert.Equal("""{"Light":{"Radius":6.75},"Steps$add":[3.5]}""", patch.ToJsonString());
                AssertRoundTrip(before, after, null);
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Fact]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => JsonPatchDiff.Diff(null, new JsonObject(), null));
            Assert.Throws<ArgumentNullException>(() => JsonPatchDiff.Diff(new JsonObject(), null, null));
        }

        /// <summary>
        /// The round trip over every entity of every mod in the repository: each entity against the
        /// next one of its kind (a large edit), against itself with a field dropped, a nested value
        /// changed and an array grown and shrunk (small edits). merge(base, diff(base, edited)) must
        /// equal edited, and diff(x, x) must be empty. An entity whose own source holds operation keys
        /// (an <c>Extends</c> child or a placement written as a layer, e.g. <c>States$add</c>) is a
        /// patch, not a concrete entity, and is left out: the diff is defined between concrete ones.
        /// </summary>
        [Fact]
        public void RoundTrip_OverAllEntitiesOfAllMods()
        {
            var byKind = new Dictionary<string, List<JsonObject>>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoPaths.FindRepoRoot(), "mods"), "*.json", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
                    continue;
                JsonObject root;
                try
                {
                    root = JsonNode.Parse(File.ReadAllText(file), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
                }
                catch (JsonException)
                {
                    continue;
                }
                if (root?["Kind"] is not JsonValue kind || root["Items"] is not JsonArray items)
                    continue;
                var list = byKind.TryGetValue(kind.ToString(), out var existing) ? existing : byKind[kind.ToString()] = new List<JsonObject>();
                list.AddRange(items.OfType<JsonObject>().Where(item => !HasOperationKey(item)));
            }

            var entities = byKind.Values.Sum(l => l.Count);
            Assert.True(entities >= 100, $"expected the repository's entities, found {entities}");
            var failures = new List<string>();
            foreach (var pair in byKind)
            {
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var entity = pair.Value[i];
                    var id = entity["Id"]?.ToJsonString() ?? i.ToString(CultureInfo.InvariantCulture);
                    Check(failures, $"{pair.Key} {id} (self)", entity, entity);
                    if (i + 1 < pair.Value.Count)
                        Check(failures, $"{pair.Key} {id} → next", entity, pair.Value[i + 1]);
                    foreach (var edited in Edits(entity))
                        Check(failures, $"{pair.Key} {id} (edit)", entity, edited);
                }
            }
            Assert.True(failures.Count == 0, $"{failures.Count} of {entities} entities fail the round trip: {string.Join("; ", failures.Take(10))}");
        }

        private static bool HasOperationKey(JsonNode node) => node switch
        {
            JsonObject obj => obj.Any(pair => pair.Key.Contains('$') || HasOperationKey(pair.Value)),
            JsonArray array => array.Any(HasOperationKey),
            _ => false,
        };

        private static IEnumerable<JsonObject> Edits(JsonObject entity)
        {
            foreach (var pair in entity.ToList())
            {
                var dropped = (JsonObject)entity.DeepClone();
                dropped.Remove(pair.Key);
                yield return dropped;

                if (pair.Value is JsonObject nested && nested.Count > 0)
                {
                    var changed = (JsonObject)entity.DeepClone();
                    var first = nested.First().Key;
                    changed[pair.Key]![first] = "changed";
                    yield return changed;
                }

                if (pair.Value is JsonArray array && array.Count > 0)
                {
                    var grown = (JsonObject)entity.DeepClone();
                    grown[pair.Key]!.AsArray().Add(array[0]?.DeepClone());
                    yield return grown;
                    var shrunk = (JsonObject)entity.DeepClone();
                    shrunk[pair.Key]!.AsArray().RemoveAt(0);
                    yield return shrunk;
                }
            }
        }

        private static void Check(List<string> failures, string what, JsonObject before, JsonObject after)
        {
            var patch = JsonPatchDiff.Diff(before, after, null);
            if (ReferenceEquals(before, after) && patch.Count > 0)
            {
                failures.Add(what + ": non-empty diff of itself");
                return;
            }
            var merged = (JsonObject)before.DeepClone();
            try
            {
                JsonPatchMerger.Apply(merged, patch, null);
            }
            catch (JsonPatchException ex)
            {
                failures.Add($"{what}: {ex.Path}: {ex.Message}");
                return;
            }
            if (!JsonSemantic.Equal(merged, after))
                failures.Add(what + ": " + patch.ToJsonString());
        }

        private static void AssertRoundTrip(JsonObject before, JsonObject after, Type type)
        {
            var merged = (JsonObject)before.DeepClone();
            JsonPatchMerger.Apply(merged, JsonPatchDiff.Diff(before, after, type), type);
            Assert.True(JsonSemantic.Equal(after, merged), merged.ToJsonString());
        }

        private static JsonObject Typed<T>(T value) => JsonPatchMerger.ParseObject(JsonSerializer.Serialize(value, ReadOptions));

        private static JsonObject Obj(string json) => JsonNode.Parse(json).AsObject();
    }
}
