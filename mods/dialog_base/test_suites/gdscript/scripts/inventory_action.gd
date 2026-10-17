extends RefCounted

# Demonstrates inventory queries and mod presence checks from a scenario action
# Usage in scenario DSL: action gds.inventory_demo

func get_name():
	return "gds.inventory_demo"

func run(ctx):
	var itemId = ctx.GetParam("item_id")
	
	if ctx.Inventory.HasItem(itemId):
		var count = ctx.Inventory.CheckCount(itemId)
		ctx.Log("EVENT", "Inventory has " + itemId + " x" + str(count))
	else:
		ctx.Log("EVENT", "Inventory does NOT have " + itemId)
	
	if ctx.ModRegistry.IsLoaded("core"):
		ctx.Log("EVENT", "Core mod is loaded")
	else:
		ctx.LogWarning("EVENT", "Core mod is NOT loaded")
