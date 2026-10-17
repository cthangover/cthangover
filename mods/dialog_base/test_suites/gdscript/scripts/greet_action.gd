extends RefCounted

func get_name():
	return "gds.greet"

func run(ctx):
	var name = ctx.GetParam("name")
	if name == "":
		name = "Stranger"
	ctx.Log("EVENT", "GDScript greet action: Hello, " + name + "!")
