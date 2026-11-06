using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Cthangover.Core.Mods.Caches;

namespace Engine.Tests.Mods
{
	public class RemoteIconPolicyTests
	{
		[Theory]
		[InlineData("my_mod")]
		[InlineData("mod.v2-x")]
		[InlineData("A1")]
		public void IsSafeModId_AcceptsPlainIds(string modId)
		{
			Assert.True(RemoteIconPolicy.IsSafeModId(modId));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData(".")]
		[InlineData("..")]
		[InlineData("../evil")]
		[InlineData("..\\evil")]
		[InlineData("C:\\Users\\x\\evil")]
		[InlineData("/etc/evil")]
		[InlineData("a/b")]
		[InlineData("a\\b")]
		[InlineData("a:b")]
		[InlineData(".hidden")]
		[InlineData("-x")]
		[InlineData("мод")]
		public void IsSafeModId_RefusesPathsAndOddNames(string modId)
		{
			Assert.False(RemoteIconPolicy.IsSafeModId(modId));
		}

		[Fact]
		public void IsSafeModId_RefusesOverlongIds()
		{
			Assert.True(RemoteIconPolicy.IsSafeModId(new string('a', 128)));
			Assert.False(RemoteIconPolicy.IsSafeModId(new string('a', 129)));
		}

		[Fact]
		public void IsAllowedUrl_AcceptsHttps()
		{
			Assert.True(RemoteIconPolicy.IsAllowedUrl("https://cdn.example.com/icons/mod.png"));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("http://cdn.example.com/mod.png")]
		[InlineData("ftp://cdn.example.com/mod.png")]
		[InlineData("file:///C:/Windows/win.ini")]
		[InlineData("https://user:secret@cdn.example.com/mod.png")]
		[InlineData("/icons/mod.png")]
		public void IsAllowedUrl_RefusesOtherSchemesCredentialsAndRelativeUrls(string url)
		{
			Assert.False(RemoteIconPolicy.IsAllowedUrl(url));
		}

		[Fact]
		public void IsAllowedUrl_RefusesOverlongUrls()
		{
			Assert.False(RemoteIconPolicy.IsAllowedUrl("https://cdn.example.com/" + new string('a', 2048)));
		}

		[Theory]
		[InlineData("8.8.8.8")]
		[InlineData("1.1.1.1")]
		[InlineData("172.32.0.1")]
		[InlineData("100.128.0.1")]
		[InlineData("2606:4700::1111")]
		public void IsPublicAddress_AcceptsPublicAddresses(string address)
		{
			Assert.True(RemoteIconPolicy.IsPublicAddress(IPAddress.Parse(address)));
		}

		[Theory]
		[InlineData("127.0.0.1")]
		[InlineData("10.1.2.3")]
		[InlineData("172.16.0.1")]
		[InlineData("172.31.255.255")]
		[InlineData("192.168.1.1")]
		[InlineData("169.254.169.254")]
		[InlineData("100.64.0.1")]
		[InlineData("0.0.0.0")]
		[InlineData("224.0.0.1")]
		[InlineData("255.255.255.255")]
		[InlineData("::1")]
		[InlineData("::")]
		[InlineData("fe80::1")]
		[InlineData("fd00::1")]
		[InlineData("ff02::1")]
		[InlineData("::ffff:192.168.1.1")]
		public void IsPublicAddress_RefusesLocalAndReservedAddresses(string address)
		{
			Assert.False(RemoteIconPolicy.IsPublicAddress(IPAddress.Parse(address)));
		}

		[Fact]
		public void IsPublicAddress_RefusesNull()
		{
			Assert.False(RemoteIconPolicy.IsPublicAddress(null));
		}

		/// <summary>A host name that resolves to loopback is refused before any connection is made.</summary>
		[Fact]
		public async Task ConnectPublicAsync_NeverConnectsToLocalAddresses()
		{
			using var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var port = ((IPEndPoint)listener.LocalEndpoint).Port;
			using var client = new HttpClient(new SocketsHttpHandler { ConnectCallback = RemoteIconPolicy.ConnectPublicAsync });

			await Assert.ThrowsAsync<HttpRequestException>(() => client.GetByteArrayAsync($"https://localhost:{port}/icon.png"));
			Assert.False(listener.Pending());
		}
	}
}
