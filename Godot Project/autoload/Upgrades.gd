extends Node

enum Type { AUTO_HARVESTER, AUTO_PLANTER }

const ALL_TYPES: Array[Type] = [Type.AUTO_HARVESTER, Type.AUTO_PLANTER]

const COST: Dictionary = {
	Type.AUTO_HARVESTER: 500,
	Type.AUTO_PLANTER: 500,
}

const DISPLAY_NAME: Dictionary = {
	Type.AUTO_HARVESTER: "Auto Harvester",
	Type.AUTO_PLANTER: "Auto Planter",
}

signal stock_changed(type: Type)

var _stock: Dictionary = {
	Type.AUTO_HARVESTER: 0,
	Type.AUTO_PLANTER: 0,
}

func cost(type: Type) -> int:
	return COST[type]

func display_name(type: Type) -> String:
	return DISPLAY_NAME[type]

func stock(type: Type) -> int:
	return _stock[type]

func try_buy(type: Type) -> bool:
	if not Economy.spend_money(COST[type]):
		return false
	_stock[type] += 1
	stock_changed.emit(type)
	return true

func try_consume(type: Type) -> bool:
	if _stock[type] <= 0:
		return false
	_stock[type] -= 1
	stock_changed.emit(type)
	return true

func reset() -> void:
	for type in ALL_TYPES:
		_stock[type] = 0
		stock_changed.emit(type)
