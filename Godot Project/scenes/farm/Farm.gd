extends Node2D

@onready var player: CharacterBody2D = $Player
@onready var money_label: Label = $UI/MoneyLabel
@onready var crop_label: Label = $UI/CropLabel
@onready var help_button: Button = $UI/HelpButton
@onready var help_menu: Node = $HelpMenu
@onready var plant_menu_button: Button = $UI/PlantMenuButton
@onready var plant_menu: Node = $PlantMenu
@onready var inventory_menu_button: Button = $UI/InventoryMenuButton
@onready var inventory_menu: Node = $InventoryMenu
@onready var win_screen: Node = $WinScreen

const WIN_MONEY: int = 100000

var _held_upgrade: int = -1

func _ready() -> void:
	Economy.money_changed.connect(_on_money_changed)
	_on_money_changed(Economy.money)
	player.crop_selected.connect(_on_crop_selected)
	player.upgrade_selected.connect(_on_upgrade_selected)
	Upgrades.stock_changed.connect(_on_upgrade_stock_changed)
	_on_crop_selected(player.selected_crop_index)
	help_button.pressed.connect(help_menu.open)
	plant_menu_button.pressed.connect(plant_menu.open)
	inventory_menu_button.pressed.connect(inventory_menu.open)

func _on_money_changed(new_amount: int) -> void:
	money_label.text = "Money: %d" % new_amount
	if new_amount >= WIN_MONEY:
		win_screen.show_win()

func _on_crop_selected(index: int) -> void:
	_held_upgrade = -1
	crop_label.text = "Planting: %s" % PlayerProgress.get_crop_data(index).display_name

func _on_upgrade_selected(type: int) -> void:
	_held_upgrade = type
	_refresh_upgrade_label()

func _on_upgrade_stock_changed(type: int) -> void:
	if _held_upgrade == type:
		_refresh_upgrade_label()

func _refresh_upgrade_label() -> void:
	crop_label.text = "Placing: %s (%d in stock)" % [
		Upgrades.display_name(_held_upgrade), Upgrades.stock(_held_upgrade),
	]
