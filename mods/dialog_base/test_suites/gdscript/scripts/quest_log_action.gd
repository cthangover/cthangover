extends RefCounted

func get_name():
	return "gds.quest_log"

func run(ctx):
	var quest_id = ctx.GetParam("quest_id")
	var message = ctx.GetParam("message")
	if quest_id == "":
		ctx.LogWarning("GDS", "gds.quest_log: missing quest_id param")
		return
	ctx.Log("QUEST", "[" + quest_id + "] " + message)
