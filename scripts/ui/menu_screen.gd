extends Control

# 放置天堂 Web 版 - 經典登入與角色選單 (MenuScreen)
# 100% 重現原專案 MenuScreen.cs 之 640x480 經典天堂火炬與角色卡片

signal start_new_game(slot: int)
signal load_game(slot: int)

const DESIGN_WIDTH = 640.0
const DESIGN_HEIGHT = 480.0
const ASSET_ROOT = "res://assets/ui/web-login"

const GOLD = Color("#f1d47a")
const TEXT_COLOR = Color("#eee2c7")
const DIM_COLOR = Color("#aa9d7c")

var _stage: Control
var _title_layer: Control
var _load_layer: Control

var _login_animation: TextureRect
var _login_frames: Array[Texture2D] = []
var _animation_elapsed: float = 0.0
var _animation_frame: int = 0

var _page: int = 0
var _selected_slot: int = 1
var _slots_data: Array = []

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	# 黑底背景
	var bg_black = ColorRect.new()
	bg_black.color = Color.BLACK
	bg_black.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg_black.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg_black)
	
	# 640x480 舞台
	_stage = Control.new()
	_stage.size = Vector2(DESIGN_WIDTH, DESIGN_HEIGHT)
	add_child(_stage)
	
	resized.connect(_layout_stage)
	_layout_stage()
	
	_load_slots()
	_build_title()
	_build_load()
	
	_show_title()

func _layout_stage() -> void:
	if _stage == null:
		return
	var s: float = min(size.x / DESIGN_WIDTH, size.y / DESIGN_HEIGHT)
	if s <= 0.0:
		s = 1.0
	_stage.scale = Vector2(s, s)
	_stage.position = Vector2(floor((size.x - DESIGN_WIDTH * s) * 0.5), floor((size.y - DESIGN_HEIGHT * s) * 0.5))

func _process(delta: float) -> void:
	if _title_layer != null and _title_layer.visible and _login_frames.size() > 0:
		_animation_elapsed += delta
		if _animation_elapsed >= 0.09:
			_animation_elapsed -= 0.09
			_animation_frame = (_animation_frame + 1) % _login_frames.size()
			if _login_animation != null and _animation_frame < _login_frames.size():
				_login_animation.texture = _login_frames[_animation_frame]

# ----------------- 階段 1：標題火炬選單 -----------------
func _build_title() -> void:
	_title_layer = Control.new()
	_title_layer.size = Vector2(DESIGN_WIDTH, DESIGN_HEIGHT)
	_stage.add_child(_title_layer)
	
	# 310.png 滿版底圖
	var bg = TextureRect.new()
	bg.texture = load(ASSET_ROOT + "/310.png")
	bg.size = _title_layer.size
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bg.stretch_mode = TextureRect.STRETCH_SCALE
	_title_layer.add_child(bg)
	
	# 28 幀火炬燃燒動畫 (273.png ~ 300.png)
	_login_frames.clear()
	for i in range(28):
		var p = "%s/%d.png" % [ASSET_ROOT, 273 + i]
		if ResourceLoader.exists(p):
			_login_frames.append(load(p))
	
	_login_animation = TextureRect.new()
	_login_animation.position = Vector2(0, 306)
	_login_animation.size = Vector2(501, 174)
	_login_animation.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_login_animation.stretch_mode = TextureRect.STRETCH_SCALE
	if _login_frames.size() > 0:
		_login_animation.texture = _login_frames[0]
	_title_layer.add_child(_login_animation)
	
	# 金色標題
	var title = Label.new()
	title.text = "天堂單機版"
	title.position = Vector2(30, 58)
	title.size = Vector2(182, 34)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_color_override("font_color", GOLD)
	title.add_theme_color_override("font_shadow_color", Color.BLACK)
	title.add_theme_constant_override("shadow_offset_x", 2)
	title.add_theme_constant_override("shadow_offset_y", 2)
	title.add_theme_font_size_override("font_size", 24)
	_title_layer.add_child(title)
	
	# 宣告文字
	var desc = Label.new()
	desc.text = "本遊戲為非官方免費同人作品，絕無營利意圖；遊戲內圖片與音樂版權歸原權利方所有。"
	desc.position = Vector2(34, 230)
	desc.size = Vector2(174, 52)
	desc.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	desc.autowrap_mode = TextServer.AUTOWRAP_ARBITRARY
	desc.add_theme_color_override("font_color", TEXT_COLOR)
	desc.add_theme_font_size_override("font_size", 10)
	_title_layer.add_child(desc)
	
	# 開始遊戲按鈕 (6996.png)
	var start_tex = load(ASSET_ROOT + "/6996.png")
	var btn_start = TextureButton.new()
	btn_start.position = Vector2(313, 228)
	btn_start.size = Vector2(180, 34)
	btn_start.texture_normal = start_tex
	btn_start.texture_hover = start_tex
	btn_start.texture_pressed = start_tex
	btn_start.ignore_texture_size = true
	btn_start.stretch_mode = TextureButton.STRETCH_SCALE
	btn_start.focus_mode = Control.FOCUS_NONE
	btn_start.tooltip_text = "開始遊戲"
	btn_start.pressed.connect(_show_load)
	_title_layer.add_child(btn_start)

