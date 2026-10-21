#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System.Collections.Generic;
    using Cthangover.Core.Events;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Runtime checks of the <see cref="ModEventBus"/> GDScript bridge, which
    /// needs the autoload and Godot variants and so cannot run under xUnit:
    /// a string-layer subscriber (what a GDScript mod registers) must receive
    /// typed events whatever their payload, and a payload the bridge cannot
    /// convert must never throw into the code that published the event.
    /// </summary>
    public partial class EventBusTestSetup : Node
    {
        private const string Category = "EVENT_BUS_TEST";

        private readonly List<Godot.Collections.Dictionary> received = new();
        private int failed;

        public override void _Ready()
        {
            var bus = ModEventBus.Instance;
            var handler = new Callable(this, MethodName.OnBridged);
            bus.Subscribe("scene_composed", handler);
            bus.Subscribe("characters_defeated", handler);

            CheckPublish("scene_composed", new SceneComposedEvent
            {
                SceneName = "bus_scene",
                Candidates = new List<SceneCandidateReport> { new() { Id = "evt", Outcome = SceneCandidateOutcome.Queued } },
            }, "scene_name", "bus_scene");
            CheckNested("candidates", 0, "outcome", "Queued");
            CheckPublish("characters_defeated", new CharactersDefeatedEvent
            {
                Source = "bus_test",
                CharacterIds = new[] { "wolf_1", "wolf_2" },
            }, "source", "bus_test");
            CheckNested("character_ids", 1, null, "wolf_2");

            bus.Unsubscribe("scene_composed", handler);
            bus.Unsubscribe("characters_defeated", handler);

            GameLogger.Log(Category, $"[TEST_RESULT] name=event_bus passed={(failed == 0 ? 1 : 0)} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }

        private void OnBridged(Godot.Collections.Dictionary data) => received.Add(data);

        private void CheckPublish<T>(string key, T evt, string field, string expected) where T : ModEvent
        {
            received.Clear();
            try
            {
                ModEventBus.Publish(evt);
            }
            catch (System.Exception ex)
            {
                Fail($"{key}: Publish threw into the publisher: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            if (received.Count != 1)
            {
                Fail($"{key}: string subscriber got {received.Count} call(s), expected 1");
                return;
            }

            var actual = received[0].ContainsKey(field) ? received[0][field].AsString() : null;
            if (actual != expected)
                Fail($"{key}: '{field}' = '{actual}', expected '{expected}'");
            else
                GameLogger.Log(Category, $"PASS {key}: delivered with {received[0].Count} field(s)");
        }

        /// <summary>Checks one element of an array field of the last delivery (a nested object's field when <paramref name="field"/> is set).</summary>
        private void CheckNested(string arrayKey, int index, string field, string expected)
        {
            if (received.Count != 1 || !received[0].ContainsKey(arrayKey) || received[0][arrayKey].VariantType != Variant.Type.Array)
            {
                Fail($"{arrayKey}: not delivered as an array");
                return;
            }

            var items = received[0][arrayKey].AsGodotArray();
            var item = index < items.Count ? items[index] : default;
            var actual = field == null ? item.AsString() : item.AsGodotDictionary().ContainsKey(field) ? item.AsGodotDictionary()[field].AsString() : null;
            if (actual != expected)
                Fail($"{arrayKey}[{index}]{(field == null ? "" : "." + field)} = '{actual}', expected '{expected}'");
            else
                GameLogger.Log(Category, $"PASS {arrayKey}[{index}] = '{actual}'");
        }

        private void Fail(string message)
        {
            failed++;
            GameLogger.Log(Category, $"FAIL {message}", LogLevel.Error);
        }
    }
}
#endif
