extends CharacterBody2D

@export var move_speed: float = 300.0

const CropScene: PackedScene = preload("res://scenes/crop/Crop.tscn")

signal crop_selected(index: int)
signal upgrade_selected(type: int)

var selected_crop_index: int = 0
var selected_plot: Plot = null
var pending_upgrade: int = -1

var _shop: Node = null
var _help: Node = null
var _plant_menu: Node = null
var _inventory: Node = null
var _auto_planter_menu: Node = null

@onready var visual: TextureRect = $Visual

func _ready() -> void:
	add_to_group("player")
	_shop = get_tree().get_first_node_in_group("shop")
	_help = get_tree().get_first_node_in_group("help_menu")
	_plant_menu = get_tree().get_first_node_in_group("plant_menu")
	_inventory = get_tree().get_first_node_in_group("inventory_menu")
	_auto_planter_menu = get_tree().get_first_node_in_group("auto_planter_menu")
	if _inventory != null:
		_inventory.crop_chosen.connect(_select_crop)
		_inventory.upgrade_chosen.connect(_select_upgrade)

func _physics_process(_delta: float) -> void:
	if _menu_open():
		velocity = Vector2.ZERO
		move_and_slide()
		return
	var input_dir := Vector2(
		Input.get_action_strength("move_right") - Input.get_action_strength("move_left"),
		Input.get_action_strength("move_down") - Input.get_action_strength("move_up")
	).normalized()
	if input_dir.x != 0:
		visual.flip_h = input_dir.x > 0
	if input_dir.y != 0:
		visual.flip_v = input_dir.y < 0
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
	if event.is_action_pressed("toggle_inventory"):
		_toggle_overlay(_inventory)
		return
	if event.is_action_pressed("open_auto_planter_menu"):
		_try_open_auto_planter_menu()
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
	if _inventory != null and _inventory.is_open:
		return _inventory
	if _auto_planter_menu != null and _auto_planter_menu.is_open:
		return _auto_planter_menu
	return null

func _handle_interact_pressed() -> void:
	var active := _active_menu()
	if active != null:
		active.close()
	elif pending_upgrade != -1:
		_try_place_upgrade()
	else:
		_try_plot_interact()

func _try_open_auto_planter_menu() -> void:
	if _auto_planter_menu == null or _menu_open():
		return
	if selected_plot == null or not selected_plot.has_auto_planter:
		return
	_auto_planter_menu.open_for(selected_plot)

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
	elif active == _inventory or active == _auto_planter_menu:
		if event.is_action_pressed("move_up"):
			active.move_cursor(-1)
		elif event.is_action_pressed("move_down"):
			active.move_cursor(1)
		elif event.is_action_pressed("ui_accept") or event.is_action_pressed("plant"):
			active.choose_selected()

func _select_crop(index: int) -> void:
	if not PlayerProgress.is_unlocked(index):
		return
	pending_upgrade = -1
	selected_crop_index = index
	crop_selected.emit(selected_crop_index)

func _select_upgrade(type: int) -> void:
	pending_upgrade = type
	upgrade_selected.emit(type)

func _try_place_upgrade() -> void:
	if selected_plot == null or not selected_plot.owned:
		return
	if pending_upgrade == Upgrades.Type.AUTO_HARVESTER:
		if selected_plot.has_auto_harvester:
			return
		if Upgrades.try_consume(Upgrades.Type.AUTO_HARVESTER):
			selected_plot.place_auto_harvester()
			_revert_to_crop_if_out_of_stock(Upgrades.Type.AUTO_HARVESTER)
	elif pending_upgrade == Upgrades.Type.AUTO_PLANTER:
		if not selected_plot.has_auto_harvester or selected_plot.has_auto_planter:
			return
		if Upgrades.try_consume(Upgrades.Type.AUTO_PLANTER):
			selected_plot.place_auto_planter()
			_revert_to_crop_if_out_of_stock(Upgrades.Type.AUTO_PLANTER)

func _revert_to_crop_if_out_of_stock(type: int) -> void:
	if Upgrades.stock(type) <= 0:
		pending_upgrade = -1
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
