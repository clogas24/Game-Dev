extends Node

@export var starting_money: int = 100

signal money_changed(new_amount: int)

var money: int = 0

func _ready() -> void:
	money = starting_money
	money_changed.emit(money)

func add_money(amount: int) -> void:
	money += amount
	money_changed.emit(money)

func spend_money(amount: int) -> bool:
	if amount > money:
		return false
	money -= amount
	money_changed.emit(money)
	return true
