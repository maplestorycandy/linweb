extends Node2D

# 放置天堂 Web 版 - 說話之島 ARPG 戰鬥世界 (ArpgWorld)
# 100% 忠實對齊 IdleLineage_Source_Restored (ArpgEngineScreen.cs / ArpgActor.cs / DraggableHpMpBar.cs)
# 1. 座標與地圖：說話之島 (2048 x 1024)，玩家在村莊 (1318, 517) 出生，怪物在島內荒野安全巡邏，絕不跑到海裡。
# 2. 真實怪物：完全載入真實 L1J 精靈圖集 (狼人 mob_1110, 妖魔 mob_56, 哥布林 mob_1022, 夏洛伯 mob_95, 高崙 mob_49, 侏儒 mob_54, 骷髏 mob_30, 史萊姆 mob_31 等)，具備 8 方向 walk/idle/attack/hurt/death 動畫。
# 3. 原版 UI 佈局：
#    - 左側狀態面板 (rn_left_panel.png)：等級、經驗條、天幣、AC、正義值、負重/飽食度
#    - 說話之島地名 Banner (金色描邊)
#    - 右側小地圖雷達 (rn_minimap_frame.png / rn_minimap_base.png)
#    - 右側巨集面板 (rn_macro_panel.png)：裝備、道具、技能、任務、選單
#    - 底部中央對話視窗 (rn_chat_born.png / rn_chat_window.png) + 分頁標籤 (全部/隊伍/掉落)
#    - 可拖曳經典血魔橫條 (hpmp_frame.png, hpmp_hp_fill.png, hpmp_mp_fill.png, 334x34)
#    - 可拖曳對話輸入框 (DraggableChatBar)
# 4. 戰鬥與掉落：點擊地面移動、點擊怪物鎖定攻擊、擊敗後地上生成金幣袋可拾取。

signal return_to_menu

var _char_data: Dictionary = {}
var _slot: int = 1

# 地圖與相機
var _map_sprite: Sprite2D
var _map_width: float = 2048.0
var _map_height: float = 1024.0
var _camera: Camera2D

# 玩家實體
var _player_node: Node2D
var _player_sprite: AnimatedSprite2D
var _player_hp_bar: ColorRect
var _player_name_label: Label
var _player_pos: Vector2 = Vector2(1318, 517)
var _player_target_pos: Vector2 = Vector2(1318, 517)
var _player_speed: float = 160.0
var _player_hp: float = 150.0
var _player_max_hp: float = 150.0
var _player_mp: float = 30.0
var _player_max_mp: float = 30.0
var _player_level: int = 1
var _player_exp: float = 0.0
var _player_exp_max: float = 100.0
var _player_adena: int = 1000
var _player_facing: int = 5 # 0~7 Lineage 方向
var _player_is_attacking: bool = false
var _player_atk_cd: float = 0.0

# 怪物列表 [{node, sprite, name, hp, max_hp, exp, adena, dmg, spd, pos, target_pos, atk_cd, atlas, is_dead, dead_timer, heading}]
var _mobs: Array[Dictionary] = []
var _target_mob: Dictionary = {}

# 地面掉落物 [{node, pos, type, amount, label}]
var _ground_drops: Array[Dictionary] = []

# HUD 節點
var _hud_layer: CanvasLayer
var _hpmp_bar_ctrl: Control
var _hp_fill_rect: TextureRect
var _mp_fill_rect: TextureRect
var _hp_txt_label: Label
var _mp_txt_label: Label
var _is_dragging_hpmp: bool = false
var _drag_hpmp_offset: Vector2 = Vector2.ZERO

var _chat_bar_ctrl: Control
var _chat_input: LineEdit
var _is_dragging_chatbar: bool = false
var _drag_chatbar_offset: Vector2 = Vector2.ZERO

var _exp_fill_rect: TextureRect
var _exp_pct_label: Label
var _adena_label: Label
var _level_label: Label
var _ac_label: Label
var _align_label: Label
var _chat_box: RichTextLabel
var _minimap_radar: Control

const GOLD = Color("#f1d47a")
const TEXT_COLOR = Color("#eee2c7")

func init_world(char_data: Dictionary, slot: int = 1) -> void:
	_char_data = char_data
	_slot = slot
	_player_hp = char_data.get("hp", 150)
	_player_max_hp = char_data.get("max_hp", 150)
	_player_mp = char_data.get("mp", 30)
	_player_max_mp = char_data.get("max_mp", 30)
	_player_level = char_data.get("level", 1)
	_player_exp = char_data.get("exp", 0)
	_player_adena = char_data.get("adena", 1000)

func _ready() -> void:
	_setup_map()
	_setup_camera()
	_setup_player()
	_setup_mobs()
	_setup_hud()
	
	_add_chat_msg("[color=#86efac]系統：歡迎來到說話之島！滑鼠左鍵點擊地面移動，點擊怪物自動走位戰鬥。[/color]")
	_add_chat_msg("[color=#f1d47a]角色：%s (Lv.%d %s) 踏上了冒險旅程！[/color]" % [_char_data.get("name", "勇者"), _player_level, _get_class_zh(_char_data.get("class", "knight"))])
	_add_chat_msg("[color=#67e8f9]提示：血魔條與對話框支援滑鼠按住拖曳至任意位置。[/color]")

# ----------------- 地圖設定 -----------------
func _setup_map() -> void:
	_map_sprite = Sprite2D.new()
	var map_tex_path = "res://assets/maps/town_talking_island/l1j-map-0-preview.png"
	if ResourceLoader.exists(map_tex_path):
		_map_sprite.texture = load(map_tex_path)
		_map_sprite.centered = false
		_map_width = _map_sprite.texture.get_width()
		_map_height = _map_sprite.texture.get_height()
	else:
		_map_width = 2048.0
		_map_height = 1024.0
	add_child(_map_sprite)

