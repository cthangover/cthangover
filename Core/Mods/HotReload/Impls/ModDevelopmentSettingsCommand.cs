namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Applies small, atomic live display patches. A restore receipt belongs to this game process and
    /// can restore only the fields the corresponding patch owned.
    /// </summary>
    public sealed class ModDevelopmentSettingsCommand : IModDevelopmentCommand
    {
        private const int MaxRestoreReceipts = 32;

        private readonly List<(Guid Id, DevelopmentDisplayValues Mask, DevelopmentDisplayValues Previous,
            DevelopmentDisplayValues Effective, DevelopmentDisplayValues PreviousProfile,
            DevelopmentDisplayValues AppliedProfile, bool Persisted)> receipts = new();

        /// <summary>Exposes current values, patch application, and restoration by receipt.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "settings.read", "settings.apply", "settings.restore" };

        /// <summary>Shows the patch fields, optional persistent write, and the meaning of the restore token.</summary>
        public string Description => "settings.read: live and profile display values. settings.apply: preset=low_power or values={max_fps:0..1000,vsync_enabled:bool,render_scale_3d:0.25..1}; persist defaults to false. settings.restore: pass restore_id from settings.apply.";

        /// <summary>Runs synchronously on the game thread and returns detached live state after any change.</summary>
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            if (!GameEnvironment.IsRuntime || GameData.Instance?.Settings is not SettingsData profile || context.Tree?.Root == null)
                throw new InvalidOperationException("Display settings require a running game window.");

            token.ThrowIfCancellationRequested();
            var viewport = context.Tree.Root;
            var before = DevelopmentDisplayValues.Capture(viewport);
            var beforeProfile = DevelopmentDisplayValues.ReadProfile(profile);
            return Task.FromResult<JsonNode>(command switch
            {
                "settings.read" => Read(before, beforeProfile),
                "settings.apply" => Apply(context, viewport, profile, before, beforeProfile),
                "settings.restore" => Restore(context, viewport, profile, before, beforeProfile),
                _ => throw new ArgumentException("Unknown settings command: " + command + ".")
            });
        }

        private static JsonObject Read(DevelopmentDisplayValues effective, DevelopmentDisplayValues profile) => new()
        {
            ["effective"] = effective.ToJson(),
            ["profile"] = profile.ToJson()
        };

        private JsonObject Apply(ModDevelopmentContext context, Viewport viewport, SettingsData profile,
            DevelopmentDisplayValues before, DevelopmentDisplayValues beforeProfile)
        {
            var patch = ParsePatch(context.Request, before);
            var persist = context.Request.ContainsKey("persist") && ReadBool("persist", context.Request["persist"]);
            var updateProfile = persist && patch.Select(patch) != beforeProfile.Select(patch);

            Commit(viewport, profile, patch, patch, patch, before, beforeProfile, updateProfile);
            var after = DevelopmentDisplayValues.Capture(viewport);
            var afterProfile = DevelopmentDisplayValues.ReadProfile(profile);
            if (before != after || updateProfile)
                context.MarkChanged();

            var id = Guid.NewGuid();
            if (receipts.Count == MaxRestoreReceipts)
                receipts.RemoveAt(0);
            receipts.Add((id, patch, before.Select(patch), after.Select(patch), beforeProfile.Select(patch),
                afterProfile.Select(patch), persist));
            return DescribeChange(id, before, after, afterProfile, persist);
        }

        private JsonObject Restore(ModDevelopmentContext context, Viewport viewport, SettingsData profile,
            DevelopmentDisplayValues before, DevelopmentDisplayValues beforeProfile)
        {
            var restoreId = context.Require("restore_id");
            if (!Guid.TryParseExact(restoreId, "D", out var id))
                throw new ArgumentException("restore_id must be the token returned by settings.apply.");

            for (var index = 0; index < receipts.Count; index++)
            {
                var receipt = receipts[index];
                if (receipt.Id != id)
                    continue;

                if (before.Select(receipt.Mask) != receipt.Effective
                    || (receipt.Persisted && beforeProfile.Select(receipt.Mask) != receipt.AppliedProfile))
                {
                    throw new InvalidOperationException("The selected settings have changed since this receipt. Restore newer changes first.");
                }

                var updateProfile = receipt.Persisted && receipt.PreviousProfile != receipt.AppliedProfile;
                Commit(viewport, profile, receipt.Mask, receipt.Previous, receipt.PreviousProfile, before, beforeProfile, updateProfile);
                var after = DevelopmentDisplayValues.Capture(viewport);
                receipts.RemoveAt(index);
                if (before != after || updateProfile)
                    context.MarkChanged();
                return DescribeChange(id, before, after, DevelopmentDisplayValues.ReadProfile(profile), receipt.Persisted);
            }

            throw new ArgumentException("Unknown, used, or expired restore_id.");
        }

        private static JsonObject DescribeChange(Guid id, DevelopmentDisplayValues before, DevelopmentDisplayValues after,
            DevelopmentDisplayValues profile, bool persisted)
        {
            var reply = Read(after, profile);
            reply["previous"] = before.ToJson();
            reply["restore_id"] = id.ToString("D");
            reply["persisted"] = persisted;
            return reply;
        }

        private static void Commit(Viewport viewport, SettingsData profile, DevelopmentDisplayValues mask,
            DevelopmentDisplayValues live, DevelopmentDisplayValues wantedProfile, DevelopmentDisplayValues previous,
            DevelopmentDisplayValues previousProfile, bool writeProfile)
        {
            try
            {
                live.Apply(viewport);
                if (writeProfile)
                {
                    wantedProfile.UpdateProfile(profile, mask);
                    profile.Save();
                }
            }
            catch
            {
                if (writeProfile)
                    previousProfile.UpdateProfile(profile, mask);
                previous.Select(mask).Apply(viewport);
                throw;
            }
        }

        private static DevelopmentDisplayValues ParsePatch(JsonObject request, DevelopmentDisplayValues current)
        {
            var patch = default(DevelopmentDisplayValues);
            if (request.ContainsKey("preset"))
            {
                var name = request["preset"]?.GetValue<string>();
                if (name != "low_power")
                    throw new ArgumentException("Unknown display preset. Use low_power.");

                var cap = current.MaxFps.GetValueOrDefault();
                patch = new DevelopmentDisplayValues(cap == 0 ? 30 : Math.Min(cap, 30), null,
                    Math.Min(current.RenderScale3D.GetValueOrDefault(1), 0.75f));
            }

            if (request.ContainsKey("values"))
            {
                if (request["values"] is not JsonObject values)
                    throw new ArgumentException("values must be an object of display setting names and values.");
                foreach (var pair in values)
                {
                    patch = pair.Key switch
                    {
                        "max_fps" => patch with { MaxFps = ReadFps(pair.Value) },
                        "vsync_enabled" => patch with
                        {
                            VsyncMode = ReadBool(pair.Key, pair.Value) ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled
                        },
                        "render_scale_3d" => patch with { RenderScale3D = ReadScale(pair.Value) },
                        _ => throw new ArgumentException("Unsupported display setting: " + pair.Key + ".")
                    };
                }
            }

            if (patch == default)
                throw new ArgumentException("Provide a display preset or at least one supported value.");
            return patch;
        }

        private static bool ReadBool(string name, JsonNode node)
        {
            if (node is not JsonValue value || !value.TryGetValue<bool>(out var result))
                throw new ArgumentException(name + " must be true or false.");
            return result;
        }

        private static int ReadFps(JsonNode node)
        {
            if (!int.TryParse(NumberText(node), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
                || result is < 0 or > 1000)
                throw new ArgumentException("max_fps must be a whole number from 0 to 1000.");
            return result;
        }

        private static float ReadScale(JsonNode node)
        {
            if (!float.TryParse(NumberText(node), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                || !float.IsFinite(result) || result is < 0.25f or > 1f)
                throw new ArgumentException("render_scale_3d must be a number from 0.25 to 1.");
            return result;
        }

        private static string NumberText(JsonNode node)
        {
            // The in-process API may supply a typed JsonValue (e.g. double 0.75),
            // whereas MCP parses the same number as a JsonElement-backed value.
            return node is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.ToJsonString() : null;
        }
    }
}
