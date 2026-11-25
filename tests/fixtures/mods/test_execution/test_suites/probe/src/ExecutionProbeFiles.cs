namespace Cthangover.Mods.ExecutionProbe
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using Cthangover.Core.Mods.Providers;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Publishes launch evidence atomically and exercises a small physical provider fixture with exact restoration.</summary>
    public static class ExecutionProbeFiles
    {
        /// <summary>Identities are copied from the already validated Core context, never inferred from output folder spelling.</summary>
        public static Dictionary<string, object> Record(TestExecutionContext context, string mode)
        {
            return new Dictionary<string, object>
            {
                ["schemaVersion"] = 1,
                ["runId"] = context.RunId,
                ["testId"] = context.TestId,
                ["attemptId"] = context.AttemptId,
                ["launchId"] = context.LaunchId,
                ["workerId"] = context.WorkerId,
                ["preparedRevision"] = context.PreparedRevision,
                ["inputIdentity"] = context.InputIdentity,
                ["leaseId"] = context.LeaseId,
                ["osPid"] = Environment.ProcessId,
                ["mode"] = mode
            };
        }

        /// <summary>CreateNew staging plus rename prevents a reader from observing a partial JSON document.</summary>
        public static void Publish(string path, Dictionary<string, object> record)
        {
            var staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, record);
                    stream.Flush(true);
                }

                File.Move(staging, path);
            }
            finally
            {
                if (File.Exists(staging))
                {
                    File.Delete(staging);
                }
            }
        }

        /// <summary>Returns the SHA-256 of actual UTF-8 payload bytes for independent source-integrity checks.</summary>
        public static string Digest(string text)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        /// <summary>Reads actual on-disk PNG bytes; the capture itself is performed by the shared viewport helper.</summary>
        public static string FileDigest(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        /// <summary>Provider reads observe overwrite, delete and restore in a private sandbox; no shared mod is written.</summary>
        public static void ExerciseFixture(string root, string original, string launchId)
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "payload.json");
            var addition = Path.Combine(root, "created.json");
            File.WriteAllText(path, original);
            using var provider = new FolderModFileProvider(root, "execution_fixture");
            try
            {
                File.WriteAllText(path, launchId);
                using (var stream = provider.OpenStream("payload.json"))
                using (var reader = new StreamReader(stream))
                {
                    ModAssert.Equal(launchId, reader.ReadToEnd(), "provider observes private overwrite");
                }

                File.WriteAllText(addition, launchId);
                ModAssert.Equal(addition, provider.GetFileSystemPath("created.json"), "provider resolves physical addition");
                File.Delete(path);
                ModAssert.False(File.Exists(path), "physical delete precedes restoration");
            }
            finally
            {
                File.WriteAllText(path, original);
                if (File.Exists(addition))
                {
                    File.Delete(addition);
                }
            }

            using var restoredStream = provider.OpenStream("payload.json");
            using var restoredReader = new StreamReader(restoredStream);
            ModAssert.Equal(original, restoredReader.ReadToEnd(), "provider observes restored bytes");
            ModAssert.False(File.Exists(addition), "addition removed after fixture restoration");
        }
    }
}