# ----------------- 階段 2：角色槽選擇 -----------------
func _build_load() -> void:
	_load_layer = Control.new()
	_load_layer.size = Vector2(DESIGN_WIDTH, DESIGN_HEIGHT)
	_load_layer.visible = false
	_stage.add_child(_load_layer)

func _show_title() -> void:
	_load_layer.visible = false
	_title_layer.visible = true
	_animation_elapsed = 0.0

func _show_load() -> void:
	_title_layer.visible = false
	_load_layer.visible = true
	_load_slots()
	_rebuild_load()

func _load_slots() -> void:
	_slots_data.clear()
	for i in range(1, 17):
		var p = "user://character_slot_%d.json" % i
		if FileAccess.file_exists(p):
			var f = FileAccess.open(p, FileAccess.READ)
			if f != null:
				var parsed = JSON.parse_string(f.get_as_text())
				if typeof(parsed) == TYPE_DICTIONARY:
					parsed["slot"] = i
					_slots_data.append(parsed)
					continue
		_slots_data.append({"slot": i, "empty": true, "name": "", "class": "", "level": 1})

func _rebuild_load() -> void:
	for c in _load_layer.get_children():
		c.queue_free()
	
	# 底圖 load.png
	var bg = TextureRect.new()
	bg.texture = load(ASSET_ROOT + "/load.png")
	bg.size = _load_layer.size
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bg.stretch_mode = TextureRect.STRETCH_SCALE
	_load_layer.add_child(bg)
	
	# 4 個分頁卡片
	var page_start = _page * 4
	for i in range(4):
		var slot_idx = page_start + i
		if slot_idx < _slots_data.size():
			_add_slot_card(_slots_data[slot_idx], i)
	
	# 上層邊框 load1.png
	var border = TextureRect.new()
	border.texture = load(ASSET_ROOT + "/load1.png")
	border.size = _load_layer.size
	border.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	border.stretch_mode = TextureRect.STRETCH_SCALE
	border.z_index = 10
	border.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_load_layer.add_child(border)
	
	# 分頁切換按鈕
	var page_centers = [276.5, 307.5, 338.5, 369.5]
	var page_hint = Label.new()
	page_hint.text = "第 %d 頁" % (_page + 1)
	page_hint.position = Vector2(250, 305)
	page_hint.size = Vector2(146, 15)
	page_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	page_hint.add_theme_color_override("font_color", GOLD)
	page_hint.add_theme_font_size_override("font_size", 11)
	_load_layer.add_child(page_hint)
	
	for p in range(4):
		var btn = Button.new()
		btn.text = str(p + 1)
		btn.position = Vector2(page_centers[p] - 12, 285)
		btn.size = Vector2(24, 20)
		btn.focus_mode = Control.FOCUS_NONE
		btn.add_theme_font_size_override("font_size", 10)
		var target_page = p
		btn.pressed.connect(func():
			_page = target_page
			_rebuild_load()
		)
		_load_layer.add_child(btn)
	
	# 呈現當前選中角色的數值與操作按鈕
	if _selected_slot - 1 < _slots_data.size():
		var sel_info = _slots_data[_selected_slot - 1]
		_add_selected_info(sel_info)
		_add_actions(sel_info)

