extends Node2D
class_name Crop

enum State { GROWING, SOLID, DECAYING, GONE }

@export var grow_time: float = 5.0
@export var solid_time: float = 10.0
@export var decay_time: float = 5.0

@export var plant_cost: int = 10
@export var sell_value: int = 25
@export var decay_sell_multiplier: float = 0.5

@export var grow_color: Color = Color(0.42, 0.85, 0.55, 0.45)
@export var solid_color: Color = Color(1.0, 0.35, 0.55, 1.0)
@export var decay_color: Color = Color(0.45, 0.35, 0.3, 1.0)

signal state_changed(new_state: State)

const TRANSITION_TIME: float = 0.2

var current_state: State = State.GROWING

@onready var visual: ColorRect = $Visual

func _ready() -> void:
	_enter_growing()

func harvest() -> int:
	if current_state == State.GROWING or current_state == State.GONE:
		return 0
	var value: int = sell_value
	if current_state == State.DECAYING:
		value = int(round(sell_value * decay_sell_multiplier))
	queue_free()
	return value

func _enter_growing() -> void:
	current_state = State.GROWING
	state_changed.emit(current_state)
	_tween_color(grow_color, TRANSITION_TIME)
	await get_tree().create_timer(grow_time).timeout
	_enter_solid()

func _enter_solid() -> void:
	current_state = State.SOLID
	state_changed.emit(current_state)
	_tween_color(solid_color, TRANSITION_TIME)
	await get_tree().create_timer(solid_time).timeout
	_enter_decaying()

func _enter_decaying() -> void:
	current_state = State.DECAYING
	state_changed.emit(current_state)
	var color_time: float = min(TRANSITION_TIME, decay_time)
	var fade_time: float = max(decay_time - TRANSITION_TIME, 0.05)
	_tween_color(decay_color, color_time)
	var fade_tween := create_tween()
	fade_tween.tween_property(visual, "modulate:a", 0.0, fade_time)
	await get_tree().create_timer(decay_time).timeout
	_enter_gone()

func _enter_gone() -> void:
	current_state = State.GONE
	state_changed.emit(current_state)
	queue_free()

func _tween_color(target_color: Color, duration: float) -> void:
	var tween := create_tween()
	tween.tween_property(visual, "color", target_color, duration)
