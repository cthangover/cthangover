namespace Mods.ModManager
{
    /// <summary>
    /// Business rules deciding which operation a mod tile offers. The tile shows
    /// a single contextual button; its meaning follows the state machine:
    /// catalog-only → Install, newer remote → Update, older remote → Downgrade,
    /// same version but edited locally → Reinstall, otherwise → Delete for
    /// deletable local mods and nothing for protected/shipped ones.
    /// </summary>
    public static class ModActions
    {
        /// <summary>Primary action for a tile/detail page, or null when there is nothing to do.</summary>
        public static ModActionKind? GetPrimary(ModCardData data)
        {
            if (data == null)
                return null;

            if (!data.IsInstalled)
                return data.HasRemote ? ModActionKind.Install : null;

            if (data.HasRemote)
            {
                switch (data.Status)
                {
                    case ModItemStatus.UpdateAvailable:
                        return ModActionKind.Update;
                    case ModItemStatus.DowngradeAvailable:
                        return ModActionKind.Downgrade;
                    case ModItemStatus.ModifiedLocally:
                        return ModActionKind.Reinstall;
                }
            }

            return data.IsDeletable ? ModActionKind.Delete : null;
        }

        /// <summary>True for actions that download and install files.</summary>
        public static bool IsInstall(ModActionKind kind)
            => kind == ModActionKind.Install
               || kind == ModActionKind.Update
               || kind == ModActionKind.Downgrade
               || kind == ModActionKind.Reinstall;
    }
}
