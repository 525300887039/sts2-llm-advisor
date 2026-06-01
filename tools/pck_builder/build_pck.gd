# Headless PCK packer for the STS2 AI Advisor mod.
#
# Independently authored for this project. Packs a single mod_manifest.json into the
# root of a .pck (as res://mod_manifest.json) using Godot's PCKPacker API.
#
# Invocation:
#   godot --headless --path tools/pck_builder --script build_pck.gd -- <manifest_json> <out_pck>
extends SceneTree

const MANIFEST_RES_PATH := "res://mod_manifest.json"

func _initialize() -> void:
	var user_args: PackedStringArray = OS.get_cmdline_user_args()
	if user_args.size() < 2:
		_fail("expected two arguments: <manifest_json> <out_pck>")
		return

	var manifest_path: String = user_args[0]
	var pck_path: String = user_args[1]

	if not FileAccess.file_exists(manifest_path):
		_fail("manifest not found: %s" % manifest_path)
		return

	var packer := PCKPacker.new()

	if packer.pck_start(pck_path) != OK:
		_fail("could not open pck for writing: %s" % pck_path)
		return

	if packer.add_file(MANIFEST_RES_PATH, manifest_path) != OK:
		_fail("could not stage manifest into pck")
		return

	if packer.flush() != OK:
		_fail("could not write pck to disk")
		return

	print("[build_pck] wrote %s" % pck_path)
	quit(0)

func _fail(reason: String) -> void:
	push_error("[build_pck] %s" % reason)
	quit(1)