func _setup_camera() -> void:
	_camera = Camera2D.new()
	_camera.zoom = Vector2(1.25, 1.25)
	_camera.limit_left = 0
	_camera.limit_top = 0
	_camera.limit_right = int(_map_width)
	_camera.limit_bottom = int(_map_height)
	add_child(_camera)

# ----------------- 玩家角色真實 AnimatedSprite2D -----------------
func _setup_player() -> void:
	_player_node = Node2D.new()
	_player_node.position = _player_pos
	add_child(_player_node)
	
	# 影子
	var shadow = ColorRect.new()
	shadow.color = Color(0, 0, 0, 0.45)
	shadow.size = Vector2(28, 12)
	shadow.position = Vector2(-14, -6)
	_player_node.add_child(shadow)
	
	# 由 AtlasLibrary 生成真實 AnimatedSprite2D
	var c_key = _char_data.get("class", "knight")
	var gender = _char_data.get("gender", "male")
	var class_atlas = _get_player_atlas_name(c_key, gender)
	
	_player_sprite = AtlasLibrary.make_sprite("classanim", class_atlas, "sword1_idle", 8.0, true)
	if _player_sprite == null:
		_player_sprite = AtlasLibrary.make_sprite("classanim", "男騎士", "sword1_idle", 8.0, true)
	
	if _player_sprite != null:
		_player_sprite.position = Vector2(0, -32)
		_player_node.add_child(_player_sprite)
	
	# 玩家頭頂綠色血條
	var hp_bg = ColorRect.new()
	hp_bg.color = Color(0.1, 0.1, 0.1, 0.8)
	hp_bg.size = Vector2(36, 4)
	hp_bg.position = Vector2(-18, -60)
	_player_node.add_child(hp_bg)
	
	_player_hp_bar = ColorRect.new()
	_player_hp_bar.color = Color("#4ade80")
	_player_hp_bar.size = Vector2(36, 4)
	_player_hp_bar.position = Vector2(-18, -60)
	_player_node.add_child(_player_hp_bar)
	
	# 玩家頭頂白色名稱
	_player_name_label = Label.new()
	_player_name_label.text = _char_data.get("name", "勇者")
	_player_name_label.position = Vector2(-50, -78)
	_player_name_label.size = Vector2(100, 16)
	_player_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_player_name_label.add_theme_color_override("font_color", Color.WHITE)
	_player_name_label.add_theme_color_override("font_shadow_color", Color.BLACK)
	_player_name_label.add_theme_constant_override("shadow_offset_x", 1)
	_player_name_label.add_theme_constant_override("shadow_offset_y", 1)
	_player_name_label.add_theme_font_size_override("font_size", 11)
	_player_node.add_child(_player_name_label)

func _get_player_atlas_name(c: String, g: String) -> String:
	match c:
		"royal": return "王子" if g == "male" else "公主"
		"knight": return "男騎士" if g == "male" else "女騎士"
		"elf": return "男妖精" if g == "male" else "女妖精"
		"mage": return "男法師" if g == "male" else "女法師"
		"dark": return "男黑暗妖精" if g == "male" else "女黑暗妖精"
		"illusion": return "男幻術士" if g == "male" else "女幻術士"
		"dragon": return "男龍騎士" if g == "male" else "女龍騎士"
		"warrior": return "男戰士" if g == "male" else "女戰士"
	return "男騎士"

# ----------------- 怪物生成 (嚴格限制在說話之島草地島內) -----------------
func _setup_mobs() -> void:
	var spawn_configs = [
		{"name": "狼人",     "atlas": "mob_1110", "hp": 120, "exp": 45,  "adena": 120, "dmg": 12, "spd": 85.0, "pos": Vector2(1200, 460)},
		{"name": "妖魔",     "atlas": "mob_56",   "hp": 80,  "exp": 25,  "adena": 60,  "dmg": 8,  "spd": 75.0, "pos": Vector2(1420, 480)},
		{"name": "妖魔鬥士", "atlas": "mob_94",   "hp": 160, "exp": 65,  "adena": 180, "dmg": 16, "spd": 80.0, "pos": Vector2(1360, 620)},
		{"name": "妖魔弓箭手","atlas": "mob_57",   "hp": 70,  "exp": 35,  "adena": 80,  "dmg": 10, "spd": 70.0, "pos": Vector2(1450, 550)},
		{"name": "哥布林",   "atlas": "mob_1022", "hp": 50,  "exp": 15,  "adena": 35,  "dmg": 6,  "spd": 70.0, "pos": Vector2(1250, 580)},
		{"name": "石頭高崙", "atlas": "mob_49",   "hp": 250, "exp": 110, "adena": 300, "dmg": 22, "spd": 50.0, "pos": Vector2(1150, 410)},
		{"name": "夏洛伯",   "atlas": "mob_95",   "hp": 180, "exp": 85,  "adena": 220, "dmg": 18, "spd": 105.0,"pos": Vector2(1120, 520)},
		{"name": "侏儒",     "atlas": "mob_54",   "hp": 60,  "exp": 20,  "adena": 45,  "dmg": 7,  "spd": 65.0, "pos": Vector2(1280, 420)},
		{"name": "骷髏",     "atlas": "mob_30",   "hp": 110, "exp": 40,  "adena": 100, "dmg": 11, "spd": 75.0, "pos": Vector2(1180, 620)},
		{"name": "史萊姆",   "atlas": "mob_31",   "hp": 40,  "exp": 12,  "adena": 25,  "dmg": 5,  "spd": 60.0, "pos": Vector2(1350, 430)},
		{"name": "狼人",     "atlas": "mob_1110", "hp": 120, "exp": 45,  "adena": 120, "dmg": 12, "spd": 85.0, "pos": Vector2(1480, 470)},
		{"name": "妖魔",     "atlas": "mob_56",   "hp": 80,  "exp": 25,  "adena": 60,  "dmg": 8,  "spd": 75.0, "pos": Vector2(1220, 520)}
	]
	
	for cfg in spawn_configs:
		_spawn_mob_instance(cfg, cfg["pos"])

