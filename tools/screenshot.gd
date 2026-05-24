extends SceneTree

# Load main.tscn, render for a few frames so the shader has time to animate,
# capture the viewport, save to /tmp/hacker-game-screenshot.png. Used for
# verifying visual output without needing to open the editor.

func _initialize() -> void:
	var packed: PackedScene = load("res://scenes/main.tscn") as PackedScene
	if packed == null:
		push_error("could not load res://scenes/main.tscn")
		quit(1)
		return
	var instance := packed.instantiate()
	root.add_child(instance)
	# Run for a handful of frames so TIME advances and the shader produces motion.
	for i in range(30):
		await process_frame
	var img := root.get_viewport().get_texture().get_image()
	if img == null:
		push_error("could not capture viewport")
		quit(1)
		return
	var out_path := "/tmp/hacker-game-screenshot.png"
	var err := img.save_png(out_path)
	if err != OK:
		push_error("save_png failed: %d" % err)
		quit(1)
		return
	print("screenshot saved -> ", out_path, " (", img.get_width(), "x", img.get_height(), ")")
	quit(0)
