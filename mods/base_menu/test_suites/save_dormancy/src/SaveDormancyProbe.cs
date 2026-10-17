using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Mods.SaveDormancyTest
{
    /// <summary>
    /// Saveable of the test mod: records whether a load restored its slot or
    /// told it that it joined an existing game without data.
    /// </summary>
    public class SaveDormancyProbe : IModSaveable
    {
        public static bool Joined { get; set; }

        public static bool Restored { get; set; }

        public void OnSave(ModSlot slot)
        {
            slot.Data["probe"] = new DataBlob();
        }

        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            Restored = true;
            errorMessage = null;
            return true;
        }

        public void OnJoinedExistingGame()
        {
            Joined = true;
        }
    }
}