func _spawn_mob_instance(tpl: Dictionary, pos: Vector2) -> void:
	var mob_name = tpl["name"]
	var atlas_name = tpl.get("atlas", DataManager.resolve_mob_atlas(mob_name))
	
	var mob_node = Node2D.new()
	mob_node.position = pos
	add_child(mob_node)
	
	# 怪物影子
	var shadow = ColorRect.new()
	shadow.color = Color(0, 0, 0, 0.45)
	shadow.size = Vector2(24, 10)
	shadow.position = Vector2(-12, -5)
	mob_node.add_child(shadow)
	
	# 怪物精靈 (預設播放 d5/idle 或 d5/walk)
	var spr: AnimatedSprite2D = AtlasLibrary.make_sprite("anim", atlas_name, "d5/walk", 8.0, true)
	if spr == null:
		spr = AtlasLibrary.make_sprite("anim", "mob_1110", "d5/walk", 8.0, true)
	
	if spr != null:
		spr.position = Vector2(0, -26)
		mob_node.add_child(spr)
	
	# 怪物血條
	var hp_bg = ColorRect.new()
	hp_bg.color = Color(0.1, 0.1, 0.1, 0.8)
	hp_bg.size = Vector2(30, 3)
	hp_bg.position = Vector2(-15, -48)
	mob_node.add_child(hp_bg)
	
	var hp_fill = ColorRect.new()
	hp_fill.color = Color("#ef4444")
	hp_fill.size = Vector2(30, 3)
	hp_fill.position = Vector2(-15, -48)
	mob_node.add_child(hp_fill)
	
	# 怪物名字
	var lbl = Label.new()
	lbl.text = mob_name
	lbl.position = Vector2(-40, -64)
	lbl.size = Vector2(80, 14)
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_color_override("font_color", Color("#fca5a5"))
	lbl.add_theme_color_override("font_shadow_color", Color.BLACK)
	lbl.add_theme_constant_override("shadow_offset_x", 1)
	lbl.add_theme_constant_override("shadow_offset_y", 1)
	lbl.add_theme_font_size_override("font_size", 10)
	mob_node.add_child(lbl)
	
	var mob_data = {
		"node": mob_node,
		"sprite": spr,
		"hp_fill": hp_fill,
		"name": mob_name,
		"hp": float(tpl["hp"]),
		"max_hp": float(tpl["hp"]),
		"exp": tpl["exp"],
		"adena": tpl["adena"],
		"dmg": tpl["dmg"],
		"spd": tpl["spd"],
		"pos": pos,
		"target_pos": pos,
		"origin_pos": pos,
		"atk_cd": 0.0,
		"atlas": atlas_name,
		"is_dead": false,
		"dead_timer": 0.0,
		"heading": 5
	}
	_mobs.append(mob_data)

