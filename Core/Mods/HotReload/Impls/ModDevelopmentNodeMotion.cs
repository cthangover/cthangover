namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Godot;

    /// <summary>Edits or animates declared node properties on the game thread through the trusted development host.</summary>
    public sealed class ModDevelopmentNodeMotion : IModDevelopmentCommand
    {
        private readonly Dictionary<(ulong Node, string Property), Tween> motions = new();

        /// <inheritdoc />
        public IReadOnlyCollection<string> Commands { get; } = new[] { "node.set", "node.tween", "node.sine", "node.motion.stop" };

        /// <inheritdoc />
        public string Description => "Use a node path and property. node.set takes value; node.tween takes value and duration_s; node.sine takes amplitude and period_s; node.motion.stop holds the current value.";

        /// <inheritdoc />
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            foreach (var entry in motions.Where(IsExpired).ToArray())
                motions.Remove(entry.Key);

            var node = ModDevelopmentNodeQuery.Resolve(context);
            var name = context.Require("property");
            var key = (node.GetInstanceId(), name);
            using var property = new StringName(name);
            var type = ModDevelopmentNodeValue.WritableType(node, property);
            var changed = Apply(context.Request, command, node, property, type, key);
            if (changed)
                context.MarkChanged();

            return Task.FromResult<JsonNode>(new JsonObject
            {
                ["path"] = node.GetPath().ToString(),
                ["property"] = name,
                ["value"] = ModDevelopmentNodeQuery.InspectValue(node.Get(property)),
                ["moving"] = motions.ContainsKey(key)
            });
        }

        private bool Apply(JsonObject request, string command, Node node, StringName property,
            Variant.Type type, (ulong Node, string Property) key)
        {
            switch (command)
            {
                case "node.set":
                    var value = Read(request, "value", type);
                    Stop(key);
                    node.Set(property, value);
                    return true;
                case "node.tween":
                    if (!ModDevelopmentNodeValue.CanTween(type))
                        throw new ArgumentException("This node property type cannot be tweened: " + type);
                    var target = Read(request, "value", type);
                    var duration = Seconds(request, "duration_s");
                    using (var path = new NodePath(property.ToString()))
                        Start(node, key).TweenProperty(node, path, target, duration)
                            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                    return true;
                case "node.sine":
                    var period = Seconds(request, "period_s");
                    var mid = node.Get(property);
                    var amplitude = Read(request, "amplitude", type);
                    var upper = ModDevelopmentNodeValue.Offset(mid, amplitude, 1);
                    var lower = ModDevelopmentNodeValue.Offset(mid, amplitude, -1);
                    using (var path = new NodePath(property.ToString()))
                        Sine(Start(node, key).SetLoops(), node, path, mid, upper, lower, period / 4);
                    return true;
                default:
                    return Stop(key);
            }
        }

        private static void Sine(Tween tween, Node node, NodePath path, Variant mid, Variant upper, Variant lower, double quarter)
        {
            tween.SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(node, path, upper, quarter).From(mid).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(node, path, mid, quarter).From(upper).SetEase(Tween.EaseType.In);
            tween.TweenProperty(node, path, lower, quarter).From(mid).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(node, path, mid, quarter).From(lower).SetEase(Tween.EaseType.In);
        }

        private Tween Start(Node node, (ulong Node, string Property) key)
        {
            Stop(key);
            var tween = node.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
            motions.Add(key, tween);
            return tween;
        }

        private bool Stop((ulong Node, string Property) key)
        {
            if (!motions.Remove(key, out var tween))
                return false;

            tween.Kill();
            return true;
        }

        private static bool IsExpired(KeyValuePair<(ulong Node, string Property), Tween> entry) => !entry.Value.IsValid();

        private static Variant Read(JsonObject request, string field, Variant.Type type) => ModDevelopmentNodeValue.Read(
            request[field] ?? throw new ArgumentException("Missing " + field + "."), type);

        private static double Seconds(JsonObject request, string field)
        {
            var seconds = ModDevelopmentNodeValue.Number(request[field] ?? throw new ArgumentException("Missing " + field + "."));
            return seconds is >= .05 and <= 300 ? seconds : throw new ArgumentException(field + " must be from 0.05 to 300 seconds.");
        }
    }
}
