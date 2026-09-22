extends CanvasLayer

@onready var restart_button: Button = $Panel/VBoxContainer/RestartButton

func _ready() -> void:
	visible = false
	process_mode = Node.PROCESS_MODE_ALWAYS
	restart_button.pressed.connect(_on_restart_pressed)

func show_win() -> void:
	if visible:
		return
	visible = true
	get_tree().paused = true

func _on_restart_pressed() -> void:
	get_tree().paused = false
	Economy.reset()
	PlayerProgress.reset()
	Upgrades.reset()
	get_tree().reload_current_scene()
