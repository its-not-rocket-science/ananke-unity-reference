extends Node3D

@onready var status_label: Label3D = $StatusLabel
@onready var websocket_client: AnankeWebSocketClient = $AnankeWebSocketClient

var _entity_nodes := {}
var _bone_maps := {}

func _ready() -> void:
	websocket_client.frame_received.connect(_on_frame_received)
	_create_placeholder_character(1, Vector3(-1.5, 0.0, 0.0), Color.CYAN)
	_create_placeholder_character(2, Vector3(1.5, 0.0, 0.0), Color.CRIMSON)
	status_label.text = "Connecting to %s" % websocket_client.stream_url

func _on_frame_received(frame: Dictionary) -> void:
	status_label.text = "Tick %s · entities %s" % [frame.get("tick", 0), frame.get("entityCount", 0)]
	for snapshot in frame.get("snapshots", []):
		_apply_snapshot(snapshot)

func _apply_snapshot(snapshot: Dictionary) -> void:
	var entity_id: int = int(snapshot.get("entityId", 0))
	if not _entity_nodes.has(entity_id):
		return

	var root: Node3D = _entity_nodes[entity_id]
	var position: Dictionary = snapshot.get("position", {})
	root.position = Vector3(
		float(position.get("x", 0.0)),
		float(position.get("z", 0.0)),
		float(position.get("y", 0.0))
	)

	var animation: Dictionary = snapshot.get("animation", {})
	var shock: float = float(animation.get("shockQ", 0)) / 10000.0
	var tint := Color.WHITE.lerp(Color.GOLD, clamp(shock, 0.0, 1.0))
	if bool(animation.get("dead", false)):
		tint = Color.DIM_GRAY
	elif bool(animation.get("unconscious", false)):
		tint = Color.LIGHT_STEEL_BLUE

	for mesh in root.find_children("*", "MeshInstance3D", true, false):
		if mesh.material_override is StandardMaterial3D:
			mesh.material_override.albedo_color = tint

	for modifier in snapshot.get("pose", []):
		var segment_id: String = modifier.get("segmentId", "")
		if not _bone_maps[entity_id].has(segment_id):
			continue
		var bone: Node3D = _bone_maps[entity_id][segment_id]
		var impairment := float(modifier.get("impairmentQ", 0)) / 10000.0
		bone.scale = Vector3.ONE * (1.0 + impairment * 0.12)
		bone.rotation_degrees.x = impairment * 8.0

func _create_placeholder_character(entity_id: int, spawn_position: Vector3, colour: Color) -> void:
	var root := Node3D.new()
	root.name = "Entity%s" % entity_id
	root.position = spawn_position
	add_child(root)

	_create_bone(root, "Pelvis", Vector3(0.0, 0.0, 0.0), Vector3(0.35, 0.25, 0.20), colour)
	_create_bone(root, "Torso", Vector3(0.0, 0.55, 0.0), Vector3(0.40, 0.55, 0.22), colour)
	_create_bone(root, "Head", Vector3(0.0, 1.15, 0.0), Vector3(0.25, 0.25, 0.25), colour)
	_create_bone(root, "LeftArm", Vector3(-0.40, 0.65, 0.0), Vector3(0.18, 0.45, 0.18), colour)
	_create_bone(root, "RightArm", Vector3(0.40, 0.65, 0.0), Vector3(0.18, 0.45, 0.18), colour)
	_create_bone(root, "LeftLeg", Vector3(-0.15, -0.55, 0.0), Vector3(0.20, 0.55, 0.20), colour)
	_create_bone(root, "RightLeg", Vector3(0.15, -0.55, 0.0), Vector3(0.20, 0.55, 0.20), colour)

	_entity_nodes[entity_id] = root
	_bone_maps[entity_id] = AnankeSkeletonMapper.index_nodes(root)

func _create_bone(parent: Node3D, name: String, offset: Vector3, size: Vector3, colour: Color) -> void:
	var bone := MeshInstance3D.new()
	bone.name = name
	bone.position = offset
	bone.mesh = BoxMesh.new()
	bone.scale = size

	var material := StandardMaterial3D.new()
	material.albedo_color = colour
	bone.material_override = material
	parent.add_child(bone)
