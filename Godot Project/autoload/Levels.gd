extends Node

const LEVEL_2_COST: int = 500

signal level_2_unlocked

var is_level_2_unlocked: bool = false

func _ready() -> void:
	reset()

func reset() -> void:
	is_level_2_unlocked = false

func try_unlock_level_2() -> bool:
	if is_level_2_unlocked:
		return false
	if not Economy.spend_money(LEVEL_2_COST):
		return false
	is_level_2_unlocked = true
	level_2_unlocked.emit()
	return true
