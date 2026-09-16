extends CharacterBody2D

@export var move_speed: float = 300.0

const CropScenes: Array[PackedScene] = [
	preload("res://scenes/crop/CropKelp.tscn"),
	preload("res://scenes/crop/CropCoral.tscn"),
]

signal crop_selected(index: int)

var selected_crop_index: int = 0
var selected_plot: Plot = null

func _ready() -> void:
	add_to_group("player")

func _physics_process(_delta: float) -> void:
	var input_dir := Vector2(
		Input.get_action_strength("move_right") - Input.get_action_strength("move_left"),
		Input.get_action_strength("move_down") - Input.get_action_strength("move_up")
	).normalized()
	velocity = input_dir * move_speed
	move_and_slide()

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("select_crop_1"):
		selected_crop_index = 0
		crop_selected.emit(selected_crop_index)
	elif event.is_action_pressed("select_crop_2"):
		selected_crop_index = 1
		crop_selected.emit(selected_crop_index)
	elif event.is_action_pressed("plant"):
		_try_plant()
	elif event.is_action_pressed("interact"):
		_try_interact()
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		_handle_click(get_global_mouse_position())
	elif event is InputEventScreenTouch and event.pressed:
		_handle_click(event.position)

func _handle_click(world_pos: Vector2) -> void:
	for plot in get_tree().get_nodes_in_group("plots"):
		if plot.contains_point(world_pos):
			_select_plot(plot)
			return
	_deselect_plot()

func _select_plot(plot: Plot) -> void:
	if selected_plot == plot:
		return
	_deselect_plot()
	selected_plot = plot
	selected_plot.set_selected(true)

func _deselect_plot() -> void:
	if selected_plot:
		selected_plot.set_selected(false)
		selected_plot = null

func _try_plant() -> void:
	if selected_plot == null or not selected_plot.owned or selected_plot.crop != null:
		return
	var scene: PackedScene = CropScenes[selected_crop_index]
	var crop: Crop = scene.instantiate()
	if not Economy.spend_money(crop.plant_cost):
		crop.queue_free()
		return
	# Crop must outlive the player walking away, so it goes on Farm
	# (our parent in the scene tree) rather than as our own child.
	crop.global_position = selected_plot.global_position
	get_parent().add_child(crop)
	selected_plot.plant(crop)

func _try_interact() -> void:
	if selected_plot == null:
		return
	if not selected_plot.owned:
		if Economy.spend_money(selected_plot.unlock_cost):
			selected_plot.unlock()
		return
	if selected_plot.crop == null:
		return
	var state: Crop.State = selected_plot.crop.current_state
	if state == Crop.State.SOLID or state == Crop.State.DECAYING:
		var value: int = selected_plot.crop.harvest()
		if value > 0:
			Economy.add_money(value)
