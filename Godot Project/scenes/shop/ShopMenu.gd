extends CanvasLayer

const CURSOR_COLOR: Color = Color(1, 0.9, 0.3, 1)
const NORMAL_COLOR: Color = Color(1, 1, 1, 1)
const DENY_COLOR: Color = Color(1, 0.2, 0.2, 1)
const MAX_ROWS: int = 4

var is_open: bool = false

var _locked_indices: Array[int] = []
var _cursor: int = 0

@onready var empty_label: Label = $Panel/VBoxContainer/EmptyLabel
@onready var list_container: VBoxContainer = $Panel/VBoxContainer/ListContainer
@onready var _rows: Array[Label] = [
	$Panel/VBoxContainer/ListContainer/Row0,
	$Panel/VBoxContainer/ListContainer/Row1,
	$Panel/VBoxContainer/ListContainer/Row2,
	$Panel/VBoxContainer/ListContainer/Row3,
]

func _ready() -> void:
	visible = false
	PlayerProgress.crop_unlocked.connect(_on_crop_unlocked)

func open() -> void:
	is_open = true
	visible = true
	_cursor = 0
	_refresh()

func close() -> void:
	is_open = false
	visible = false

func move_cursor(delta: int) -> void:
	if _locked_indices.is_empty():
		return
	_cursor = (_cursor + delta + _locked_indices.size()) % _locked_indices.size()
	_refresh()

func attempt_purchase() -> void:
	if _locked_indices.is_empty():
		return
	var index: int = _locked_indices[_cursor]
	if not PlayerProgress.try_unlock(index):
		_flash_insufficient()

func _on_crop_unlocked(_index: int) -> void:
	if is_open:
		_refresh()

func _refresh() -> void:
	_locked_indices = PlayerProgress.get_locked_indices()
	if _cursor >= _locked_indices.size():
		_cursor = max(_locked_indices.size() - 1, 0)
	var locked_empty: bool = _locked_indices.is_empty()
	empty_label.visible = locked_empty
	list_container.visible = not locked_empty
	for i in MAX_ROWS:
		var row: Label = _rows[i]
		if i < _locked_indices.size():
			var data: CropData = PlayerProgress.get_crop_data(_locked_indices[i])
			var prefix: String = "> " if i == _cursor else "    "
			row.text = "%s%s — %d" % [prefix, data.display_name, data.unlock_cost]
			row.modulate = CURSOR_COLOR if i == _cursor else NORMAL_COLOR
			row.visible = true
		else:
			row.visible = false

func _flash_insufficient() -> void:
	if _cursor >= MAX_ROWS:
		return
	var row: Label = _rows[_cursor]
	var tween := create_tween()
	tween.tween_property(row, "modulate", DENY_COLOR, 0.08)
	tween.tween_property(row, "modulate", CURSOR_COLOR, 0.08)
	tween.tween_property(row, "modulate", DENY_COLOR, 0.08)
	tween.tween_property(row, "modulate", CURSOR_COLOR, 0.08)
