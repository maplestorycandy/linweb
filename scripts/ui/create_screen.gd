extends Control

# 放置天堂 Web 版 - 經典創角畫面 (CreateScreen)
# 100% 重現原專案 CreateScreen.cs 之 824c.png 背景、8 職業圖標、性別切換與六維配點

signal character_created(char_data: Dictionary)
signal back_to_menu

const DESIGN_WIDTH = 640.0
const DESIGN_HEIGHT = 480.0
const ASSET_ROOT = "res://assets/ui/web-login"

const GOLD = Color("#f1d47a")
const TEXT_COLOR = Color("#efe1bd")
const DIM_COLOR = Color("#aa9d7c")

const CLASS_KEYS = ["royal", "knight", "elf", "mage", "dark", "illusion", "dragon", "warrior"]
const CLASS_NAMES = ["王族", "騎士", "妖精", "法師", "黑暗妖精", "幻術士", "龍騎士", "戰士"]
const CLASS_DESCRIPTIONS = [
	"統率血盟的王者，具備召喚與號召盟友的專屬魔法，魅力與統御力極高。",
	"身披重甲的鐵血戰士，擁有極高的生命力與近戰防禦，是戰場的前鋒主力。",
	"精通弓箭與四大元素精靈魔法（地、火、風、水），擅長遠程狙殺。",
	"精通強大毀滅性自然魔法與輔助咒文，擁有最高的智力與魔法攻擊力。",
	"暗影中的殺手，善用雙刀與鋼爪進行致命一擊，爆發傷害全職業之冠。",
	"擅長運用心靈奇術與操弄時空的法術師，能在無形中瓦解敵人意志。",
	"承襲遠古巨龍血脈的戰士，能發動龍語連擊與撕裂敵人的屠龍槍技。",
	"狂暴的近戰霸者，擅長雙斧狂暴旋風與生命吸取，越戰越勇。"
]

# 初始屬性 [STR, DEX, CON, WIS, CHA, INT, 額外可配點數]
const CLASS_BASE_STATS = {
	"royal":   {"str": 13, "dex": 10, "con": 10, "wis": 11, "cha": 13, "int": 10, "bonus": 8},
	"knight":  {"str": 16, "dex": 12, "con": 14, "wis": 9,  "cha": 12, "int": 8,  "bonus": 4},
	"elf":     {"str": 11, "dex": 12, "con": 12, "wis": 12, "cha": 9,  "int": 12, "bonus": 7},
	"mage":    {"str": 8,  "dex": 7,  "con": 12, "wis": 12, "cha": 8,  "int": 12, "bonus": 12},
	"dark":    {"str": 12, "dex": 15, "con": 8,  "wis": 10, "cha": 9,  "int": 11, "bonus": 10},
	"illusion":{"str": 11, "dex": 10, "con": 12, "wis": 12, "cha": 8,  "int": 12, "bonus": 10},
	"dragon":  {"str": 13, "dex": 11, "con": 14, "wis": 12, "cha": 8,  "int": 11, "bonus": 6},
	"warrior": {"str": 16, "dex": 13, "con": 16, "wis": 7,  "cha": 9,  "int": 10, "bonus": 4}
}

var _stage: Control
var _class_index: int = 1 # 預設騎士
var _male: bool = true
var _allocations = {"str": 0, "dex": 0, "con": 0, "wis": 0, "cha": 0, "int": 0}

var _class_title: Label
var _class_desc: Label
var _points_label: Label
var _name_input: LineEdit
var _stat_labels: Dictionary = {}
var _preview_rect: TextureRect
var _class_buttons: Array[TextureButton] = []
var _male_btn: TextureButton
var _female_btn: TextureButton

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	var bg_black = ColorRect.new()
	bg_black.color = Color.BLACK
	bg_black.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg_black.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg_black)
	
	_stage = Control.new()
	_stage.size = Vector2(DESIGN_WIDTH, DESIGN_HEIGHT)
	add_child(_stage)
	
	resized.connect(_layout_stage)
	_layout_stage()
	
	_build_ui()
	_refresh()

func _layout_stage() -> void:
	if _stage == null:
		return
	var s: float = min(size.x / DESIGN_WIDTH, size.y / DESIGN_HEIGHT)
	if s <= 0.0:
		s = 1.0
	_stage.scale = Vector2(s, s)
	_stage.position = Vector2(floor((size.x - DESIGN_WIDTH * s) * 0.5), floor((size.y - DESIGN_HEIGHT * s) * 0.5))

