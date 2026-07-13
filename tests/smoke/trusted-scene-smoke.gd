extends SceneTree

# Explicit maintained-scene allowlist. Do not replace this with directory discovery or project-wide import.
const SCENE_PATHS: PackedStringArray = [
    "res://scenes/battle/entities/units/BattleUnitBase.tscn",
    "res://tools/battle/UnitPreviewWorkbench.tscn",
]

var _failed: bool = false

func _initialize() -> void:
    call_deferred("_run")


func _run() -> void:
    for scene_path: String in SCENE_PATHS:
        var packed_scene := ResourceLoader.load(scene_path, "PackedScene") as PackedScene
        if packed_scene == null:
            _fail(scene_path, "load returned no PackedScene")
            continue

        var instance := packed_scene.instantiate()
        if instance == null:
            _fail(scene_path, "instantiate returned no Node")
            continue

        root.add_child(instance)
        await process_frame
        if not is_instance_valid(instance) or not instance.is_inside_tree():
            _fail(scene_path, "instance did not survive one lifecycle frame")
        else:
            print("PASS scene smoke: %s" % scene_path)
        instance.queue_free()
        await process_frame

    quit(1 if _failed else 0)


func _fail(scene_path: String, reason: String) -> void:
    _failed = true
    push_error("FAIL scene smoke [%s]: %s" % [scene_path, reason])
