using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// The player's authoritative source list, seeded from shipped config until
    /// first edited. Empty lists and disabled sources survive a restart. Writes
    /// are atomic; replacement keys are published before the list and obsolete
    /// owned key files are removed afterwards. External token files are borrowed.
    /// </summary>
    public sealed class CatalogServerStore
    {
        private readonly string filePath;
        private readonly string keyDirectory;

        public CatalogServerStore(string filePath)
        {
            this.filePath = Path.GetFullPath(filePath);
            keyDirectory = Path.Combine(Path.GetDirectoryName(this.filePath), "catalog_keys");
        }

        /// <summary>Resolve on the main thread. No shared mutable instance is needed.</summary>
        public static CatalogServerStore Default => new(GameEnvironment.IsRuntime
            ? GamePaths.Globalize("user://catalog_servers.json") : "catalog_servers.json");

        /// <summary>Returns a detached list, preserving shipped sources until a player first saves.</summary>
        public List<CatalogServer> Load(ModConfig config)
        {
            if (File.Exists(filePath))
            {
                var entries = JsonSerializer.Deserialize<List<CatalogServer>>(File.ReadAllText(filePath))
                    ?? throw new JsonException("Catalog server list is null");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var urls = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries)
                    if (entry == null || !Guid.TryParseExact(entry.Id, "N", out _)
                        || !ids.Add(entry.Id) || !urls.Add(NormalizeUrl(entry.Url)))
                        throw new JsonException("Invalid or duplicate catalog server");
                return entries;
            }

            var result = new List<CatalogServer>();
            foreach (var url in config.CatalogUrls ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(url) || FindUrl(result, url) != null)
                    continue;
                config.CatalogTokenFiles.TryGetValue(url, out var tokenFile);
                var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).Substring(0, 32).ToLowerInvariant();
                result.Add(new CatalogServer { Id = id, Url = url, Name = new Uri(url).Host, TokenFile = tokenFile });
            }
            return result;
        }

        /// <summary>Publishes enabled endpoints into the live config, keeping secrets out of that config file.</summary>
        public void ApplyTo(ModConfig config)
        {
            var entries = Load(config);
            var urls = new List<string>();
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (!entry.Enabled)
                    continue;
                urls.Add(entry.Url);
                if (!string.IsNullOrEmpty(entry.TokenFile))
                    tokens.Add(entry.Url, entry.TokenFile);
            }
            config.CatalogUrls = urls;
            config.CatalogTokenFiles = tokens;
        }

        /// <summary>
        /// Adds or edits a source. A blank token keeps the key only for an
        /// unchanged URL; changing the endpoint requires explicit key entry.
        /// ClearToken converts the source to public. Returns the saved entry.
        /// </summary>
        public CatalogServer Upsert(ModConfig config, CatalogServer entry, string token = null, bool clearToken = false)
        {
            if (entry == null || !Guid.TryParseExact(entry.Id, "N", out _))
                throw new ArgumentException("Invalid catalog server id");
            var entries = Load(config);
            var url = NormalizeUrl(entry.Url);
            var existing = FindId(entries, entry.Id);
            var duplicate = FindUrl(entries, url);
            if (duplicate != null && duplicate.Id != entry.Id)
                throw new InvalidOperationException("Duplicate catalog URL");
            token = token?.Trim();
            if (!string.IsNullOrEmpty(token))
            {
                try { CatalogRequest.ValidateToken(token); }
                catch (InvalidOperationException) { throw new ArgumentException("Invalid catalog key"); }
            }
            var key = clearToken || existing?.Url != url ? null : existing?.TokenFile;
            if (!clearToken && !string.IsNullOrEmpty(token))
                key = Path.Combine(keyDirectory, Guid.NewGuid().ToString("N") + ".token");
            if (key != null)
                ValidatePrivateUrl(url);
            var saved = new CatalogServer
            {
                Id = entry.Id, Url = url, Enabled = entry.Enabled,
                Name = string.IsNullOrWhiteSpace(entry.Name) ? new Uri(url).Host : entry.Name.Trim(),
                TokenFile = key,
            };
            var wroteKey = key != null && key != existing?.TokenFile;
            try
            {
                if (wroteKey)
                    WritePrivate(key, token + "\n");
                if (existing == null)
                    entries.Add(saved);
                else
                    entries[entries.IndexOf(existing)] = saved;
                WritePrivate(filePath, JsonSerializer.Serialize(entries, JsonOptionsProvider.WriteIndentedOptions));
            }
            catch
            {
                if (wroteKey)
                    File.Delete(key);
                throw;
            }
            if (existing?.TokenFile != key)
                DeleteOwnedKey(existing?.TokenFile);
            return saved;
        }

        /// <summary>Removes a source and its owned key, including the final source. Borrowed key files remain intact.</summary>
        public void Remove(ModConfig config, string id)
        {
            var entries = Load(config);
            var existing = FindId(entries, id);
            if (existing == null)
                return;
            entries.Remove(existing);
            WritePrivate(filePath, JsonSerializer.Serialize(entries, JsonOptionsProvider.WriteIndentedOptions));
            DeleteOwnedKey(existing.TokenFile);
        }

        /// <summary>Accepts a catalog URL or a bare server address. Bare origins resolve to the keyserver catalog endpoint.</summary>
        public static string NormalizeUrl(string value)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Catalog URL is required");
            if (!value.Contains("://", StringComparison.Ordinal))
            {
                var host = value.Split('/')[0].Split(':')[0];
                var local = IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
                value = (local ? "http://" : "https://") + value;
            }
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)
                || (uri.Scheme != "https" && uri.Scheme != "http") || uri.UserInfo.Length != 0
                || uri.Fragment.Length != 0 || uri.Query.Length != 0)
                throw new ArgumentException("Invalid catalog URL");
            if (uri.AbsolutePath == "/")
                uri = new Uri(uri, "/v1/catalog");
            return uri.AbsoluteUri;
        }

        private static void ValidatePrivateUrl(string url)
        {
            var uri = new Uri(url);
            if (uri.Scheme != "https" && !(IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip)))
                throw new ArgumentException("Private catalog requires HTTPS or loopback HTTP");
        }

        private static CatalogServer FindId(List<CatalogServer> entries, string id)
        {
            foreach (var entry in entries)
                if (entry.Id == id)
                    return entry;
            return null;
        }

        private static CatalogServer FindUrl(List<CatalogServer> entries, string url)
        {
            var normalized = NormalizeUrl(url);
            foreach (var entry in entries)
                if (NormalizeUrl(entry.Url) == normalized)
                    return entry;
            return null;
        }

        private void DeleteOwnedKey(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            var full = Path.GetFullPath(path);
            if (Path.GetDirectoryName(full) == keyDirectory && Guid.TryParseExact(Path.GetFileNameWithoutExtension(full), "N", out _)
                && Path.GetExtension(full) == ".token")
                File.Delete(full);
        }

        private static void WritePrivate(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows())
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (var stream = new FileStream(temporary, options))
                using (var writer = new StreamWriter(stream))
                    writer.Write(text);
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