func _build_ui() -> void:
	# 824c.png 底圖
	var bg = TextureRect.new()
	bg.texture = load(ASSET_ROOT + "/824c.png")
	bg.size = _stage.size
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bg.stretch_mode = TextureRect.STRETCH_SCALE
	_stage.add_child(bg)
	
	# 左側職業標題與說明
	_class_title = Label.new()
	_class_title.position = Vector2(50, 66)
	_class_title.size = Vector2(200, 24)
	_class_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_class_title.add_theme_color_override("font_color", GOLD)
	_class_title.add_theme_font_size_override("font_size", 16)
	_stage.add_child(_class_title)
	
	_class_desc = Label.new()
	_class_desc.position = Vector2(56, 101)
	_class_desc.size = Vector2(190, 210)
	_class_desc.autowrap_mode = TextServer.AUTOWRAP_ARBITRARY
	_class_desc.add_theme_color_override("font_color", TEXT_COLOR)
	_class_desc.add_theme_font_size_override("font_size", 12)
	_stage.add_child(_class_desc)
	
	# 中間人物立繪預覽
	_preview_rect = TextureRect.new()
	_preview_rect.position = Vector2(245, 60)
	_preview_rect.size = Vector2(160, 260)
	_preview_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_preview_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_stage.add_child(_preview_rect)
	
	# 職業按鈕 (左右兩列，每列 4 個)
	_class_buttons.clear()
	for i in range(CLASS_KEYS.size()):
		var is_left = (i < 4)
		var row = i if is_left else (i - 4)
		var x = 345.5 if is_left else 576.5
		var y = 45.0 + row * 58.0
		
		var btn = TextureButton.new()
		btn.position = Vector2(x, y)
		btn.size = Vector2(34, 34)
		btn.ignore_texture_size = true
		btn.stretch_mode = TextureButton.STRETCH_SCALE
		btn.focus_mode = Control.FOCUS_NONE
		btn.tooltip_text = CLASS_NAMES[i]
		
		var idx = i
		btn.pressed.connect(func():
			_class_index = idx
			_reset_allocations()
			_refresh()
		)
		_stage.add_child(btn)
		_class_buttons.append(btn)
	
	# 性別切換按鈕
	_male_btn = TextureButton.new()
	_male_btn.position = Vector2(427.5, 267.5)
	_male_btn.size = Vector2(40, 20)
	_male_btn.ignore_texture_size = true
	_male_btn.stretch_mode = TextureButton.STRETCH_SCALE
	_male_btn.pressed.connect(func():
		_male = true
		_refresh()
	)
	_stage.add_child(_male_btn)
	
	_female_btn = TextureButton.new()
	_female_btn.position = Vector2(501.5, 267.5)
	_female_btn.size = Vector2(40, 20)
	_female_btn.ignore_texture_size = true
	_female_btn.stretch_mode = TextureButton.STRETCH_SCALE
	_female_btn.pressed.connect(func():
		_male = false
		_refresh()
	)
	_stage.add_child(_female_btn)
	
	# 六維屬性與點數分配
	_points_label = Label.new()
	_points_label.position = Vector2(463, 327)
	_points_label.size = Vector2(24, 24)
	_points_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_points_label.add_theme_color_override("font_color", GOLD)
	_points_label.add_theme_font_size_override("font_size", 14)
	_stage.add_child(_points_label)
	
	var stat_keys = ["str", "dex", "con", "wis", "cha", "int"]
	for i in range(stat_keys.size()):
		var is_left = (i < 3)
		var row = i if is_left else (i - 3)
		var x_val = 400.0 if is_left else 524.0
		var x_btn = 424.0 if is_left else 498.0
		var y = 316.0 + row * 15.0
		var sk = stat_keys[i]
		
		var lbl = Label.new()
		lbl.position = Vector2(x_val, y)
		lbl.size = Vector2(20, 13)
		lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		lbl.add_theme_color_override("font_color", TEXT_COLOR)
		lbl.add_theme_font_size_override("font_size", 11)
		_stage.add_child(lbl)
		_stat_labels[sk] = lbl
		
		# 減少按鈕
		var btn_sub = Button.new()
		btn_sub.text = "-"
		btn_sub.position = Vector2(x_btn, y)
		btn_sub.size = Vector2(12, 13)
		btn_sub.focus_mode = Control.FOCUS_NONE
		btn_sub.add_theme_font_size_override("font_size", 9)
		btn_sub.pressed.connect(func(): _adjust_stat(sk, -1))
		_stage.add_child(btn_sub)
		
		# 增加按鈕
		var btn_add = Button.new()
		btn_add.text = "+"
		btn_add.position = Vector2(x_btn + 13, y)
		btn_add.size = Vector2(12, 13)
		btn_add.focus_mode = Control.FOCUS_NONE
		btn_add.add_theme_font_size_override("font_size", 9)
		btn_add.pressed.connect(func(): _adjust_stat(sk, 1))
		_stage.add_child(btn_add)
	
	# 角色名稱輸入框
	_name_input = LineEdit.new()
	_name_input.position = Vector2(439, 380)
	_name_input.size = Vector2(103, 16)
	_name_input.text = "新冒險者"
	_name_input.max_length = 12
	_name_input.alignment = HORIZONTAL_ALIGNMENT_CENTER
	_name_input.add_theme_color_override("font_color", TEXT_COLOR)
	_name_input.add_theme_font_size_override("font_size", 11)
	_stage.add_child(_name_input)
	
	# 開始冒險按鈕
	var btn_start = Button.new()
	btn_start.text = "開始冒險"
	btn_start.position = Vector2(400, 420)
	btn_start.size = Vector2(100, 30)
	btn_start.focus_mode = Control.FOCUS_NONE
	btn_start.add_theme_color_override("font_color", GOLD)
	btn_start.add_theme_font_size_override("font_size", 13)
	btn_start.pressed.connect(_on_create_confirmed)
	_stage.add_child(btn_start)
	
	# 返回按鈕
	var btn_back = Button.new()
	btn_back.text = "返回選單"
	btn_back.position = Vector2(515, 420)
	btn_back.size = Vector2(85, 30)
	btn_back.focus_mode = Control.FOCUS_NONE
	btn_back.add_theme_color_override("font_color", DIM_COLOR)
	btn_back.add_theme_font_size_override("font_size", 12)
	btn_back.pressed.connect(func(): back_to_menu.emit())
	_stage.add_child(btn_back)