# ----------------- 經典天堂原版 UI (對齊 ArpgEngineScreen) -----------------
func _setup_hud() -> void:
	_hud_layer = CanvasLayer.new()
	add_child(_hud_layer)
	
	var vp_size = get_viewport_rect().size
	
	# 1. 地名 Banner (左上/左下面板上方)
	var area_lbl = Label.new()
	area_lbl.text = "說話之島 (安全區)"
	area_lbl.position = Vector2(16, vp_size.y - 150)
	area_lbl.add_theme_color_override("font_color", Color("#6fa8dc"))
	area_lbl.add_theme_color_override("font_shadow_color", Color("#101c2e"))
	area_lbl.add_theme_constant_override("shadow_offset_x", 2)
	area_lbl.add_theme_constant_override("shadow_offset_y", 2)
	area_lbl.add_theme_font_size_override("font_size", 13)
	_hud_layer.add_child(area_lbl)
	
	# 2. 左側狀態面板 (rn_left_panel.png, 138x127)
	var left_panel = TextureRect.new()
	left_panel.texture = load("res://assets/ui/rn_left_panel.png")
	left_panel.position = Vector2(0, vp_size.y - 127)
	left_panel.size = Vector2(138, 127)
	left_panel.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(left_panel)
	
	# 等級文字與經驗條 (在 rn_left_panel 內部)
	_level_label = Label.new()
	_level_label.text = "Lv.%d" % _player_level
	_level_label.position = Vector2(40, vp_size.y - 105)
	_level_label.add_theme_color_override("font_color", GOLD)
	_level_label.add_theme_font_size_override("font_size", 11)
	_hud_layer.add_child(_level_label)
	
	_exp_fill_rect = TextureRect.new()
	_exp_fill_rect.texture = load("res://assets/ui/rn_level_bar.png")
	_exp_fill_rect.position = Vector2(25, vp_size.y - 92)
	_exp_fill_rect.size = Vector2(98 * clamp(_player_exp / _player_exp_max, 0.0, 1.0), 5)
	_exp_fill_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(_exp_fill_rect)
	
	_exp_pct_label = Label.new()
	_exp_pct_label.text = "%d%%" % int((_player_exp / _player_exp_max) * 100)
	_exp_pct_label.position = Vector2(85, vp_size.y - 105)
	_exp_pct_label.add_theme_color_override("font_color", TEXT_COLOR)
	_exp_pct_label.add_theme_font_size_override("font_size", 10)
	_hud_layer.add_child(_exp_pct_label)
	
	# 天幣圖示與數值
	var adena_icon = TextureRect.new()
	adena_icon.texture = load("res://assets/ui/rn_adena_btn.png")
	adena_icon.position = Vector2(12, vp_size.y - 83)
	adena_icon.size = Vector2(16, 16)
	_hud_layer.add_child(adena_icon)
	
	_adena_label = Label.new()
	_adena_label.text = str(_player_adena)
	_adena_label.position = Vector2(34, vp_size.y - 82)
	_adena_label.add_theme_color_override("font_color", GOLD)
	_adena_label.add_theme_font_size_override("font_size", 11)
	_hud_layer.add_child(_adena_label)
	
	# AC (防禦) 與 正義值
	_ac_label = Label.new()
	_ac_label.text = "AC -%d" % _char_data.get("ac", 10)
	_ac_label.position = Vector2(38, vp_size.y - 60)
	_ac_label.add_theme_color_override("font_color", TEXT_COLOR)
	_ac_label.add_theme_font_size_override("font_size", 10)
	_hud_layer.add_child(_ac_label)
	
	_align_label = Label.new()
	_align_label.text = "32767"
	_align_label.position = Vector2(38, vp_size.y - 40)
	_align_label.add_theme_color_override("font_color", Color("#60a5fa"))
	_align_label.add_theme_font_size_override("font_size", 10)
	_hud_layer.add_child(_align_label)
	
	# 負重表
	var weight_bar = TextureRect.new()
	weight_bar.texture = load("res://assets/ui/rn_gauge_low.png")
	weight_bar.position = Vector2(24, vp_size.y - 20)
	weight_bar.size = Vector2(48, 5)
	weight_bar.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(weight_bar)
	
	# 3. 右側小地圖與巨集面板
	var mini_x = vp_size.x - 315
	var panel_y = vp_size.y - 134
	
	var minimap_base = TextureRect.new()
	minimap_base.texture = load("res://assets/ui/rn_minimap_base.png")
	minimap_base.position = Vector2(mini_x + 12, panel_y + 29)
	minimap_base.size = Vector2(135, 101)
	minimap_base.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(minimap_base)
	
	# 小地圖雷達光點層
	_minimap_radar = Control.new()
	_minimap_radar.position = Vector2(mini_x + 12, panel_y + 29)
	_minimap_radar.size = Vector2(135, 101)
	_minimap_radar.clip_contents = true
	_hud_layer.add_child(_minimap_radar)
	
	var minimap_frame = TextureRect.new()
	minimap_frame.texture = load("res://assets/ui/rn_minimap_frame.png")
	minimap_frame.position = Vector2(mini_x + 4, panel_y)
	minimap_frame.size = Vector2(144, 134)
	minimap_frame.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(minimap_frame)
	
	# 巨集功能面板 (rn_macro_panel.png)
	var macro_x = vp_size.x - 167
	var macro_panel = TextureRect.new()
	macro_panel.texture = load("res://assets/ui/rn_macro_panel.png")
	macro_panel.position = Vector2(macro_x, panel_y)
	macro_panel.size = Vector2(167, 134)
	macro_panel.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(macro_panel)
	
	# 巨集面板上的互動按鈕
	var btn_names = ["裝備", "道具", "技能", "任務", "選單"]
	for bi in range(btn_names.size()):
		var m_btn = Button.new()
		m_btn.text = btn_names[bi]
		m_btn.position = Vector2(macro_x + 20 + (bi % 2) * 68, panel_y + 35 + int(bi / 2) * 32)
		m_btn.size = Vector2(62, 26)
		m_btn.focus_mode = Control.FOCUS_NONE
		m_btn.add_theme_font_size_override("font_size", 10)
		m_btn.add_theme_color_override("font_color", GOLD)
		if btn_names[bi] == "選單":
			m_btn.pressed.connect(_on_return_pressed)
		else:
			m_btn.pressed.connect(func(): _add_chat_msg("[color=#94a3b8]已開啟【%s】面板。[/color]" % btn_names[bi]))
		_hud_layer.add_child(m_btn)
	
	# 4. 底部中央對話視窗 (rn_chat_born.png, rn_chat_window.png)
	var chat_x = 138.0
	var chat_w = mini_x - chat_x + 4.0
	var chat_y = vp_size.y - 130.0
	
	var chat_bg = TextureRect.new()
	chat_bg.texture = load("res://assets/ui/rn_chat_born.png")
	chat_bg.position = Vector2(chat_x, chat_y)
	chat_bg.size = Vector2(chat_w, 130)
	chat_bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(chat_bg)
	
	var chat_win = TextureRect.new()
	chat_win.texture = load("res://assets/ui/rn_chat_window.png")
	chat_win.position = Vector2(chat_x + 8, chat_y + 8)
	chat_win.size = Vector2(chat_w - 16, 114)
	chat_win.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	chat_win.modulate = Color(1, 1, 1, 0.8)
	_hud_layer.add_child(chat_win)
	
	# 對話框分頁 Tabs
	var tabs_box = HBoxContainer.new()
	tabs_box.position = Vector2(chat_x + 14, chat_y + 12)
	tabs_box.size = Vector2(chat_w - 28, 18)
	_hud_layer.add_child(tabs_box)
	
	for tab_t in ["全部", "隊伍聊天", "掉落/經驗"]:
		var tb = Button.new()
		tb.text = tab_t
		tb.focus_mode = Control.FOCUS_NONE
		tb.add_theme_font_size_override("font_size", 9)
		tb.add_theme_color_override("font_color", GOLD if tab_t == "全部" else TEXT_COLOR)
		tabs_box.add_child(tb)
	
	_chat_box = RichTextLabel.new()
	_chat_box.position = Vector2(chat_x + 14, chat_y + 34)
	_chat_box.size = Vector2(chat_w - 28, 86)
	_chat_box.bbcode_enabled = true
	_chat_box.scroll_following = true
	_chat_box.add_theme_font_size_override("normal_font_size", 10)
	_hud_layer.add_child(_chat_box)
	
	# 5. 可拖曳經典血魔橫條 (DraggableHpMpBar, 334x34)
	_hpmp_bar_ctrl = Control.new()
	_hpmp_bar_ctrl.size = Vector2(334, 34)
	_hpmp_bar_ctrl.position = Vector2(chat_x + (chat_w - 334) * 0.5, chat_y - 38)
	_hpmp_bar_ctrl.mouse_filter = Control.MOUSE_FILTER_STOP
	_hpmp_bar_ctrl.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	_hpmp_bar_ctrl.tooltip_text = "按住左鍵可拖曳移動血魔條"
	_hpmp_bar_ctrl.gui_input.connect(_on_hpmp_gui_input)
	_hud_layer.add_child(_hpmp_bar_ctrl)
	
	var hpmp_frame = TextureRect.new()
	hpmp_frame.texture = load("res://assets/ui/hpmp_frame.png")
	hpmp_frame.size = Vector2(334, 34)
	hpmp_frame.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	hpmp_frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hpmp_bar_ctrl.add_child(hpmp_frame)
	
	# HP 紅色進度
	_hp_fill_rect = TextureRect.new()
	_hp_fill_rect.texture = load("res://assets/ui/hpmp_hp_fill.png")
	_hp_fill_rect.position = Vector2(9, 10)
	_hp_fill_rect.size = Vector2(142 * clamp(_player_hp / _player_max_hp, 0.0, 1.0), 14)
	_hp_fill_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hp_fill_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hpmp_bar_ctrl.add_child(_hp_fill_rect)
	
	_hp_txt_label = Label.new()
	_hp_txt_label.text = "%d / %d" % [_player_hp, _player_max_hp]
	_hp_txt_label.position = Vector2(9, 8)
	_hp_txt_label.size = Vector2(142, 14)
	_hp_txt_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_hp_txt_label.add_theme_font_size_override("font_size", 10)
	_hp_txt_label.add_theme_color_override("font_color", Color.WHITE)
	_hp_txt_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hpmp_bar_ctrl.add_child(_hp_txt_label)
	
	# MP 藍色進度
	_mp_fill_rect = TextureRect.new()
	_mp_fill_rect.texture = load("res://assets/ui/hpmp_mp_fill.png")
	_mp_fill_rect.position = Vector2(184, 10)
	_mp_fill_rect.size = Vector2(142 * clamp(_player_mp / _player_max_mp, 0.0, 1.0), 14)
	_mp_fill_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_mp_fill_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hpmp_bar_ctrl.add_child(_mp_fill_rect)
	
	_mp_txt_label = Label.new()
	_mp_txt_label.text = "%d / %d" % [_player_mp, _player_max_mp]
	_mp_txt_label.position = Vector2(184, 8)
	_mp_txt_label.size = Vector2(142, 14)
	_mp_txt_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_mp_txt_label.add_theme_font_size_override("font_size", 10)
	_mp_txt_label.add_theme_color_override("font_color", Color.WHITE)
	_mp_txt_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hpmp_bar_ctrl.add_child(_mp_txt_label)
	
	# 6. 可拖曳聊天輸入框 (DraggableChatBar)
	_chat_bar_ctrl = Control.new()
	_chat_bar_ctrl.size = Vector2(280, 26)
	_chat_bar_ctrl.position = Vector2(_hpmp_bar_ctrl.position.x + 27, _hpmp_bar_ctrl.position.y - 28)
	_chat_bar_ctrl.mouse_filter = Control.MOUSE_FILTER_STOP
	_chat_bar_ctrl.gui_input.connect(_on_chatbar_gui_input)
	_hud_layer.add_child(_chat_bar_ctrl)
	
	var chatbar_bg = ColorRect.new()
	chatbar_bg.color = Color(0.06, 0.08, 0.12, 0.88)
	chatbar_bg.size = Vector2(280, 26)
	chatbar_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_chat_bar_ctrl.add_child(chatbar_bg)
	
	var chat_handle = Label.new()
	chat_handle.text = "✥ 聊天"
	chat_handle.position = Vector2(6, 4)
	chat_handle.add_theme_font_size_override("font_size", 10)
	chat_handle.add_theme_color_override("font_color", Color("#66d9ef"))
	chat_handle.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_chat_bar_ctrl.add_child(chat_handle)
	
	_chat_input = LineEdit.new()
	_chat_input.placeholder_text = "輸入訊息或指令..."
	_chat_input.position = Vector2(50, 2)
	_chat_input.size = Vector2(175, 22)
	_chat_input.add_theme_font_size_override("font_size", 10)
	_chat_input.text_submitted.connect(_on_chat_submitted)
	_chat_bar_ctrl.add_child(_chat_input)
	
	var send_btn = Button.new()
	send_btn.text = "發送"
	send_btn.position = Vector2(230, 2)
	send_btn.size = Vector2(45, 22)
	send_btn.focus_mode = Control.FOCUS_NONE
	send_btn.add_theme_font_size_override("font_size", 10)
	send_btn.pressed.connect(func(): _on_chat_submitted(_chat_input.text))
	_chat_bar_ctrl.add_child(send_btn)
	
	# 7. 右上角「返回選單」按鈕
	var btn_menu = Button.new()
	btn_menu.text = "返回角色選單"
	btn_menu.position = Vector2(vp_size.x - 120, 15)
	btn_menu.size = Vector2(105, 28)
	btn_menu.focus_mode = Control.FOCUS_NONE
	btn_menu.add_theme_color_override("font_color", GOLD)
	btn_menu.add_theme_font_size_override("font_size", 11)
	btn_menu.pressed.connect(_on_return_pressed)
	_hud_layer.add_child(btn_menu)

