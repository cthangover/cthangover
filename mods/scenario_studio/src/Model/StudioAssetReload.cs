namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;

    /// <summary>
    /// What the "Resources" button found: how many backgrounds the game has now and
    /// which of them appeared since the last read (files dropped in while the
    /// game ran). A replaced picture is not "added" — it is simply shown anew.
    /// </summary>
    public sealed class StudioAssetReload
    {
        public int Total { get; set; }
        public List<string> Added { get; set; } = new();
    }
}
