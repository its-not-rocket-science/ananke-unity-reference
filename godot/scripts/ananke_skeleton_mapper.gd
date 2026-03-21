extends RefCounted
class_name AnankeSkeletonMapper

const SEGMENT_TO_NODE := {
	"head": "Head",
	"neck": "Neck",
	"torso": "Torso",
	"thorax": "Torso",
	"abdomen": "Torso",
	"pelvis": "Pelvis",
	"leftArm": "LeftArm",
	"rightArm": "RightArm",
	"leftLeg": "LeftLeg",
	"rightLeg": "RightLeg"
}

static func resolve_segment(segment_id: String) -> String:
	return SEGMENT_TO_NODE.get(segment_id, "Torso")

static func index_nodes(root: Node) -> Dictionary:
	var indexed := {}
	for segment_id in SEGMENT_TO_NODE.keys():
		var node_name: String = SEGMENT_TO_NODE[segment_id]
		var node := root.find_child(node_name, true, false)
		if node != null:
			indexed[segment_id] = node
	return indexed
