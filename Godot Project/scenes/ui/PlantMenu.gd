extends CanvasLayer

const MAX_ROWS: int = 5

var is_open: bool = false

@onready var _rows: Array[Label] = [
	$Panel/VBoxContainer/ListContainer/Row0,
	$Panel/VBoxContainer/ListContainer/Row1,
	$Panel/VBoxContainer/ListContainer/Row2,
	$Panel/VBoxContainer/ListContainer/Row3,
	$Panel/VBoxContainer/ListContainer/Row4,
]

func _ready() -> void:
	visible = false
	add_to_group("plant_menu")

func open() -> void:
	is_open = true
	visible = true
	_refresh()

func close() -> void:
	is_open = false
	visible = false

func _refresh() -> void:
	var shown: int = 0
	for i in PlayerProgress.crop_count():
		if not PlayerProgress.is_unlocked(i):
			continue
		if shown >= MAX_ROWS:
			break
		var data: CropData = PlayerProgress.get_crop_data(i)
		var row: Label = _rows[shown]
		row.text = "%s\nPlant Cost: %d    Harvest: %d    Decayed: %d\nGrow: %ds   Full-Value Window: %ds   Decay Window: %ds" % [
			data.display_name, data.plant_cost, data.harvest_value(), data.decayed_value(),
			roundi(data.grow_time), roundi(data.solid_time), roundi(data.decay_time),
		]
		row.visible = true
		shown += 1
	for i in range(shown, MAX_ROWS):
		_rows[i].visible = false
