extends RefCounted

func get_name():
	return "gds.light_reset"

func run(ctx):
	ctx.Lighting.ClearDepthMap()
	ctx.Lighting.SetUseTime(false)
	ctx.Log("GDS", "Lighting reset via GDScript")
