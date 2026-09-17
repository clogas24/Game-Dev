extends Resource
class_name CropData

const HARVEST_MULTIPLIER: float = 2.5
const DECAYED_MULTIPLIER: float = 0.7

@export var display_name: String = ""
@export var plant_cost: int = 10
@export var grow_time: float = 5.0
@export var solid_time: float = 10.0
@export var decay_time: float = 5.0
@export var unlock_cost: int = 0
@export var color: Color = Color(1, 1, 1, 1)

func harvest_value() -> int:
	return int(round(plant_cost * HARVEST_MULTIPLIER))

func decayed_value() -> int:
	return int(round(plant_cost * DECAYED_MULTIPLIER))