func _add_slot_card(info: Dictionary, column: int) -> void:
	var slot_num = info.get("slot", 1)
	var is_empty = info.get("empty", false) or not info.has("name") or str(info.get("name", "")).is_empty()
	var is_selected = (slot_num == _selected_slot)
	
	var card = Button.new()
	card.position = Vector2(11 + column * 156, 25)
	card.size = Vector2(149, 231)
	card.focus_mode = Control.FOCUS_NONE
	
	var sb = StyleBoxFlat.new()
	if is_selected:
		sb.bg_color = Color(1, 0.84, 0, 0.18)
		sb.border_color = GOLD
		sb.set_border_width_all(2)
	else:
		sb.bg_color = Color(0, 0, 0, 0.4)
		sb.border_color = Color(0.3, 0.3, 0.3, 0.5)
		sb.set_border_width_all(1)
	card.add_theme_stylebox_override("normal", sb)
	card.add_theme_stylebox_override("hover", sb)
	card.add_theme_stylebox_override("pressed", sb)
	
	card.pressed.connect(func():
		_selected_slot = slot_num
		_rebuild_load()
	)
	_load_layer.add_child(card)
	
	# 卡片中的角色動態立繪 / 空槽預覽
	if is_empty:
		var empty_lbl = Label.new()
		empty_lbl.text = "【角色槽 %d】\n點擊創新角色" % slot_num
		empty_lbl.position = Vector2(0, 100)
		empty_lbl.size = Vector2(149, 40)
		empty_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		empty_lbl.add_theme_color_override("font_color", DIM_COLOR)
		empty_lbl.add_theme_font_size_override("font_size", 12)
		card.add_child(empty_lbl)
	else:
		# 載入立繪
		var spr_tex = _get_start_seq_texture(info.get("class", "knight"), info.get("gender", "male"))
		if spr_tex != null:
			var portrait = TextureRect.new()
			portrait.texture = spr_tex
			portrait.position = Vector2(10, 10)
			portrait.size = Vector2(129, 190)
			portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			card.add_child(portrait)
		
		var name_lbl = Label.new()
		name_lbl.text = "%s Lv.%d" % [info.get("name", "冒險者"), info.get("level", 1)]
		name_lbl.position = Vector2(0, 205)
		name_lbl.size = Vector2(149, 20)
		name_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		name_lbl.add_theme_color_override("font_color", GOLD if is_selected else TEXT_COLOR)
		name_lbl.add_theme_font_size_override("font_size", 12)
		card.add_child(name_lbl)

func _get_start_seq_texture(class_name: String, gender: String) -> Texture2D:
	# 依職業獲取 assets/start/ 裡的第一幀或立繪
	var idx = 378 # 預設騎士
	match class_name:
		"royal": idx = 714 if gender == "male" else 629
		"knight": idx = 378 if gender == "male" else 315
		"elf": idx = 245 if gender == "male" else 166
		"mage": idx = 531 if gender == "male" else 452
		"dark": idx = 90 if gender == "male" else 25
	var p = "res://assets/start/%d.png" % idx
	if ResourceLoader.exists(p):
		return load(p)
	return null

