extends CanvasLayer

const CURSOR_COLOR: Color = Color(1, 0.9, 0.3, 1)
const NORMAL_COLOR: Color = Color(1, 1, 1, 1)
const DENY_COLOR: Color = Color(1, 0.2, 0.2, 1)
const MAX_ROWS: int = 6

var is_open: bool = false

var _entries: Array[Dictionary] = []
var _cursor: int = 0

@onready var empty_label: Label = $Panel/VBoxContainer/EmptyLabel
@onready var list_container: VBoxContainer = $Panel/VBoxContainer/ListContainer
@onready var _rows: Array[Label] = [
	$Panel/VBoxContainer/ListContainer/Row0,
	$Panel/VBoxContainer/ListContainer/Row1,
	$Panel/VBoxContainer/ListContainer/Row2,
	$Panel/VBoxContainer/ListContainer/Row3,
	$Panel/VBoxContainer/ListContainer/Row4,
	$Panel/VBoxContainer/ListContainer/Row5,
]

func _ready() -> void:
	visible = false
	PlayerProgress.crop_unlocked.connect(_on_catalog_changed)
	Upgrades.stock_changed.connect(_on_catalog_changed)
	Levels.level_2_unlocked.connect(_on_catalog_changed)

func open() -> void:
	is_open = true
	visible = true
	_cursor = 0
	_refresh()

func close() -> void:
	is_open = false
	visible = false

func move_cursor(delta: int) -> void:
	if _entries.is_empty():
		return
	_cursor = (_cursor + delta + _entries.size()) % _entries.size()
	_refresh()

func attempt_purchase() -> void:
	if _entries.is_empty():
		return
	var entry: Dictionary = _entries[_cursor]
	var success: bool
	if entry.type == "crop":
		success = PlayerProgress.try_unlock(entry.index)
	elif entry.type == "level":
		success = Levels.try_unlock_level_2()
	else:
		success = Upgrades.try_buy(entry.upgrade_type)
	if not success:
		_flash_insufficient()

func _on_catalog_changed(_arg = null) -> void:
	if is_open:
		_refresh()

func _build_entries() -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if not Levels.is_level_2_unlocked:
		entries.append({
			"type": "level",
			"label": "Unlock Level 2 — %d (auto upgrades + 2 more plants)" % Levels.LEVEL_2_COST,
		})
	for index in PlayerProgress.get_locked_indices():
		var data: CropData = PlayerProgress.get_crop_data(index)
		entries.append({
			"type": "crop",
			"index": index,
			"label": "%s — %d" % [data.display_name, data.unlock_cost],
		})
	if Levels.is_level_2_unlocked:
		for upgrade_type in Upgrades.ALL_TYPES:
			entries.append({
				"type": "upgrade",
				"upgrade_type": upgrade_type,
				"label": "%s — %d (own %d)" % [
					Upgrades.display_name(upgrade_type), Upgrades.cost(upgrade_type), Upgrades.stock(upgrade_type),
				],
			})
	return entries

func _refresh() -> void:
	_entries = _build_entries()
	if _cursor >= _entries.size():
		_cursor = max(_entries.size() - 1, 0)
	var is_empty: bool = _entries.is_empty()
	empty_label.visible = is_empty
	list_container.visible = not is_empty
	for i in MAX_ROWS:
		var row: Label = _rows[i]
		if i < _entries.size():
			var entry: Dictionary = _entries[i]
			var prefix: String = "> " if i == _cursor else "    "
			row.text = "%s%s" % [prefix, entry.label]
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
