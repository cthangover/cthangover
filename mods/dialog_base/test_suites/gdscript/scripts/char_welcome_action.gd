extends RefCounted

func get_name():
	return "gds.char_welcome"

func run(ctx):
	var char_type = ctx.GetParam("char_type")
	if char_type == "":
		ctx.LogWarning("GDS", "gds.char_welcome: missing char_type")
		return

	ctx.Character.AddToParty(char_type)
	ctx.Character.SendNotification(char_type)
	ctx.Log("GDS", "Character '" + char_type + "' added to party via GDScript")
