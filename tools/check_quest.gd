extends SceneTree

# Load a .tres and print the parsed fields. Quick sanity check on the
# resource format. Pass the .tres path as the only argument:
#   godot-mono --headless --path . --script tools/check_quest.gd \
#       -- content/levels/01-recon/q1_get_help.tres

func _initialize() -> void:
	var args := OS.get_cmdline_args()
	var path := ""
	for i in range(args.size()):
		if args[i] == "--" and i + 1 < args.size():
			path = args[i + 1]
			break
	if path == "":
		path = "res://content/levels/01-recon/q1_get_help.tres"
	if not path.begins_with("res://"):
		path = "res://" + path
	print("loading ", path)
	var res = load(path)
	if res == null:
		push_error("load returned null")
		quit(1)
		return
	print("class: ", res.get_class())
	print("script: ", res.get_script())
	for prop in res.get_property_list():
		if prop.name in ["resource_local_to_scene", "resource_path", "resource_name", "resource_scene_unique_id", "script", "Resource"]:
			continue
		var v = res.get(prop.name)
		var rendered = str(v)
		if rendered.length() > 80:
			rendered = rendered.substr(0, 77) + "..."
		print("  %s = %s" % [prop.name, rendered])
	quit(0)
