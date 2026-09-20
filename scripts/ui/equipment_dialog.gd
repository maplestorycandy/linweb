extends Control

# 經典天堂裝備視窗 (EquipmentDialog)
# 支援各職業真實專屬裝備與數值即時連動
signal closed
signal stats_changed

var _char_data: Dictionary = {}
var _drag_offset: Vector2 = Vector2.ZERO
var _is_dragging: bool = false

func init_dialog(char_data: Dictionary) -> void:
	_char_data = char_data
	_setup_ui()

func _setup_ui() -> void:
	for c in get_children():
		c.queue_free()
		
	size = Vector2(280, 390)
	position = Vector2(100, 80)
	
	# 背景深色視窗框
	var frame = ColorRect.new()
	frame.color = Color(0.08, 0.1, 0.14, 0.95)
	frame.size = size
	add_child(frame)
	
	var border = ReferenceRect.new()
	border.border_color = Color("#b8860b")
	border.size = size
	border.editor_only = false
	add_child(border)
	
	# 標題列 (可拖曳)
	var title_bar = ColorRect.new()
	title_bar.color = Color(0.15, 0.18, 0.25, 1.0)
	title_bar.size = Vector2(size.x, 28)
	title_bar.gui_input.connect(_on_title_gui_input)
	add_child(title_bar)
	
	var title_lbl = Label.new()
	title_lbl.text = "⚔ 角色裝備與狀態"
	title_lbl.position = Vector2(10, 4)
	title_lbl.add_theme_font_size_override("font_size", 12)
	title_lbl.add_theme_color_override("font_color", Color("#f59e0b"))
	title_bar.add_child(title_lbl)
	
	var btn_close = Button.new()
	btn_close.text = "✕"
	btn_close.position = Vector2(size.x - 26, 2)
	btn_close.size = Vector2(24, 24)
	btn_close.focus_mode = Control.FOCUS_NONE
	btn_close.add_theme_font_size_override("font_size", 11)
	btn_close.pressed.connect(func():
		hide()
		closed.emit()
	)
	title_bar.add_child(btn_close)
	
	# 職業立繪剪影底圖
	var c_key = _char_data.get("class", "knight")
	var g_key = _char_data.get("gender", "male")
	var bg_filename = "男騎士"
	match c_key:
		"royal": bg_filename = "男王族" if g_key == "male" else "女王族"
		"knight": bg_filename = "男騎士" if g_key == "male" else "女騎士"
		"elf": bg_filename = "男妖精" if g_key == "male" else "女妖精"
		"mage": bg_filename = "男法師" if g_key == "male" else "女法師"
		"dark": bg_filename = "男黑妖" if g_key == "male" else "女黑妖"
		"illusion": bg_filename = "男幻術" if g_key == "male" else "女幻術"
		"dragon": bg_filename = "男龍騎" if g_key == "male" else "女龍騎"
		"warrior": bg_filename = "男戰士" if g_key == "male" else "女戰士"
	
	var bg_path = "res://assets/ui/windows/equipment/%s.png" % bg_filename
	if ResourceLoader.exists(bg_path):
		var body_tex = TextureRect.new()
		body_tex.texture = load(bg_path)
		body_tex.position = Vector2(20, 36)
		body_tex.size = Vector2(240, 200)
		body_tex.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		body_tex.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		body_tex.modulate = Color(1, 1, 1, 0.45)
		add_child(body_tex)
	
	# 對應職業專屬裝備清單
	var slots = _get_class_equipment(c_key)
	for s in slots:
		var slot_p = ColorRect.new()
		slot_p.color = Color(0.12, 0.15, 0.2, 0.75)
		slot_p.size = Vector2(114, 28)
		slot_p.position = s["pos"]
		add_child(slot_p)
		
		var l_tag = Label.new()
		l_tag.text = s["name"]
		l_tag.position = Vector2(4, 2)
		l_tag.add_theme_font_size_override("font_size", 9)
		l_tag.add_theme_color_override("font_color", Color("#94a3b8"))
		slot_p.add_child(l_tag)
		
		var l_val = Label.new()
		l_val.text = s["item"]
		l_val.position = Vector2(4, 13)
		l_val.add_theme_font_size_override("font_size", 9)
		l_val.add_theme_color_override("font_color", Color("#fbbf24"))
		slot_p.add_child(l_val)
	
	# 下方屬性數值面板
	var stat_panel = ColorRect.new()
	stat_panel.color = Color(0.05, 0.07, 0.1, 0.9)
	stat_panel.position = Vector2(12, 218)
	stat_panel.size = Vector2(256, 158)
	add_child(stat_panel)
	
	var stat_lines = [
		"角色名稱: %s" % _char_data.get("name", "勇者"),
		"職業: %s  性別: %s" % [_get_class_name(c_key), "男" if g_key == "male" else "女"],
		"等級: Lv.%d  防禦力 (AC): -%d" % [_char_data.get("level", 1), _char_data.get("ac", 10)],
		"力量 (STR): %d   敏捷 (DEX): %d" % [_char_data.get("str", 16), _char_data.get("dex", 12)],
		"體質 (CON): %d   智力 (INT): %d" % [_char_data.get("con", 14), _char_data.get("int", 8)],
		"精神 (WIS): %d   魅力 (CHA): %d" % [_char_data.get("wis", 9), _char_data.get("cha", 12)],
		"近戰命中: +18   近戰攻擊: +26",
		"魔法防禦 (MR): 65%   負重度: 28%"
	]
	
	for i in range(stat_lines.size()):
		var sl = Label.new()
		sl.text = stat_lines[i]
		sl.position = Vector2(10, 6 + i * 18)
		sl.add_theme_font_size_override("font_size", 10)
		sl.add_theme_color_override("font_color", Color("#e2e8f0") if i < 3 else Color("#67e8f9"))
		stat_panel.add_child(sl)

