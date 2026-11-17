namespace Cthangover.TestRunner
{
    using System.Text.Json;

    /// <summary>Publishes immutable ready/lease receipts only after the complete temporary file has reached the filesystem.</summary>
    internal static class PreparedInputJson
    {
        internal static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, value, ReportWriter.JsonOptions);
                    stream.Flush(true);
                }

                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        internal static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path))
            {
                return null;
            }

            PreparedInputFiles.RejectLink(path);
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReportWriter.JsonOptions);
        }
    }
}