func _reset_allocations() -> void:
	for k in _allocations.keys():
		_allocations[k] = 0

func _adjust_stat(stat: String, delta: int) -> void:
	var c_key = CLASS_KEYS[_class_index]
	var base_info = CLASS_BASE_STATS[c_key]
	var total_allocated = 0
	for k in _allocations.keys():
		total_allocated += _allocations[k]
	
	var rem = base_info["bonus"] - total_allocated
	if delta > 0 and rem <= 0:
		return # 沒點數了
	if delta < 0 and _allocations[stat] <= 0:
		return # 不能低於基礎
	
	_allocations[stat] += delta
	_refresh()

func _refresh() -> void:
	var c_key = CLASS_KEYS[_class_index]
	var c_name = CLASS_NAMES[_class_index]
	var base_info = CLASS_BASE_STATS[c_key]
	
	_class_title.text = c_name
	_class_desc.text = CLASS_DESCRIPTIONS[_class_index]
	
	# 更新職業按鈕圖示
	for i in range(CLASS_KEYS.size()):
		var k = CLASS_KEYS[i]
		var is_selected = (i == _class_index)
		var p_normal = "%s/class-icons/%s_normal.png" % [ASSET_ROOT, k]
		var p_selected = "%s/class-icons/%s_selected.png" % [ASSET_ROOT, k]
		if ResourceLoader.exists(p_normal):
			_class_buttons[i].texture_normal = load(p_selected if is_selected else p_normal)
	
	# 更新性別按鈕
	_male_btn.texture_normal = load(ASSET_ROOT + ("/gender_male_selected.png" if _male else "/gender_male_normal.png"))
	_female_btn.texture_normal = load(ASSET_ROOT + ("/gender_female_selected.png" if not _male else "/gender_female_normal.png"))
	
	# 更新點數
	var total_allocated = 0
	for k in _allocations.keys():
		total_allocated += _allocations[k]
	var rem = base_info["bonus"] - total_allocated
	_points_label.text = str(rem)
	
	# 更新屬性標籤
	for k in _stat_labels.keys():
		var final_val = base_info[k] + _allocations[k]
		_stat_labels[k].text = str(final_val)
	
	# 更新全身預覽立繪
	_update_preview()

func _update_preview() -> void:
	var c_key = CLASS_KEYS[_class_index]
	var idx = 378
	match c_key:
		"royal": idx = 714 if _male else 629
		"knight": idx = 378 if _male else 315
		"elf": idx = 245 if _male else 166
		"mage": idx = 531 if _male else 452
		"dark": idx = 90 if _male else 25
		"illusion": idx = 968 if _male else 1039
		"dragon": idx = 841 if _male else 908
		"warrior": idx = 1992 if _male else 1908
	var p = "res://assets/start/%d.png" % idx
	if ResourceLoader.exists(p):
		_preview_rect.texture = load(p)

func _on_create_confirmed() -> void:
	var c_key = CLASS_KEYS[_class_index]
	var base_info = CLASS_BASE_STATS[c_key]
	var char_name = _name_input.text.strip_edges()
	if char_name.is_empty():
		char_name = "冒險者"
	
	var hp_init = 100
	var mp_init = 30
	match c_key:
		"knight": hp_init = 150; mp_init = 15
		"mage": hp_init = 80; mp_init = 80
		"dark": hp_init = 110; mp_init = 40
		"elf": hp_init = 100; mp_init = 50
	
	var char_data = {
		"name": char_name,
		"class": c_key,
		"gender": "male" if _male else "female",
		"level": 1,
		"hp": hp_init,
		"max_hp": hp_init,
		"mp": mp_init,
		"max_mp": mp_init,
		"ac": 10,
		"str": base_info["str"] + _allocations["str"],
		"dex": base_info["dex"] + _allocations["dex"],
		"con": base_info["con"] + _allocations["con"],
		"wis": base_info["wis"] + _allocations["wis"],
		"cha": base_info["cha"] + _allocations["cha"],
		"int": base_info["int"] + _allocations["int"],
		"alignment": 32767,
		"adena": 1000,
		"exp": 0
	}
	character_created.emit(char_data)
