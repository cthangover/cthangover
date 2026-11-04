using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Attaches a keyserver bearer to one exact catalog request. The caller must
    /// disable redirects for authenticated requests. Download clients never use
    /// this header; their temporary URLs already grant access to one archive.
    /// </summary>
    public static class CatalogRequest
    {
        /// <summary>
        /// Creates a request with an optional file-backed bearer. The returned
        /// request belongs to the caller. File errors never include token text.
        /// </summary>
        public static HttpRequestMessage Create(string url, string tokenFile = null)
        {
            var uri = new Uri(url, UriKind.Absolute);
            string token = null;
            if (tokenFile != null)
            {
                if (uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
                    || (uri.Scheme != "https" && !(uri.Scheme == "http"
                        && IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip))))
                    throw new InvalidOperationException("Private catalog requires HTTPS or a literal loopback HTTP address without credentials, query or fragment");

                var path = GameEnvironment.IsRuntime && (tokenFile.StartsWith("user://") || tokenFile.StartsWith("res://"))
                    ? GamePaths.Globalize(tokenFile) : tokenFile;
                using var stream = File.OpenRead(path);
                if (stream.Length > 2048)
                    throw new InvalidOperationException("Invalid catalog token file size");
                using var reader = new StreamReader(stream);
                token = reader.ReadToEnd().Trim();
                ValidateToken(token);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        /// <summary>Validates the keyserver bearer without logging or echoing secret input.</summary>
        public static void ValidateToken(string token)
        {
            if (token == null || token.Length != 1024)
                throw new InvalidOperationException("Catalog token must contain 1024 Base64URL characters");
            foreach (var ch in token)
                if (!(ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_'))
                    throw new InvalidOperationException("Invalid catalog token format");
        }
    }
}