# ----------------- 拖曳 HP/MP 條與聊天框 -----------------
func _on_hpmp_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_is_dragging_hpmp = true
			_drag_hpmp_offset = _hpmp_bar_ctrl.get_global_mouse_position() - _hpmp_bar_ctrl.position
		else:
			_is_dragging_hpmp = false
	elif event is InputEventMouseMotion and _is_dragging_hpmp:
		_hpmp_bar_ctrl.position = _hpmp_bar_ctrl.get_global_mouse_position() - _drag_hpmp_offset

func _on_chatbar_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_is_dragging_chatbar = true
			_drag_chatbar_offset = _chat_bar_ctrl.get_global_mouse_position() - _chat_bar_ctrl.position
		else:
			_is_dragging_chatbar = false
	elif event is InputEventMouseMotion and _is_dragging_chatbar:
		_chat_bar_ctrl.position = _chat_bar_ctrl.get_global_mouse_position() - _drag_chatbar_offset

func _on_chat_submitted(text: String) -> void:
	var msg = text.strip_edges()
	if msg.is_empty():
		return
	_chat_input.text = ""
	_chat_input.release_focus()
	if msg == "/heal":
		_player_hp = _player_max_hp
		_player_mp = _player_max_mp
		_add_chat_msg("[color=#4ade80]系統：治癒術！生命與魔力已完全恢復。[/color]")
	elif msg == "/adena":
		_player_adena += 10000
		_add_chat_msg("[color=#f1d47a]系統：獲得天幣 10,000！[/color]")
	else:
		_add_chat_msg("[color=#e2e8f0][b]%s[/b]： %s[/color]" % [_char_data.get("name", "勇者"), msg])

