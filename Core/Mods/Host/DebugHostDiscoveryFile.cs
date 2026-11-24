namespace Cthangover.Core.Mods.Host
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// File-based discovery shared by every transport: the host writes one pointer file with all live
    /// endpoints, clients read it and pick a descriptor their transport understands. The location is
    /// injected from configuration (<c>development_discovery</c>) and classified through
    /// <see cref="GamePaths"/>: <c>user://</c> profile data or <c>res://</c> inside the game tree (writable
    /// in the editor, packed read-only in exports — a failed write is reported instead of silently
    /// redirecting). The newest running instance wins the file; multi-instance setups are expected to read
    /// the identity from it and prefer their own target deliberately.
    /// <para>
    /// The JSON is a wire contract, not an internal detail: <c>sdk/dev_control.py</c> parses this exact
    /// document (field names <c>protocol</c>, <c>instance</c>, <c>pid</c>, <c>updatedUtc</c>,
    /// <c>transports[].kind/address/protocol</c>) to resolve a pipe name for a pid and to verify it reached
    /// the intended instance. Renaming a field here silently breaks every external client, and the client
    /// keeps its own default location (<c>%APPDATA%</c> on Windows, the XDG data directory on Linux), so a
    /// custom <c>res://</c> location reaches clients only through <c>CTHANGOVER_DEV_DISCOVERY</c>.
    /// </para>
    /// </summary>
    public sealed class DebugHostDiscoveryFile : IDebugHostDiscovery
    {
        /// <summary>Shipped pointer location used when the host setting is absent or unusable.</summary>
        public const string DefaultPath = "res://dev_host.json";

        private readonly string path;

        /// <summary>
        /// Normalizes the host setting itself: the discovery implementation owns the default and the
        /// accepted schemes (<see cref="GamePaths.VirtualScheme.User"/>, <see cref="GamePaths.VirtualScheme.Res"/>),
        /// so the shared config stays free of discovery rules.
        /// </summary>
        public DebugHostDiscoveryFile(string discoveryPath)
        {
            var requested = (discoveryPath ?? "").Trim();
            path = GamePaths.NormalizeVirtual(requested, DefaultPath, GamePaths.VirtualScheme.User,
                GamePaths.VirtualScheme.Res);
            if (requested.Length > 0 && path == DefaultPath && requested != DefaultPath)
            {
                DebugHostInstance.Log("discovery path '" + requested + "' must be a user:// or res:// file path; using "
                    + DefaultPath, LogLevel.Warning);
            }
        }

        /// <summary>
        /// Writes the whole pointer document in one pass — protocol version, this process's
        /// <see cref="DebugHostInstance.Id"/>, pid, UTC timestamp, the globalized absolute location and every
        /// endpoint descriptor. The file is replaced, never merged: the newest running instance wins, which is
        /// why the document carries identity and timestamp, so a client can tell "overwritten by another
        /// instance" from "the host I wanted".
        /// <para>
        /// Each descriptor is written with its transport-owned <c>kind</c> string (open by design, not an
        /// enum), its address and the wire protocol version; clients filter on that string, so a transport
        /// must keep its kind stable across builds.
        /// </para>
        /// <para>
        /// Known limits, deliberately left to the client: the write is not atomic (a reader can catch a torn
        /// document — parsing then yields "no discovery" rather than a wrong answer), and a <c>res://</c>
        /// location is writable only in the editor tree; in an export the open fails, the host logs one
        /// warning and keeps serving, so discovery is the only thing missing. The host calls this once, after
        /// its transports are already accepting, so any descriptor published here is immediately connectable.
        /// </para>
        /// </summary>
        /// <param name="endpoints">Descriptors of transports that are already serving; each becomes one entry.</param>
        /// <param name="processId">PID the client uses to pick its target among instances.</param>
        public void Publish(IReadOnlyList<HostEndpointDescriptor> endpoints, int processId)
        {
            if (!GameEnvironment.IsRuntime)
            {
                throw new InvalidOperationException("Debug host discovery requires the runtime");
            }

            var transports = new System.Text.Json.Nodes.JsonArray();
            foreach (var endpoint in endpoints)
            {
                transports.Add(new System.Text.Json.Nodes.JsonObject
                {
                    ["kind"] = endpoint.Kind.ToString(),
                    ["address"] = endpoint.Address,
                    ["protocol"] = endpoint.Protocol
                });
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                DebugHostInstance.Log("discovery write failed at " + path + ": " + FileAccess.GetOpenError(),
                    LogLevel.Warning);
                return;
            }

            file.StoreString(new System.Text.Json.Nodes.JsonObject
            {
                ["protocol"] = 1,
                ["instance"] = DebugHostInstance.Id.ToString(),
                ["pid"] = processId,
                ["updatedUtc"] = DateTime.UtcNow.ToString("O"),
                ["path"] = GamePaths.Globalize(path),
                ["transports"] = transports
            }.ToJsonString());
        }

        /// <summary>
        /// Reads the pointer document on every call — no caching, because the file is tiny and a stale answer
        /// would be worse than a re-read — and returns only descriptors of the requested
        /// <paramref name="kind"/>. A missing file, an unparsable (torn) document, an unknown kind string or
        /// an entry without an address degrades to an empty list: "no discovery" is a normal state, not an
        /// error, and the caller decides whether to fall back to a known endpoint or refuse to connect.
        /// </summary>
        /// <param name="kind">Transport-owned kind string the caller can connect over (for example <c>pipe</c>).</param>
        /// <returns>Zero or more descriptors, in document order.</returns>
        public IReadOnlyList<HostEndpointDescriptor> Locate(string kind)
        {
            if (!GameEnvironment.IsRuntime)
            {
                throw new InvalidOperationException("Debug host discovery requires the runtime");
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                return Array.Empty<HostEndpointDescriptor>();
            }

            var parsed = System.Text.Json.Nodes.JsonNode.Parse(file.GetAsText()) as System.Text.Json.Nodes.JsonObject;
            if (parsed?["transports"] is not System.Text.Json.Nodes.JsonArray transports)
            {
                return Array.Empty<HostEndpointDescriptor>();
            }

            var found = new List<HostEndpointDescriptor>();
            foreach (var node in transports)
            {
                if (node is not System.Text.Json.Nodes.JsonObject entry)
                    continue;

                found.Add(new HostEndpointDescriptor(entry["kind"]?.GetValue<string>() ?? kind, entry["address"]?.GetValue<string>() ?? "",
                    entry["protocol"]?.GetValue<int>() ?? 1));
            }

            return found;
        }
    }
}
