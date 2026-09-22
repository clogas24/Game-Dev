extends Node

const CROP_DATA: Array[CropData] = [
	preload("res://data/crops/kelp.tres"),
	preload("res://data/crops/coral.tres"),
	preload("res://data/crops/sea_lettuce.tres"),
	preload("res://data/crops/dead_mans_fingers.tres"),
	preload("res://data/crops/mermaids_wine_glass.tres"),
]

const LEVEL_1_CROP_COUNT: int = 3

signal crop_unlocked(index: int)

var _unlocked: Array[bool] = []

func _ready() -> void:
	reset()

func reset() -> void:
	_unlocked.resize(CROP_DATA.size())
	for i in CROP_DATA.size():
		_unlocked[i] = CROP_DATA[i].unlock_cost <= 0

func crop_count() -> int:
	return CROP_DATA.size()

func get_crop_data(index: int) -> CropData:
	return CROP_DATA[index]

func is_unlocked(index: int) -> bool:
	return _unlocked[index]

func get_locked_indices() -> Array[int]:
	var result: Array[int] = []
	for i in CROP_DATA.size():
		if not _unlocked[i] and is_available(i):
			result.append(i)
	return result

func is_available(index: int) -> bool:
	return index < LEVEL_1_CROP_COUNT or Levels.is_level_2_unlocked

func all_unlocked() -> bool:
	return get_locked_indices().is_empty()

func try_unlock(index: int) -> bool:
	if _unlocked[index]:
		return false
	if not is_available(index):
		return false
	if not Economy.spend_money(CROP_DATA[index].unlock_cost):
		return false
	_unlocked[index] = true
	crop_unlocked.emit(index)
	return true
