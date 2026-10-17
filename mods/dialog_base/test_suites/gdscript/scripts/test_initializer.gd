extends RefCounted

func on_mod_loaded(mod_id):
	print("[GDS_TEST] on_mod_loaded: " + mod_id)

func on_mod_resources_ready():
	print("[GDS_TEST] on_mod_resources_ready: all mods loaded")
