extends StaticBody2D
class_name Plot

const CLICK_SIZE: Vector2 = Vector2(64, 64)

@export var owned: bool = false
@export var unlock_cost: int = 50

var crop: Crop = null
var selected: bool = false

const LOCKED_COLOR: Color = Color(0.25, 0.45, 0.9, 1.0)
const EMPTY_COLOR: Color = Color(0.76, 0.69, 0.5, 1.0)
const OCCUPIED_COLOR: Color = Color(0.55, 0.5, 0.35, 1.0)

@onready var visual: ColorRect = $Visual
@onready var cost_label: Label = $CostLabel
@onready var selection_border: ColorRect = $SelectionBorder

func _ready() -> void:
	add_to_group("plots")
	_update_visual()

func contains_point(point: Vector2) -> bool:
	var half := CLICK_SIZE * 0.5
	var local := point - global_position
	return absf(local.x) <= half.x and absf(local.y) <= half.y

func unlock() -> void:
	owned = true
	_update_visual()

func plant(crop_instance: Crop) -> void:
	crop = crop_instance
	crop.tree_exiting.connect(_on_crop_freed, CONNECT_ONE_SHOT)
	_update_visual()

func set_selected(value: bool) -> void:
	selected = value
	selection_border.visible = value

func _on_crop_freed() -> void:
	crop = null
	_update_visual()

func _update_visual() -> void:
	cost_label.visible = not owned
	cost_label.text = str(unlock_cost)
	if not owned:
		visual.color = LOCKED_COLOR
	elif crop == null:
		visual.color = EMPTY_COLOR
	else:
		visual.color = OCCUPIED_COLOR