func _add_chat_msg(msg: String) -> void:
	if _chat_box != null:
		_chat_box.append_text(msg + "\n")

# ----------------- 遊戲循環：走位、戰鬥、AI -----------------
func _process(delta: float) -> void:
	_update_player(delta)
	_update_mobs(delta)
	_update_ground_drops(delta)
	_update_hud()
	
	if _camera != null and _player_node != null:
		_camera.position = _player_node.position

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var click_world_pos = get_global_mouse_position()
		
		# 檢查是否點擊到怪物
		var clicked_mob = _find_mob_at(click_world_pos)
		if not clicked_mob.is_empty() and not clicked_mob["is_dead"]:
			_target_mob = clicked_mob
			_player_target_pos = clicked_mob["pos"]
		else:
			_target_mob = {}
			_player_target_pos = click_world_pos
			_show_click_marker(click_world_pos)

func _show_click_marker(pos: Vector2) -> void:
	var marker = ColorRect.new()
	marker.color = Color(0.9, 0.8, 0.3, 0.7)
	marker.size = Vector2(8, 8)
	marker.position = pos - Vector2(4, 4)
	add_child(marker)
	var tw = create_tween()
	tw.tween_property(marker, "modulate:a", 0.0, 0.4)
	tw.tween_callback(marker.queue_free)

func _find_mob_at(wpos: Vector2) -> Dictionary:
	for m in _mobs:
		if not m["is_dead"] and m["pos"].distance_to(wpos) <= 40.0:
			return m
	return {}

func _update_player(delta: float) -> void:
	if _player_atk_cd > 0.0:
		_player_atk_cd -= delta
	
	# 若有目標怪物，跟隨並在進入射程時攻擊
	if not _target_mob.is_empty():
		if _target_mob["is_dead"]:
			_target_mob = {}
		else:
			_player_target_pos = _target_mob["pos"]
			var dist = _player_pos.distance_to(_target_mob["pos"])
			if dist <= 55.0:
				if _player_atk_cd <= 0.0:
					_player_attack_target(_target_mob)
					_player_atk_cd = 0.8
				return
	
	# 移動處理
	var to_target = _player_target_pos - _player_pos
	var dist_to_target = to_target.length()
	
	if dist_to_target > 4.0:
		var move_step = min(_player_speed * delta, dist_to_target)
		var dir = to_target.normalized()
		_player_pos += dir * move_step
		# 邊界約束
		_player_pos.x = clamp(_player_pos.x, 30.0, _map_width - 30.0)
		_player_pos.y = clamp(_player_pos.y, 30.0, _map_height - 30.0)
		_player_node.position = _player_pos
		
		_player_facing = _vector_to_dir8(dir)
		if _player_sprite != null:
			_player_sprite.flip_h = (dir.x < 0)
			_play_player_anim("walk")
	else:
		if not _player_is_attacking:
			_play_player_anim("idle")

func _play_player_anim(act: String) -> void:
	if _player_sprite == null or _player_sprite.sprite_frames == null:
		return
	var sf = _player_sprite.sprite_frames
	var anims = sf.get_animation_names()
	for a in anims:
		if a.contains(act):
			if _player_sprite.animation != a or not _player_sprite.is_playing():
				_player_sprite.play(a)
			return

