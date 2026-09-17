extends Area2D

const CLICK_SIZE: Vector2 = Vector2(80, 100)

@onready var menu: Node = $ShopMenu

func _ready() -> void:
	add_to_group("shop")
	body_exited.connect(_on_body_exited)

func contains_point(point: Vector2) -> bool:
	var half := CLICK_SIZE * 0.5
	var local := point - global_position
	return absf(local.x) <= half.x and absf(local.y) <= half.y

func open_menu() -> void:
	menu.open()

func _on_body_exited(body: Node) -> void:
	if body.is_in_group("player") and menu.is_open:
		menu.close()
