using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Save settings shown under the base menu in the mods settings tab.
    /// Games keep the data of mechanics the player turned off (dormant slots,
    /// see <see cref="SaveDormancy"/>) so it returns with the mechanic; this
    /// is where the player drops that data from every save on purpose.
    /// </summary>
    public class SaveSettings : IModSettings, IModSettingsActions
    {
        public const string PurgeDormantKey = "purge_dormant";

        public IReadOnlyList<ModSettingDefinition> GetDefinitions() => new List<ModSettingDefinition>
        {
            new()
            {
                Key = PurgeDormantKey,
                Name = "settings/saves/purge_dormant",
                Type = SettingType.Action,
                ActionText = "settings/saves/purge_dormant_button",
                ConfirmText = "settings/saves/purge_dormant_confirm",
            },
        };

        public void WriteValues(DataBlob blob)
        {
        }

        public void ReadValues(DataBlob blob)
        {
        }

        public string RunAction(string key)
        {
            if (key != PurgeDormantKey)
                return null;

            var purged = SaveService.PurgeDormantData();
            return string.Format(TranslationServer.Translate("settings/saves/purge_dormant_done"), purged);
        }
    }
}
