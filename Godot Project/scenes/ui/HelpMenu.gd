extends CanvasLayer

var is_open: bool = false

func _ready() -> void:
	visible = false
	add_to_group("help_menu")

func open() -> void:
	is_open = true
	visible = true

func close() -> void:
	is_open = false
	visible = false
