extends Node

@onready var _harvest_player: AudioStreamPlayer = $HarvestPlayer
@onready var _move_player: AudioStreamPlayer = $MovePlayer

var _move_active: bool = false

func _ready() -> void:
	_move_player.finished.connect(_on_move_finished)

func play_harvest() -> void:
	if _harvest_player.stream != null:
		_harvest_player.play()

func set_moving(moving: bool) -> void:
	if moving == _move_active:
		return
	_move_active = moving
	if moving:
		_play_move_once()
	else:
		_move_player.stop()

func _play_move_once() -> void:
	if _move_player.stream != null:
		_move_player.play()

func _on_move_finished() -> void:
	if _move_active:
		_play_move_once()
