extends StaticBody2D
class_name Plot

const CLICK_SIZE: Vector2 = Vector2(64, 64)
const CropScene: PackedScene = preload("res://scenes/crop/Crop.tscn")

@export var owned: bool = false
@export var unlock_cost: int = 50

var crop: Crop = null
var selected: bool = false
var has_auto_harvester: bool = false
var has_auto_planter: bool = false
var auto_planter_crop_index: int = -1

const LOCKED_COLOR: Color = Color(0.25, 0.45, 0.9, 1.0)
const EMPTY_COLOR: Color = Color(0.76, 0.69, 0.5, 1.0)
const OCCUPIED_COLOR: Color = Color(0.55, 0.5, 0.35, 1.0)

@onready var visual: ColorRect = $Visual
@onready var cost_label: Label = $CostLabel
@onready var selection_border: ColorRect = $SelectionBorder
@onready var auto_harvester_border: ColorRect = $AutoHarvesterBorder
@onready var auto_planter_border: ColorRect = $AutoPlanterBorder

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
	if has_auto_harvester:
		crop.state_changed.connect(_on_crop_state_changed)
	_update_visual()

func set_selected(value: bool) -> void:
	selected = value
	selection_border.visible = value

func place_auto_harvester() -> void:
	if has_auto_harvester:
		return
	has_auto_harvester = true
	auto_harvester_border.visible = true
	if crop != null and not crop.state_changed.is_connected(_on_crop_state_changed):
		crop.state_changed.connect(_on_crop_state_changed)

func place_auto_planter() -> void:
	if has_auto_planter or not has_auto_harvester:
		return
	has_auto_planter = true
	auto_planter_border.visible = true

func set_auto_planter_crop(index: int) -> void:
	auto_planter_crop_index = index
	_try_auto_plant()

func _on_crop_state_changed(new_state: Crop.State) -> void:
	if new_state == Crop.State.SOLID:
		_auto_harvest()

func _auto_harvest() -> void:
	if crop == null:
		return
	var value: int = crop.harvest()
	if value > 0:
		Economy.add_money(value)
		Sfx.play_harvest()

func _on_crop_freed() -> void:
	crop = null
	_update_visual()
	_try_auto_plant()

func _try_auto_plant() -> void:
	if not has_auto_planter or auto_planter_crop_index < 0 or crop != null or not owned:
		return
	if not PlayerProgress.is_unlocked(auto_planter_crop_index):
		return
	var data: CropData = PlayerProgress.get_crop_data(auto_planter_crop_index)
	if not Economy.spend_money(data.plant_cost):
		return
	var new_crop: Crop = CropScene.instantiate()
	new_crop.data = data
	new_crop.global_position = global_position
	# _try_auto_plant() can run from _on_crop_freed(), which fires while the
	# scene tree is mid-removal of the old crop, so add_child() must be
	# deferred or the parent rejects it ("Parent node is busy...").
	get_parent().add_child.call_deferred(new_crop)
	plant(new_crop)

func _update_visual() -> void:
	cost_label.visible = not owned
	cost_label.text = str(unlock_cost)
	if not owned:
		visual.color = LOCKED_COLOR
	elif crop == null:
		visual.color = EMPTY_COLOR
	else:
		visual.color = OCCUPIED_COLOR
