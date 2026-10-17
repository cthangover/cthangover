extends RefCounted

func get_name():
	return "gds.quest_setup"

func run(ctx):
	var quest_id = ctx.GetParam("quest_id")
	if quest_id == "":
		ctx.LogWarning("GDS", "gds.quest_setup: missing quest_id")
		return

	if ctx.Quests.Exists(quest_id):
		ctx.Quests.SetStatus(quest_id, "Active")
		ctx.Quests.AddTag(quest_id, "gds_started")
		ctx.Quests.SendNotification(quest_id)
		ctx.Log("GDS", "Quest '" + quest_id + "' activated by GDScript")