func _add_selected_info(info: Dictionary) -> void:
	var is_empty = info.get("empty", false) or not info.has("name") or str(info.get("name", "")).is_empty()
	if is_empty:
		return
	
	var left_tops = [310.0, 330.0, 355.0, 375.0, 401.0, 421.0, 441.0]
	var right_tops = [314.0, 341.0, 361.0, 381.0, 401.0, 421.0, 441.0]
	
	# 左側資訊：名稱、血盟、職業、正義、HP、MP、AC
	var class_zh = _get_class_display_name(info.get("class", "knight"))
	var left_vals = [
		info.get("name", "勇者"),
		info.get("clan", "無"),
		class_zh,
		"正義 %d" % info.get("alignment", 32767),
		"%d / %d" % [info.get("hp", 100), info.get("max_hp", 100)],
		"%d / %d" % [info.get("mp", 50), info.get("max_mp", 50)],
		str(info.get("ac", 10))
	]
	for i in range(left_vals.size()):
		_add_info_label(left_vals[i], 188.0, left_tops[i], 83.0)
	
	# 右側資訊：等級、STR、DEX、CON、WIS、CHA、INT
	var right_vals = [
		str(info.get("level", 1)),
		str(info.get("str", 16)),
		str(info.get("dex", 12)),
		str(info.get("con", 14)),
		str(info.get("wis", 9)),
		str(info.get("cha", 12)),
		str(info.get("int", 8))
	]
	for i in range(right_vals.size()):
		_add_info_label(right_vals[i], 412.0, right_tops[i], 86.0)

func _add_info_label(text: String, x: float, y: float, w: float) -> void:
	var lbl = Label.new()
	lbl.text = text
	lbl.position = Vector2(x, y)
	lbl.size = Vector2(w, 15)
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	lbl.add_theme_color_override("font_color", TEXT_COLOR)
	lbl.add_theme_font_size_override("font_size", 11)
	_load_layer.add_child(lbl)

func _add_actions(info: Dictionary) -> void:
	var is_empty = info.get("empty", false) or not info.has("name") or str(info.get("name", "")).is_empty()
	var slot_num = info.get("slot", 1)
	
	var y = 310.0
	var btn_main = Button.new()
	btn_main.text = "創新角色" if is_empty else "進入遊戲"
	btn_main.position = Vector2(518, y)
	btn_main.size = Vector2(100, 26)
	btn_main.focus_mode = Control.FOCUS_NONE
	btn_main.add_theme_color_override("font_color", GOLD)
	btn_main.add_theme_font_size_override("font_size", 12)
	btn_main.pressed.connect(func():
		if is_empty:
			start_new_game.emit(slot_num)
		else:
			load_game.emit(slot_num)
	)
	_load_layer.add_child(btn_main)
	
	if not is_empty:
		y += 32.0
		var btn_del = Button.new()
		btn_del.text = "刪除角色"
		btn_del.position = Vector2(518, y)
		btn_del.size = Vector2(100, 24)
		btn_del.focus_mode = Control.FOCUS_NONE
		btn_del.add_theme_color_override("font_color", Color(1, 0.4, 0.4))
		btn_del.add_theme_font_size_override("font_size", 11)
		btn_del.pressed.connect(func():
			var p = "user://character_slot_%d.json" % slot_num
			if FileAccess.file_exists(p):
				DirAccess.remove_absolute(p)
			_load_slots()
			_rebuild_load()
		)
		_load_layer.add_child(btn_del)
	
	# 返回標題
	var btn_back = Button.new()
	btn_back.text = "返回標題"
	btn_back.position = Vector2(518, 435)
	btn_back.size = Vector2(100, 24)
	btn_back.focus_mode = Control.FOCUS_NONE
	btn_back.add_theme_color_override("font_color", DIM_COLOR)
	btn_back.add_theme_font_size_override("font_size", 11)
	btn_back.pressed.connect(_show_title)
	_load_layer.add_child(btn_back)

func _get_class_display_name(c: String) -> String:
	match c:
		"royal": return "王族"
		"knight": return "騎士"
		"elf": return "妖精"
		"mage": return "法師"
		"dark": return "黑暗妖精"
		"illusion": return "幻術士"
		"dragon": return "龍騎士"
		"warrior": return "戰士"
	return "騎士"
