extends Node2D
class_name Crop

enum State { GROWING, SOLID, DECAYING, GONE }

@export var data: CropData

signal state_changed(new_state: State)

const TRANSITION_TIME: float = 0.2
const GROW_TINT: Color = Color(0.55, 0.55, 0.55, 0.5)
const DECAY_TINT: Color = Color(0.35, 0.3, 0.22, 1.0)
const FULL_TINT: Color = Color(1, 1, 1, 1)

var current_state: State = State.GROWING

@onready var visual: TextureRect = $Visual

func _ready() -> void:
	visual.texture = data.texture
	_enter_growing()

func harvest() -> int:
	if current_state == State.GROWING or current_state == State.GONE:
		return 0
	var value: int = data.harvest_value()
	if current_state == State.DECAYING:
		value = data.decayed_value()
	queue_free()
	return value

func _enter_growing() -> void:
	current_state = State.GROWING
	state_changed.emit(current_state)
	_tween_modulate(GROW_TINT, TRANSITION_TIME)
	await get_tree().create_timer(data.grow_time).timeout
	_enter_solid()

func _enter_solid() -> void:
	current_state = State.SOLID
	state_changed.emit(current_state)
	_tween_modulate(FULL_TINT, TRANSITION_TIME)
	await get_tree().create_timer(data.solid_time).timeout
	_enter_decaying()

func _enter_decaying() -> void:
	current_state = State.DECAYING
	state_changed.emit(current_state)
	var color_time: float = min(TRANSITION_TIME, data.decay_time)
	var fade_time: float = max(data.decay_time - TRANSITION_TIME, 0.05)
	_tween_modulate(DECAY_TINT, color_time)
	var fade_tween := create_tween()
	fade_tween.tween_property(visual, "modulate:a", 0.0, fade_time)
	await get_tree().create_timer(data.decay_time).timeout
	_enter_gone()

func _enter_gone() -> void:
	current_state = State.GONE
	state_changed.emit(current_state)
	queue_free()

func _tween_modulate(target_color: Color, duration: float) -> void:
	var tween := create_tween()
	tween.tween_property(visual, "modulate", target_color, duration)
