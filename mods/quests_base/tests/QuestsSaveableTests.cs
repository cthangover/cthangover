namespace Cthangover.Mods.Quests.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Quests;
    using Cthangover.Core.Settings;

    /// <summary>
    /// The quests slot holds the state of every content mod's quests, so a
    /// quest whose mod left the game is still in a loaded save: it must not
    /// take part in the game, and the next save must write it back so its
    /// progress returns with the mod.
    /// </summary>
    public class QuestsSaveableTests
    {
        private const string UnknownId = "__test_no_such_quest";

        [ModTest("save/unknown_quest_survives_load_and_save")]
        static void UnknownQuestSurvivesLoadAndSave(ModTestContext ctx)
        {
            var factory = QuestFactory.Instance;
            var before = JsonSerializer.Serialize(factory.GetAll());
            try
            {
                var unknown = new QuestBase { ID = UnknownId, Status = QuestStatus.Progress };
                unknown.Data.Tags.Add("met");
                var slot = new ModSlot();
                slot.Data["Quests"] = new DataBlob(1, new List<QuestBase> { unknown });
                var saveable = new QuestsSaveable();

                ModAssert.True(saveable.OnLoad(slot, out _), "slot with an unknown quest loads");
                ModAssert.False(factory.GetAll().Any(q => q.ID == UnknownId), "unknown quest takes no part in the game");

                var saved = new ModSlot();
                saveable.OnSave(saved);
                var kept = saved.Data["Quests"].ReadJson<List<QuestBase>>().SingleOrDefault(q => q.ID == UnknownId);
                ModAssert.NotNull(kept, "unknown quest written back");
                ModAssert.True(kept.Status == QuestStatus.Progress && kept.Data.Tags.Contains("met"), "its status and tags kept");
            }
            finally
            {
                factory.SetAll(JsonSerializer.Deserialize<List<QuestBase>>(before));
            }
        }
    }
}
