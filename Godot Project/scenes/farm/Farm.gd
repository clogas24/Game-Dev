extends Node2D

const CROP_NAMES: Array[String] = ["Kelp", "Coral"]

@onready var player: CharacterBody2D = $Player
@onready var money_label: Label = $UI/MoneyLabel
@onready var crop_label: Label = $UI/CropLabel

func _ready() -> void:
	Economy.money_changed.connect(_on_money_changed)
	_on_money_changed(Economy.money)
	player.crop_selected.connect(_on_crop_selected)
	_on_crop_selected(player.selected_crop_index)

func _on_money_changed(new_amount: int) -> void:
	money_label.text = "Money: %d" % new_amount

func _on_crop_selected(index: int) -> void:
	crop_label.text = "Planting: %s (press 1/2)" % CROP_NAMES[index]
