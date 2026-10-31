namespace Engine.Tests.Mods
{
    /// <summary>Public fixture content shared by loader and provider contract tests.</summary>
    internal static class SevenZipFixtures
    {
        internal const string Password = "fixture-пароль";

        internal static string PathFor(string name) =>
            Path.Combine(AppContext.BaseDirectory, "fixtures", "sevenzip", name + ".7z");

        internal static byte[] Payload()
        {
            var bytes = new byte[262144];
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = (byte)(index % 256);
            return bytes;
        }
    }
}
