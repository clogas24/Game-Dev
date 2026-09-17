extends CharacterBody2D

@export var move_speed: float = 300.0

const CropScene: PackedScene = preload("res://scenes/crop/Crop.tscn")

signal crop_selected(index: int)

var selected_crop_index: int = 0
var selected_plot: Plot = null

var _shop: Node = null
var _help: Node = null
var _plant_menu: Node = null

func _ready() -> void:
	add_to_group("player")
	_shop = get_tree().get_first_node_in_group("shop")
	_help = get_tree().get_first_node_in_group("help_menu")
	_plant_menu = get_tree().get_first_node_in_group("plant_menu")

func _physics_process(_delta: float) -> void:
	if _menu_open():
		velocity = Vector2.ZERO
		move_and_slide()
		return
	var input_dir := Vector2(
		Input.get_action_strength("move_right") - Input.get_action_strength("move_left"),
		Input.get_action_strength("move_down") - Input.get_action_strength("move_up")
	).normalized()
	velocity = input_dir * move_speed
	move_and_slide()

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("interact"):
		_handle_interact_pressed()
		return
	if event.is_action_pressed("toggle_help"):
		_toggle_overlay(_help)
		return
	if event.is_action_pressed("toggle_plants"):
		_toggle_overlay(_plant_menu)
		return
	if _menu_open():
		_handle_menu_input(event)
		return
	if event.is_action_pressed("select_crop_1"):
		_select_crop(0)
	elif event.is_action_pressed("select_crop_2"):
		_select_crop(1)
	elif event.is_action_pressed("select_crop_3"):
		_select_crop(2)
	elif event.is_action_pressed("select_crop_4"):
		_select_crop(3)
	elif event.is_action_pressed("select_crop_5"):
		_select_crop(4)
	elif event.is_action_pressed("plant"):
		_try_plant()
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		_handle_click(get_global_mouse_position())
	elif event is InputEventScreenTouch and event.pressed:
		_handle_click(event.position)

func _menu_open() -> bool:
	return _active_menu() != null

func _active_menu() -> Node:
	if _shop != null and _shop.menu.is_open:
		return _shop.menu
	if _help != null and _help.is_open:
		return _help
	if _plant_menu != null and _plant_menu.is_open:
		return _plant_menu
	return null

func _handle_interact_pressed() -> void:
	var active := _active_menu()
	if active != null:
		active.close()
	else:
		_try_plot_interact()

func _toggle_overlay(overlay: Node) -> void:
	if overlay == null:
		return
	if overlay.is_open:
		overlay.close()
	elif not _menu_open():
		overlay.open()

func _handle_menu_input(event: InputEvent) -> void:
	var active := _active_menu()
	if active == null:
		return
	if event.is_action_pressed("ui_cancel"):
		active.close()
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		active.close()
	elif event is InputEventScreenTouch and event.pressed:
		active.close()
	elif active == _shop.menu:
		if event.is_action_pressed("move_up"):
			active.move_cursor(-1)
		elif event.is_action_pressed("move_down"):
			active.move_cursor(1)
		elif event.is_action_pressed("ui_accept"):
			active.attempt_purchase()

func _select_crop(index: int) -> void:
	if not PlayerProgress.is_unlocked(index):
		return
	selected_crop_index = index
	crop_selected.emit(selected_crop_index)

func _handle_click(world_pos: Vector2) -> void:
	if _shop != null and _shop.contains_point(world_pos):
		_shop.open_menu()
		return
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
	if not PlayerProgress.is_unlocked(selected_crop_index):
		return
	var data: CropData = PlayerProgress.get_crop_data(selected_crop_index)
	var crop: Crop = CropScene.instantiate()
	crop.data = data
	if not Economy.spend_money(data.plant_cost):
		crop.queue_free()
		return
	# Crop must outlive the player walking away, so it goes on Farm
	# (our parent in the scene tree) rather than as our own child.
	crop.global_position = selected_plot.global_position
	get_parent().add_child(crop)
	selected_plot.plant(crop)

func _try_plot_interact() -> void:
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
