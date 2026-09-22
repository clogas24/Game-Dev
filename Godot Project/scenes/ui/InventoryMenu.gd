extends CanvasLayer

const CURSOR_COLOR: Color = Color(1, 0.9, 0.3, 1)
const NORMAL_COLOR: Color = Color(1, 1, 1, 1)
const MAX_ROWS: int = 7

signal crop_chosen(index: int)
signal upgrade_chosen(type: int)

var is_open: bool = false

var _entries: Array[Dictionary] = []
var _cursor: int = 0

@onready var _rows: Array[Label] = [
	$Panel/VBoxContainer/ListContainer/Row0,
	$Panel/VBoxContainer/ListContainer/Row1,
	$Panel/VBoxContainer/ListContainer/Row2,
	$Panel/VBoxContainer/ListContainer/Row3,
	$Panel/VBoxContainer/ListContainer/Row4,
	$Panel/VBoxContainer/ListContainer/Row5,
	$Panel/VBoxContainer/ListContainer/Row6,
]

func _ready() -> void:
	visible = false
	add_to_group("inventory_menu")
	PlayerProgress.crop_unlocked.connect(_on_catalog_changed)
	Upgrades.stock_changed.connect(_on_catalog_changed)

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

func choose_selected() -> void:
	if _entries.is_empty():
		return
	var entry: Dictionary = _entries[_cursor]
	if entry.type == "crop":
		crop_chosen.emit(entry.index)
	else:
		upgrade_chosen.emit(entry.upgrade_type)
	close()

func _on_catalog_changed(_arg = null) -> void:
	if is_open:
		_refresh()

func _build_entries() -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for i in PlayerProgress.crop_count():
		if not PlayerProgress.is_unlocked(i):
			continue
		var data: CropData = PlayerProgress.get_crop_data(i)
		entries.append({
			"type": "crop",
			"index": i,
			"label": "%s — Plant Cost: %d" % [data.display_name, data.plant_cost],
		})
	for upgrade_type in Upgrades.ALL_TYPES:
		var stock: int = Upgrades.stock(upgrade_type)
		if stock <= 0:
			continue
		entries.append({
			"type": "upgrade",
			"upgrade_type": upgrade_type,
			"label": "%s (%d in stock) — place with E" % [Upgrades.display_name(upgrade_type), stock],
		})
	return entries

func _refresh() -> void:
	_entries = _build_entries()
	if _cursor >= _entries.size():
		_cursor = max(_entries.size() - 1, 0)
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
