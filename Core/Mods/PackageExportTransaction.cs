namespace Cthangover.Core.Mods
{
    using System;
    using System.IO;

    /// <summary>Publishes a prepared archive/catalog pair with rollback on runtime errors; each file replacement is atomic.</summary>
    internal static class PackageExportTransaction
    {
        internal static void Publish(string archiveTemp, string archivePath, string catalogTemp, string catalogPath)
        {
            if (Directory.Exists(archivePath) || Directory.Exists(catalogPath))
                throw new IOException("An export destination is a directory");
            var suffix = "." + Guid.NewGuid().ToString("N") + ".previous";
            var archiveBackup = archivePath + suffix;
            var catalogBackup = catalogPath + suffix;
            var hadArchive = File.Exists(archivePath);
            var hadCatalog = File.Exists(catalogPath);
            var archiveChanged = false;
            var catalogChanged = false;
            var restored = false;
            try
            {
                if (hadArchive)
                    File.Copy(archivePath, archiveBackup);
                if (hadCatalog)
                    File.Copy(catalogPath, catalogBackup);
                File.Move(archiveTemp, archivePath, true);
                archiveChanged = true;
                File.Move(catalogTemp, catalogPath, true);
                catalogChanged = true;
                restored = true;
            }
            catch
            {
                if (archiveChanged)
                    Restore(archiveBackup, archivePath, hadArchive);
                if (catalogChanged)
                    Restore(catalogBackup, catalogPath, hadCatalog);
                restored = true;
                throw;
            }
            finally
            {
                // Preserve recovery copies if rollback itself failed.
                if (restored)
                {
                    File.Delete(archiveBackup);
                    File.Delete(catalogBackup);
                }
            }
        }

        private static void Restore(string backup, string destination, bool existed)
        {
            if (existed)
                File.Move(backup, destination, true);
            else
                File.Delete(destination);
        }
    }
}
