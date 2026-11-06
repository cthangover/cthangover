namespace Cthangover.Core.Mods.Caches
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// What <see cref="ModIconCache"/> may do with a catalog entry's <c>id</c> and
    /// <c>icon_url</c>. Both come from whoever runs a catalog, and a player adds
    /// third-party catalogs (anyone can host a private one), so neither is trusted:
    /// <list type="bullet">
    ///   <item>the id becomes a file name under the icons folder, so it must not
    ///   contain separators, drive letters or <c>..</c>;</item>
    ///   <item>the URL must be HTTPS without credentials, and the connection may only
    ///   go to a public address. The address check runs at connect time
    ///   (<see cref="ConnectPublicAsync"/>), after DNS, so a public host name that
    ///   resolves to a router or a localhost service is refused as well;</item>
    ///   <item>an icon is bounded by <see cref="MaxIconBytes"/>.</item>
    /// </list>
    /// </summary>
    public static class RemoteIconPolicy
    {
        /// <summary>Largest icon response accepted; bigger responses fail the download.</summary>
        public const int MaxIconBytes = 2 * 1024 * 1024;

        private const int MaxModIdLength = 128;
        private const int MaxUrlLength = 2048;

        /// <summary>
        /// True when the id can name a cache file: 1–128 ASCII letters, digits,
        /// <c>_</c>, <c>-</c> or <c>.</c>, starting with a letter or digit.
        /// </summary>
        public static bool IsSafeModId(string modId)
        {
            if (string.IsNullOrEmpty(modId) || modId.Length > MaxModIdLength || !char.IsAsciiLetterOrDigit(modId[0]))
                return false;
            return modId.All(IsModIdChar);
        }

        /// <summary>True for an absolute HTTPS URL with a host and without user info.</summary>
        public static bool IsAllowedUrl(string iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl) || iconUrl.Length > MaxUrlLength)
                return false;
            return Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrEmpty(uri.Host)
                && string.IsNullOrEmpty(uri.UserInfo);
        }

        /// <summary>
        /// True for a globally routable unicast address. Loopback, private,
        /// carrier-grade NAT, link-local, unique-local, multicast, reserved and
        /// unspecified ranges are refused; IPv4-mapped IPv6 is judged as IPv4.
        /// </summary>
        public static bool IsPublicAddress(IPAddress address)
        {
            if (address == null)
                return false;
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224
                    || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                    || (b[0] == 169 && b[1] == 254)
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                    || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                    || (b[0] == 192 && b[1] == 168)
                    || (b[0] == 198 && (b[1] == 18 || b[1] == 19)));
            }
            if (address.AddressFamily != AddressFamily.InterNetworkV6)
                return false;
            var v6 = address.GetAddressBytes();
            return !(IPAddress.IPv6Loopback.Equals(address) || IPAddress.IPv6None.Equals(address)
                || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (v6[0] & 0xFE) == 0xFC);
        }

        /// <summary>
        /// <see cref="SocketsHttpHandler.ConnectCallback"/> that resolves the host and
        /// connects only to its public addresses, trying them in order.
        /// </summary>
        public static async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var endPoint = context.DnsEndPoint;
            var addresses = await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken);
            Exception lastError = null;
            foreach (var address in addresses.Where(IsPublicAddress))
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, endPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex) when (ex is SocketException || ex is OperationCanceledException)
                {
                    socket.Dispose();
                    lastError = ex;
                    if (ex is OperationCanceledException)
                        throw;
                }
            }
            throw new HttpRequestException($"Icon host '{endPoint.Host}' has no reachable public address", lastError);
        }

        private static bool IsModIdChar(char c)
        {
            return char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-' || c == '.';
        }
    }
}