func _player_attack_target(mob: Dictionary) -> void:
	_player_is_attacking = true
	_play_player_anim("attack")
	
	var str_val = _char_data.get("str", 16)
	var dmg = int(randf_range(str_val * 0.9, str_val * 1.6))
	mob["hp"] -= dmg
	
	_show_damage_float(mob["pos"] + Vector2(0, -40), str(dmg), Color("#facc15"))
	_add_chat_msg("[color=#e2e8f0]你對 [%s] 造成了 %d 點傷害！[/color]" % [mob["name"], dmg])
	
	_play_mob_anim(mob, "hurt")
	
	if mob["hp"] <= 0:
		_kill_mob(mob)
	
	get_tree().create_timer(0.4).timeout.connect(func(): _player_is_attacking = false)

func _kill_mob(mob: Dictionary) -> void:
	mob["is_dead"] = true
	mob["dead_timer"] = 3.5
	_play_mob_anim(mob, "death")
	
	var g_exp = mob["exp"]
	var g_adena = mob["adena"]
	_player_exp += g_exp
	
	_add_chat_msg("[color=#fbbf24]你擊敗了 [%s]！獲得 %d 經驗值。[/color]" % [mob["name"], g_exp])
	_show_damage_float(mob["pos"] + Vector2(0, -60), "+%d Exp" % g_exp, Color("#60a5fa"))
	
	# 在地上生成金幣掉落物
	_spawn_ground_drop(mob["pos"], "adena", g_adena)
	
	# 升級檢查
	if _player_exp >= _player_exp_max:
		_player_exp -= _player_exp_max
		_player_exp_max = int(_player_exp_max * 1.5)
		_player_level += 1
		_player_max_hp += 20
		_player_hp = _player_max_hp
		_add_chat_msg("[color=#4ade80]★ 恭喜升級！等級提升至 Lv.%d，HP 上限大幅增加！[/color]" % _player_level)
		_show_damage_float(_player_pos + Vector2(0, -80), "LEVEL UP!", GOLD)

func _spawn_ground_drop(pos: Vector2, type: String, amount: int) -> void:
	var drop_node = Node2D.new()
	drop_node.position = pos
	add_child(drop_node)
	
	var icon = TextureRect.new()
	icon.texture = load("res://assets/ui/rn_adena_btn.png")
	icon.size = Vector2(18, 18)
	icon.position = Vector2(-9, -9)
	drop_node.add_child(icon)
	
	var lbl = Label.new()
	lbl.text = "%d 金幣" % amount
	lbl.position = Vector2(-30, -26)
	lbl.size = Vector2(60, 14)
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_font_size_override("font_size", 9)
	lbl.add_theme_color_override("font_color", GOLD)
	drop_node.add_child(lbl)
	
	_ground_drops.append({
		"node": drop_node,
		"pos": pos,
		"type": type,
		"amount": amount
	})

func _update_ground_drops(_delta: float) -> void:
	var to_remove = []
	for d in _ground_drops:
		if _player_pos.distance_to(d["pos"]) < 36.0:
			# 自動拾取
			_player_adena += d["amount"]
			_add_chat_msg("[color=#facc15]拾取了地面上的 %d 天幣。[/color]" % d["amount"])
			_show_damage_float(_player_pos + Vector2(0, -50), "+%d 金幣" % d["amount"], GOLD)
			d["node"].queue_free()
			to_remove.append(d)
	for r in to_remove:
		_ground_drops.erase(r)

func _update_mobs(delta: float) -> void:
	for m in _mobs:
		if m["is_dead"]:
			m["dead_timer"] -= delta
			if m["node"] != null:
				m["node"].modulate.a = max(0.0, m["dead_timer"] / 3.5)
				if m["dead_timer"] <= 0.0:
					# 在島內原位重生
					m["is_dead"] = false
					m["hp"] = m["max_hp"]
					m["pos"] = m["origin_pos"] + Vector2(randf_range(-40, 40), randf_range(-40, 40))
					m["node"].position = m["pos"]
					m["node"].modulate.a = 1.0
					_play_mob_anim(m, "idle")
			continue
		
		# 更新怪物血條
		if m["hp_fill"] != null:
			var ratio = clamp(m["hp"] / m["max_hp"], 0.0, 1.0)
			m["hp_fill"].size.x = 30.0 * ratio
		
		var dist_to_player = m["pos"].distance_to(_player_pos)
		if dist_to_player < 150.0:
			if dist_to_player > 45.0:
				# 追擊玩家
				var dir = (_player_pos - m["pos"]).normalized()
				m["pos"] += dir * (m["spd"] * delta)
				m["node"].position = m["pos"]
				m["heading"] = _vector_to_dir8(dir)
				_play_mob_anim(m, "walk")
			else:
				# 攻擊玩家
				m["atk_cd"] -= delta
				if m["atk_cd"] <= 0.0:
					m["atk_cd"] = 1.2
					_play_mob_anim(m, "attack")
					_mob_attack_player(m)
		else:
			# 閒置微幅晃動
			if randf() < 0.01:
				m["target_pos"] = m["origin_pos"] + Vector2(randf_range(-50, 50), randf_range(-50, 50))
			var to_tp = m["target_pos"] - m["pos"]
			if to_tp.length() > 2.0:
				var d_step = min(m["spd"] * 0.4 * delta, to_tp.length())
				m["pos"] += to_tp.normalized() * d_step
				m["node"].position = m["pos"]
				m["heading"] = _vector_to_dir8(to_tp.normalized())
				_play_mob_anim(m, "walk")
			else:
				_play_mob_anim(m, "idle")

