extends CanvasLayer

const CURSOR_COLOR: Color = Color(1, 0.9, 0.3, 1)
const NORMAL_COLOR: Color = Color(1, 1, 1, 1)
const MAX_ROWS: int = 5

var is_open: bool = false

var _target_plot: Plot = null
var _unlocked_indices: Array[int] = []
var _cursor: int = 0

@onready var _rows: Array[Label] = [
	$Panel/VBoxContainer/ListContainer/Row0,
	$Panel/VBoxContainer/ListContainer/Row1,
	$Panel/VBoxContainer/ListContainer/Row2,
	$Panel/VBoxContainer/ListContainer/Row3,
	$Panel/VBoxContainer/ListContainer/Row4,
]

func _ready() -> void:
	visible = false
	add_to_group("auto_planter_menu")
	PlayerProgress.crop_unlocked.connect(_on_crop_unlocked)

func open_for(plot: Plot) -> void:
	_target_plot = plot
	is_open = true
	visible = true
	_cursor = 0
	_refresh()
	if plot != null:
		var existing: int = _unlocked_indices.find(plot.auto_planter_crop_index)
		if existing >= 0:
			_cursor = existing
			_refresh()

func close() -> void:
	is_open = false
	visible = false
	_target_plot = null

func move_cursor(delta: int) -> void:
	if _unlocked_indices.is_empty():
		return
	_cursor = (_cursor + delta + _unlocked_indices.size()) % _unlocked_indices.size()
	_refresh()

func choose_selected() -> void:
	if _unlocked_indices.is_empty() or _target_plot == null:
		return
	_target_plot.set_auto_planter_crop(_unlocked_indices[_cursor])
	close()

func _on_crop_unlocked(_index: int) -> void:
	if is_open:
		_refresh()

func _refresh() -> void:
	_unlocked_indices = []
	for i in PlayerProgress.crop_count():
		if PlayerProgress.is_unlocked(i):
			_unlocked_indices.append(i)
	if _cursor >= _unlocked_indices.size():
		_cursor = max(_unlocked_indices.size() - 1, 0)
	for i in MAX_ROWS:
		var row: Label = _rows[i]
		if i < _unlocked_indices.size():
			var data: CropData = PlayerProgress.get_crop_data(_unlocked_indices[i])
			var prefix: String = "> " if i == _cursor else "    "
			row.text = "%s%s — Plant Cost: %d" % [prefix, data.display_name, data.plant_cost]
			row.modulate = CURSOR_COLOR if i == _cursor else NORMAL_COLOR
			row.visible = true
		else:
			row.visible = false
