namespace Cthangover.TestRunner
{
    using System.Runtime.InteropServices;

    /// <summary>Builds only unpublished exclusively owned inputs and verifies complete output hashes before later reuse.</summary>
    internal static class PreparedBuildCache
    {
        private const string Policy = "content_build_v3_hashed_ide_outputs";

        internal static string Toolchain(PreparedBuildRequest request, CancellationToken cancellation)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.ArtifactsDirectory);
            var artifacts = Path.GetFullPath(request.ArtifactsDirectory);
            Directory.CreateDirectory(artifacts);
            var version = ProcessRunner.Run("dotnet", new[] { "--version" }, artifacts, 30,
                Path.Combine(artifacts, "toolchain.log"), cancellationToken: cancellation);
            if (version.ExitCode != 0 || version.TimedOut)
            {
                throw new IOException("Cannot establish effective dotnet SDK identity");
            }

            return version.OutputText.Trim() + ";" + RuntimeInformation.OSDescription + ";"
                + RuntimeInformation.ProcessArchitecture + ";" + RuntimeInformation.FrameworkDescription;
        }

        internal static bool Validate(string root, PreparedInputManifest manifest, PreparedBuildRequest request,
            string toolchain, InputPreparationMetrics metrics)
        {
            if (request == null)
            {
                return true;
            }

            var identity = Identity(manifest.Files, toolchain);
            metrics.BuildIdentity = identity;
            var reason = manifest.Build == null ? "build-receipt-missing"
                : manifest.Build.Identity != identity || request.IncludeUnit && !manifest.Build.IncludesUnit
                    ? "build-input-mismatch" : null;
            if (reason == null)
            {
                if (manifest.Build.Outputs.Count == 0)
                {
                    reason = "build-artifact-mismatch";
                }

                foreach (var output in manifest.Build.Outputs)
                {
                    if (!PreparedInputFiles.Matches(root, output))
                    {
                        reason = "build-artifact-mismatch";
                        break;
                    }
                }
            }

            if (reason == null)
            {
                metrics.BuildCacheHit = true;
                return true;
            }

            if (request.NoBuild)
            {
                throw new PreparedInputRefusal(reason, metrics);
            }

            return false;
        }

        internal static void Ensure(string root, PreparedInputManifest manifest, PreparedBuildRequest request,
            string toolchain, InputPreparationMetrics metrics, CancellationToken cancellation)
        {
            if (request == null)
            {
                manifest.Build = null;
                return;
            }

            if (Validate(root, manifest, request, toolchain, metrics))
            {
                return;
            }

            metrics.BuildCacheHit = false;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (IsOutput(relative))
                {
                    PreparedInputFiles.RejectLink(file);
                    PreparedInputFiles.Writable(file);
                }
            }

            var project = request.IncludeUnit ? "tests/Cthangover.Tests/Cthangover.Tests.csproj" : "Cthangover.csproj";
            var arguments = new[]
            {
                "build",
                project,
                // Content identity, not source mtime, invalidates this receipt. MSBuild's timestamp
                // checks can otherwise reuse stale outputs after a same-length, same-mtime edit.
                "--no-incremental",
                "--nologo",
                "-v",
                "minimal"
            };
            var processClaim = new PendingProcessClaim(root, root, Guid.NewGuid().ToString("N"), "build",
                "inputs-v1-" + manifest.SourceFingerprint);
            var run = ProcessRunner.Run("dotnet", arguments, root, 600,
                Path.Combine(request.ArtifactsDirectory, "build.log"), cancellationToken: cancellation,
                onCompleted: processClaim.OnCompleted);
            metrics.BuildSeconds = run.Duration.TotalSeconds;
            metrics.BuildCpuSeconds = run.CpuSeconds;
            metrics.BuildPeakMemoryMb = run.PeakMemoryMb;
            if (!run.CleanupSafe)
            {
                throw new PreparedInputRefusal("prepared-process-unconfirmed", metrics);
            }
            if (run.ExitCode != 0 || run.TimedOut)
            {
                throw new PreparedInputRefusal("build-failed", metrics);
            }

            var outputs = new List<PreparedInputFile>();
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (IsOutput(relative))
                {
                    outputs.Add(PreparedInputFiles.Read(root, file, cancellation));
                }
            }

            if (outputs.Count == 0)
            {
                throw new PreparedInputRefusal("build-artifact-mismatch", metrics);
            }

            manifest.Build = new PreparedBuildReceipt
            {
                Identity = Identity(manifest.Files, toolchain),
                IncludesUnit = request.IncludeUnit,
                Outputs = outputs
            };
            metrics.BuildIdentity = manifest.Build.Identity;
        }

        private static string Identity(IEnumerable<PreparedInputFile> files, string toolchain)
        {
            var inputs = new List<PreparedInputFile>();
            foreach (var file in files)
            {
                if (!file.IsDirectory && (file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    || file.Path.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
                    || file.Path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase)
                    || file.Path.EndsWith(".config", StringComparison.OrdinalIgnoreCase)
                    || file.Path.EndsWith(".globalconfig", StringComparison.OrdinalIgnoreCase)
                    || file.Path == "global.json" || file.Path == ".editorconfig"))
                {
                    inputs.Add(file);
                }
            }

            return PreparedInputFiles.Fingerprint(inputs, toolchain + "\n" + Policy);
        }

        private static bool IsOutput(string relative)
        {
            return relative.StartsWith(".godot/mono/temp/bin/Debug/", StringComparison.Ordinal)
                || relative.StartsWith(".ide/bin/", StringComparison.Ordinal)
                || relative.StartsWith("bin/Debug/", StringComparison.Ordinal)
                || relative.Contains("/bin/Debug/", StringComparison.Ordinal);
        }
    }
}