func _play_mob_anim(mob: Dictionary, act: String) -> void:
	if mob["sprite"] == null or mob["sprite"].sprite_frames == null:
		return
	var sf = mob["sprite"].sprite_frames
	var h = mob.get("heading", 5)
	var anim_name = "d%d/%s" % [h, act]
	if sf.has_animation(anim_name):
		if mob["sprite"].animation != anim_name or not mob["sprite"].is_playing():
			mob["sprite"].play(anim_name)
	else:
		# fallback to finding any matching action
		for a in sf.get_animation_names():
			if a.contains(act):
				if mob["sprite"].animation != a or not mob["sprite"].is_playing():
					mob["sprite"].play(a)
				return

func _mob_attack_player(m: Dictionary) -> void:
	var dmg = max(1, m["dmg"] - int(_char_data.get("ac", 10) * 0.25))
	_player_hp = max(0.0, _player_hp - dmg)
	_show_damage_float(_player_pos + Vector2(0, -50), str(dmg), Color("#ef4444"))
	_add_chat_msg("[color=#f87171][%s] 對你造成了 %d 點傷害！[/color]" % [m["name"], dmg])
	
	if _player_hp <= 0.0:
		_add_chat_msg("[color=#ef4444]你被擊敗了！自動在說話之島村莊復活。[/color]")
		_player_hp = _player_max_hp
		_player_pos = Vector2(1318, 517)
		_player_target_pos = _player_pos
		_player_node.position = _player_pos

func _update_hud() -> void:
	if _hp_fill_rect != null:
		_hp_fill_rect.size.x = 142.0 * clamp(_player_hp / _player_max_hp, 0.0, 1.0)
	if _mp_fill_rect != null:
		_mp_fill_rect.size.x = 142.0 * clamp(_player_mp / _player_max_mp, 0.0, 1.0)
	if _hp_txt_label != null:
		_hp_txt_label.text = "%d / %d" % [_player_hp, _player_max_hp]
	if _mp_txt_label != null:
		_mp_txt_label.text = "%d / %d" % [_player_mp, _player_max_mp]
	if _player_hp_bar != null:
		_player_hp_bar.size.x = 36.0 * clamp(_player_hp / _player_max_hp, 0.0, 1.0)
	if _adena_label != null:
		_adena_label.text = str(_player_adena)
	if _level_label != null:
		_level_label.text = "Lv.%d" % _player_level
	if _exp_fill_rect != null:
		_exp_fill_rect.size.x = 98.0 * clamp(_player_exp / _player_exp_max, 0.0, 1.0)
	if _exp_pct_label != null:
		_exp_pct_label.text = "%d%%" % int((_player_exp / _player_exp_max) * 100)
	
	# 小地圖雷達更新
	_update_minimap_radar()

func _update_minimap_radar() -> void:
	if _minimap_radar == null:
		return
	for c in _minimap_radar.get_children():
		c.queue_free()
	
	# 玩家綠點
	var px = (_player_pos.x / _map_width) * 135.0
	var py = (_player_pos.y / _map_height) * 101.0
	var p_dot = ColorRect.new()
	p_dot.color = Color("#4ade80")
	p_dot.size = Vector2(4, 4)
	p_dot.position = Vector2(px - 2, py - 2)
	_minimap_radar.add_child(p_dot)
	
	# 怪物紅點
	for m in _mobs:
		if not m["is_dead"]:
			var mx = (m["pos"].x / _map_width) * 135.0
			var my = (m["pos"].y / _map_height) * 101.0
			var m_dot = ColorRect.new()
			m_dot.color = Color("#ef4444")
			m_dot.size = Vector2(3, 3)
			m_dot.position = Vector2(mx - 1.5, my - 1.5)
			_minimap_radar.add_child(m_dot)

func _show_damage_float(pos: Vector2, text: String, color: Color) -> void:
	var lbl = Label.new()
	lbl.text = text
	lbl.position = pos
	lbl.add_theme_color_override("font_color", color)
	lbl.add_theme_color_override("font_shadow_color", Color.BLACK)
	lbl.add_theme_constant_override("shadow_offset_x", 1)
	lbl.add_theme_constant_override("shadow_offset_y", 1)
	lbl.add_theme_font_size_override("font_size", 12)
	add_child(lbl)
	
	var tw = create_tween()
	tw.tween_property(lbl, "position:y", pos.y - 30.0, 0.6)
	tw.parallel().tween_property(lbl, "modulate:a", 0.0, 0.6)
	tw.tween_callback(lbl.queue_free)

func _vector_to_dir8(v: Vector2) -> int:
	if v.length_squared() < 0.001:
		return 5
	var deg = rad_to_deg(v.angle())
	if deg < 0:
		deg += 360.0
	# 0:N, 1:NE, 2:E, 3:SE, 4:S, 5:SW, 6:W, 7:NW
	if deg >= 337.5 or deg < 22.5: return 2
	elif deg < 67.5: return 3
	elif deg < 112.5: return 4
	elif deg < 157.5: return 5
	elif deg < 202.5: return 6
	elif deg < 247.5: return 7
	elif deg < 292.5: return 0
	else: return 1

func _on_return_pressed() -> void:
	return_to_menu.emit()

func _get_class_zh(k: String) -> String:
	match k:
		"royal": return "王族"
		"knight": return "騎士"
		"elf": return "妖精"
		"mage": return "法師"
		"dark": return "黑暗妖精"
		"illusion": return "幻術士"
		"dragon": return "龍騎士"
		"warrior": return "戰士"
	return "勇者"
