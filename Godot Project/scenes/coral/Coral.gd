extends StaticBody2D

enum State { GROWING, SOLID, DECAYING, GONE }

@export var grow_time: float = 5.0
@export var solid_time: float = 10.0
@export var decay_time: float = 5.0

signal state_changed(new_state: State)

const TRANSITION_TIME: float = 0.2
const GROW_COLOR: Color = Color(0.42, 0.85, 0.55, 0.45)
const SOLID_COLOR: Color = Color(1.0, 0.35, 0.55, 1.0)
const DECAY_COLOR: Color = Color(0.45, 0.35, 0.3, 1.0)

var current_state: State = State.GROWING

@onready var visual: ColorRect = $Visual
@onready var collision_shape: CollisionShape2D = $CollisionShape2D

func _ready() -> void:
	_enter_growing()

func _enter_growing() -> void:
	current_state = State.GROWING
	state_changed.emit(current_state)
	collision_shape.disabled = true
	_tween_color(GROW_COLOR, TRANSITION_TIME)
	await get_tree().create_timer(grow_time).timeout
	_enter_solid()

func _enter_solid() -> void:
	current_state = State.SOLID
	state_changed.emit(current_state)
	collision_shape.disabled = false
	_tween_color(SOLID_COLOR, TRANSITION_TIME)
	await get_tree().create_timer(solid_time).timeout
	_enter_decaying()

func _enter_decaying() -> void:
	current_state = State.DECAYING
	state_changed.emit(current_state)
	# Collision turns off the moment decay starts, not at the end of it.
	# Decaying coral is visibly crumbling, so it should stop reading as
	# safe footing right away rather than staying "solid" underfoot while
	# it looks unstable, and it avoids a player getting stranded when the
	# node frees itself out from under them.
	collision_shape.disabled = true
	var color_time: float = min(TRANSITION_TIME, decay_time)
	var fade_time: float = max(decay_time - TRANSITION_TIME, 0.05)
	_tween_color(DECAY_COLOR, color_time)
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
