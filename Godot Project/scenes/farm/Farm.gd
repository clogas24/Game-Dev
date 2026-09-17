extends Node2D

@onready var player: CharacterBody2D = $Player
@onready var money_label: Label = $UI/MoneyLabel
@onready var crop_label: Label = $UI/CropLabel
@onready var help_button: Button = $UI/HelpButton
@onready var help_menu: Node = $HelpMenu
@onready var plant_menu_button: Button = $UI/PlantMenuButton
@onready var plant_menu: Node = $PlantMenu

func _ready() -> void:
	Economy.money_changed.connect(_on_money_changed)
	_on_money_changed(Economy.money)
	player.crop_selected.connect(_on_crop_selected)
	_on_crop_selected(player.selected_crop_index)
	help_button.pressed.connect(help_menu.open)
	plant_menu_button.pressed.connect(plant_menu.open)

func _on_money_changed(new_amount: int) -> void:
	money_label.text = "Money: %d" % new_amount

func _on_crop_selected(index: int) -> void:
	crop_label.text = "Planting: %s" % PlayerProgress.get_crop_data(index).display_name