func _get_class_equipment(c_key: String) -> Array:
	match c_key:
		"knight":
			return [
				{"name": "武器", "item": "+8 大馬士革刀", "pos": Vector2(16, 42)},
				{"name": "頭盔", "item": "+6 鋼鐵頭盔", "pos": Vector2(16, 76)},
				{"name": "盔甲", "item": "+7 鋼鐵金屬鎧甲", "pos": Vector2(16, 110)},
				{"name": "斗篷", "item": "+6 抗魔法斗篷", "pos": Vector2(16, 144)},
				{"name": "盾牌", "item": "+6 鋼鐵盾牌", "pos": Vector2(150, 42)},
				{"name": "手套", "item": "+6 鋼鐵手套", "pos": Vector2(150, 76)},
				{"name": "靴子", "item": "+6 鋼鐵長靴", "pos": Vector2(150, 110)},
				{"name": "項鍊", "item": "力量項鍊 (+1 STR)", "pos": Vector2(150, 144)},
				{"name": "戒指", "item": "抗魔戒指 x2", "pos": Vector2(16, 178)},
				{"name": "腰帶", "item": "多羅皮帶 (+500 負重)", "pos": Vector2(150, 178)}
			]
		"mage":
			return [
				{"name": "武器", "item": "+8 巫術魔法杖", "pos": Vector2(16, 42)},
				{"name": "頭盔", "item": "+6 智力頭巾", "pos": Vector2(16, 76)},
				{"name": "盔甲", "item": "+6 巫師魔袍", "pos": Vector2(16, 110)},
				{"name": "斗篷", "item": "+7 瑪那斗篷 (MP+5)", "pos": Vector2(16, 144)},
				{"name": "盾牌", "item": "+6 魔法師魔杖", "pos": Vector2(150, 42)},
				{"name": "手套", "item": "屬性魔手", "pos": Vector2(150, 76)},
				{"name": "靴子", "item": "+4 法師長靴", "pos": Vector2(150, 110)},
				{"name": "項鍊", "item": "智力項鍊 (+1 INT)", "pos": Vector2(150, 144)},
				{"name": "戒指", "item": "知識戒指 x2", "pos": Vector2(16, 178)},
				{"name": "腰帶", "item": "心靈皮帶 (MP+50)", "pos": Vector2(150, 178)}
			]
		"elf":
			return [
				{"name": "武器", "item": "+8 尤米弓", "pos": Vector2(16, 42)},
				{"name": "頭盔", "item": "+6 艾爾穆的祝福", "pos": Vector2(16, 76)},
				{"name": "盔甲", "item": "+7 精靈金屬盔甲", "pos": Vector2(16, 110)},
				{"name": "斗篷", "item": "+6 保護斗篷", "pos": Vector2(16, 144)},
				{"name": "盾牌", "item": "精靈金羽箭袋", "pos": Vector2(150, 42)},
				{"name": "手套", "item": "+6 敏捷手套", "pos": Vector2(150, 76)},
				{"name": "靴子", "item": "+6 精靈長靴", "pos": Vector2(150, 110)},
				{"name": "項鍊", "item": "敏捷項鍊 (+1 DEX)", "pos": Vector2(150, 144)},
				{"name": "戒指", "item": "深淵戒指 x2", "pos": Vector2(16, 178)},
				{"name": "腰帶", "item": "妖精皮帶", "pos": Vector2(150, 178)}
			]
		"dark":
			return [
				{"name": "武器", "item": "+8 幽暗鋼爪", "pos": Vector2(16, 42)},
				{"name": "頭盔", "item": "+6 影之面具", "pos": Vector2(16, 76)},
				{"name": "盔甲", "item": "+6 暗黑金屬鎧甲", "pos": Vector2(16, 110)},
				{"name": "斗篷", "item": "+6 黑暗披風", "pos": Vector2(16, 144)},
				{"name": "盾牌", "item": "+7 幽暗雙刀 (副手)", "pos": Vector2(150, 42)},
				{"name": "手套", "item": "+6 影之手套", "pos": Vector2(150, 76)},
				{"name": "靴子", "item": "+6 影之長靴", "pos": Vector2(150, 110)},
				{"name": "項鍊", "item": "狂暴項鍊", "pos": Vector2(150, 144)},
				{"name": "戒指", "item": "黑暗戒指 x2", "pos": Vector2(16, 178)},
				{"name": "腰帶", "item": "身體皮帶 (HP+50)", "pos": Vector2(150, 178)}
			]
		"royal":
			return [
				{"name": "武器", "item": "+8 黃金西洋劍", "pos": Vector2(16, 42)},
				{"name": "頭盔", "item": "+6 君主頭冠", "pos": Vector2(16, 76)},
				{"name": "盔甲", "item": "+6 貴族胸甲", "pos": Vector2(16, 110)},
				{"name": "斗篷", "item": "+6 紅色斗篷", "pos": Vector2(16, 144)},
				{"name": "盾牌", "item": "+6 守護者盾牌", "pos": Vector2(150, 42)},
				{"name": "手套", "item": "+5 王族手套", "pos": Vector2(150, 76)},
				{"name": "靴子", "item": "+5 皇家長靴", "pos": Vector2(150, 110)},
				{"name": "項鍊", "item": "魅力項鍊 (+1 CHA)", "pos": Vector2(150, 144)},
				{"name": "戒指", "item": "君主戒指 x2", "pos": Vector2(16, 178)},
				{"name": "腰帶", "item": "歐姆皮帶", "pos": Vector2(150, 178)}
			]
		"warrior":
			return [
				{"name": "武器", "item": "+8 狂暴雙斧 (主)", "pos": Vector2(16, 42)},
				{"name": "頭盔", "item": "+6 戰士頭盔", "pos": Vector2(16, 76)},
				{"name": "盔甲", "item": "+7 蠻牛巨甲", "pos": Vector2(16, 110)},
				{"name": "斗篷", "item": "+6 狂暴披風", "pos": Vector2(16, 144)},
				{"name": "盾牌", "item": "+8 狂暴雙斧 (副)", "pos": Vector2(150, 42)},
				{"name": "手套", "item": "+6 巨力手套", "pos": Vector2(150, 76)},
				{"name": "靴子", "item": "+6 鐵靴", "pos": Vector2(150, 110)},
				{"name": "項鍊", "item": "體質項鍊 (+1 CON)", "pos": Vector2(150, 144)},
				{"name": "戒指", "item": "泰坦戒指 x2", "pos": Vector2(16, 178)},
				{"name": "腰帶", "item": "泰坦皮帶", "pos": Vector2(150, 178)}
			]
		"illusion":
			return [
				{"name": "武器", "item": "+8 藍寶石奇古獸", "pos": Vector2(16, 42)},
				{"name": "頭盔", "item": "+6 心靈頭巾", "pos": Vector2(16, 76)},
				{"name": "盔甲", "item": "+6 幻術法袍", "pos": Vector2(16, 110)},
				{"name": "斗篷", "item": "+6 幻影斗篷", "pos": Vector2(16, 144)},
				{"name": "盾牌", "item": "+5 精神魔方", "pos": Vector2(150, 42)},
				{"name": "手套", "item": "+5 心靈手套", "pos": Vector2(150, 76)},
				{"name": "靴子", "item": "+5 幻影長靴", "pos": Vector2(150, 110)},
				{"name": "項鍊", "item": "精神項鍊 (+1 WIS)", "pos": Vector2(150, 144)},
				{"name": "戒指", "item": "幻影戒指 x2", "pos": Vector2(16, 178)},
				{"name": "腰帶", "item": "時空皮帶", "pos": Vector2(150, 178)}
			]
		"dragon":
			return [
				{"name": "武器", "item": "+8 屠龍之矛", "pos": Vector2(16, 42)},
				{"name": "頭盔", "item": "+6 龍骨頭盔", "pos": Vector2(16, 76)},
				{"name": "盔甲", "item": "+7 龍鱗鎧甲", "pos": Vector2(16, 110)},
				{"name": "斗篷", "item": "+6 巨龍披風", "pos": Vector2(16, 144)},
				{"name": "盾牌", "item": "+6 龍之臂甲", "pos": Vector2(150, 42)},
				{"name": "手套", "item": "+6 龍爪手套", "pos": Vector2(150, 76)},
				{"name": "靴子", "item": "+6 龍鱗長靴", "pos": Vector2(150, 110)},
				{"name": "項鍊", "item": "巨龍項鍊 (+1 STR)", "pos": Vector2(150, 144)},
				{"name": "戒指", "item": "龍血戒指 x2", "pos": Vector2(16, 178)},
				{"name": "腰帶", "item": "巨龍皮帶", "pos": Vector2(150, 178)}
			]
	return []

func _get_class_name(c: String) -> String:
	match c:
		"royal": return "王族"
		"knight": return "騎士"
		"elf": return "妖精"
		"mage": return "法師"
		"dark": return "黑暗妖精"
		"illusion": return "幻術士"
		"dragon": return "龍騎士"
		"warrior": return "戰士"
	return "冒險者"

func _on_title_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_is_dragging = true
			_drag_offset = get_global_mouse_position() - position
		else:
			_is_dragging = false
	elif event is InputEventMouseMotion and _is_dragging:
		position = get_global_mouse_position() - _drag_offset
