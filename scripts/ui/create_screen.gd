extends Control

signal character_created(player_data: Dictionary)
signal back_to_menu()

const CLASSES = [
	{"name": "王族 (Royal)", "desc": "血盟的領導者，具備號召群雄的王者魅力。", "hp": 85, "mp": 20, "str": 13, "dex": 10, "con": 11, "int": 10, "wis": 11, "cha": 13},
	{"name": "騎士 (Knight)", "desc": "衝鋒陷陣的近戰王者，擁有最強健的體魄與防禦力。", "hp": 110, "mp": 10, "str": 16, "dex": 12, "con": 14, "int": 8, "wis": 9, "cha": 10},
	{"name": "妖精 (Elf)", "desc": "精通精靈魔法與弓箭遠程打擊的森林守護者。", "hp": 75, "mp": 35, "str": 11, "dex": 14, "con": 12, "int": 12, "wis": 12, "cha": 9},
	{"name": "魔法師 (Mage)", "desc": "掌控天地自然奧術力量，具備毀天滅地的高階魔法。", "hp": 60, "mp": 60, "str": 8, "dex": 9, "con": 10, "int": 16, "wis": 16, "cha": 8},
	{"name": "黑暗妖精 (Dark Elf)", "desc": "隱匿於陰影中的刺客，擁有爆發性極限物理傷害。", "hp": 80, "mp": 25, "str": 15, "dex": 13, "con": 12, "int": 11, "wis": 10, "cha": 9}
]

var _selected_class_idx: int = 0
var _name_edit: LineEdit
var _class_desc_label: Label
var _stat_labels: Dictionary = {}

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	var bg = ColorRect.new()
	bg.color = Color("#0b0e14")
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	
	build_ui()

func build_ui() -> void:
	var title = Label.new()
	title.text = "👑 創建全新英雄角色"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.position = Vector2(0, 30)
	title.size = Vector2(800, 40)
	title.add_theme_color_override("font_color", Color("#f1d47a"))
	title.add_theme_font_size_override("font_size", 22)
	add_child(title)
	
	# 左側職業選擇列表
	var class_box = VBoxContainer.new()
	class_box.position = Vector2(100, 90)
	class_box.size = Vector2(200, 300)
	add_child(class_box)
	
	var lbl_pick = Label.new()
	lbl_pick.text = "請選擇職業："
	lbl_pick.add_theme_color_override("font_color", Color("#eee2c7"))
	class_box.add_child(lbl_pick)
	
	for i in range(CLASSES.size()):
		var c = CLASSES[i]
		var btn = Button.new()
		btn.text = c["name"]
		var idx = i
		btn.pressed.connect(func(): _select_class(idx))
		class_box.add_child(btn)
	
	# 右側資訊面板
	var info_panel = PanelContainer.new()
	info_panel.position = Vector2(340, 90)
	info_panel.size = Vector2(360, 300)
	add_child(info_panel)
	
	var vbox = VBoxContainer.new()
	info_panel.add_child(vbox)
	
	_class_desc_label = Label.new()
	_class_desc_label.autowrap_mode = TextServer.AUTOWRAP_WORD
	_class_desc_label.add_theme_color_override("font_color", Color("#94a3b8"))
	vbox.add_child(_class_desc_label)
	
	vbox.add_child(HSeparator.new())
	
	var name_box = HBoxContainer.new()
	var lbl_name = Label.new()
	lbl_name.text = "角色暱稱："
	_name_edit = LineEdit.new()
	_name_edit.text = "亞丁之光"
	_name_edit.custom_minimum_size = Vector2(180, 30)
	name_box.add_child(lbl_name)
	name_box.add_child(_name_edit)
	vbox.add_child(name_box)
	
	vbox.add_child(HSeparator.new())
	
	var stats_title = Label.new()
	stats_title.text = "基礎屬性能力值："
	stats_title.add_theme_color_override("font_color", Color("#f1d47a"))
	vbox.add_child(stats_title)
	
	for stat in ["str", "dex", "con", "int", "wis", "cha"]:
		var stat_row = HBoxContainer.new()
		var s_name = Label.new()
		s_name.text = stat.to_upper() + "："
		s_name.custom_minimum_size = Vector2(60, 20)
		var s_val = Label.new()
		s_val.text = "10"
		stat_row.add_child(s_name)
		stat_row.add_child(s_val)
		vbox.add_child(stat_row)
		_stat_labels[stat] = s_val
	
	# 底部操作按鈕
	var btn_confirm = Button.new()
	btn_confirm.text = "✔ 完成創建，踏入亞丁大陸"
	btn_confirm.position = Vector2(340, 410)
	btn_confirm.size = Vector2(220, 44)
	btn_confirm.pressed.connect(self._on_confirm_pressed)
	add_child(btn_confirm)
	
	var btn_back = Button.new()
	btn_back.text = "返回選單"
	btn_back.position = Vector2(580, 410)
	btn_back.size = Vector2(120, 44)
	btn_back.pressed.connect(func(): emit_signal("back_to_menu"))
	add_child(btn_back)
	
	_select_class(0)

func _select_class(idx: int) -> void:
	_selected_class_idx = idx
	var c = CLASSES[idx]
	_class_desc_label.text = c["desc"]
	for stat in ["str", "dex", "con", "int", "wis", "cha"]:
		if _stat_labels.has(stat):
			_stat_labels[stat].text = str(c[stat])

func _on_confirm_pressed() -> void:
	var c = CLASSES[_selected_class_idx]
	var char_name = _name_edit.text.strip_edges()
	if char_name.is_empty():
		char_name = "冒險者"
	
	var data = {
		"name": char_name,
		"class_name": c["name"],
		"class_index": _selected_class_idx,
		"lv": 1,
		"hp": c["hp"],
		"max_hp": c["hp"],
		"mp": c["mp"],
		"max_mp": c["mp"],
		"exp": 0,
		"max_exp": 100,
		"adena": 1000,
		"map_key": "town_talking_island",
		"x": 332,
		"y": 185,
		"stats": {
			"str": c["str"],
			"dex": c["dex"],
			"con": c["con"],
			"int": c["int"],
			"wis": c["wis"],
			"cha": c["cha"]
		}
	}
	emit_signal("character_created", data)
