extends CharacterBody2D

@export var move_speed: float = 300.0
@export var drag: float = 900.0

func _physics_process(delta: float) -> void:
	var input_dir := Vector2(
		Input.get_action_strength("move_right") - Input.get_action_strength("move_left"),
		Input.get_action_strength("move_down") - Input.get_action_strength("move_up")
	).normalized()

	var target_velocity := input_dir * move_speed
	# drag doubles as accel/decel rate so speeding up and slowing down feel the same
	velocity = velocity.move_toward(target_velocity, drag * delta)
	move_and_slide()

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("plant"):
		print("plant requested at: ", global_position)
