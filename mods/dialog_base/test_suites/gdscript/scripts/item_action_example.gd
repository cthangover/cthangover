extends RefCounted

# Custom item action: heals the character by a fixed amount when the item is used
# ID must match the ItemAction field in the item's JSON data
# The use(item) method receives a GDItem (with fields Id, Name, Description, Cost, ItemType)
# Returns true on success

func get_item_action_id():
	return "gds.heal_item"

func use(item):
	print("[GDS_ITEM] Using item: " + item.Name + " (id=" + item.Id + ")")
	
	# In a real mod, perform actual healing logic here (e.g. via player stats)
	# For demonstration, simply return success
	return true
