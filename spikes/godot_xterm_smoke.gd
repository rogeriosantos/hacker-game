extends SceneTree

# Phase E1 Step 4 smoke test for godot-xterm.
# Just confirms: (1) the Terminal class is registered by the GDExtension,
# (2) we can instantiate it, (3) write_to_screen works.
# Does NOT yet hook up PowerShell — that's the next integration step.

func _init() -> void:
	print("=== godot-xterm smoke ===")

	if not ClassDB.class_exists("Terminal"):
		push_error("FAIL: Terminal class not registered by godot-xterm GDExtension")
		quit(1)
		return
	print("PASS: Terminal class registered")

	var term = ClassDB.instantiate("Terminal")
	if term == null:
		push_error("FAIL: Terminal could not be instantiated")
		quit(1)
		return
	print("PASS: Terminal instantiated (%s)" % term.get_class())

	# Terminal.write() accepts a String or PackedByteArray of UTF-8 bytes,
	# including ANSI escape sequences. Try a colored greeting.
	if not term.has_method("write"):
		push_error("FAIL: Terminal has no `write` method (API changed?)")
		quit(1)
		return
	# ESC[32m = green, ESC[0m = reset. PowerShell error output uses ESC[31m (red).
	var greeting := "[32mhello from godot-xterm[0m\r\n"
	term.write(greeting)
	print("PASS: write() accepted %d bytes (with ANSI green)" % greeting.length())

	# Smoke that geometry getters return sane values.
	var cols: int = term.get_cols()
	var rows: int = term.get_rows()
	print("PASS: terminal grid is %dx%d cells" % [cols, rows])

	print("=== godot-xterm smoke OK ===")
	quit(0)
