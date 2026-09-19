extends Node2D

# 放置天堂 Web 版 - 說話之島 ARPG 戰鬥世界 (ArpgWorld)
# 完整還原原版 ArpgEngineScreen.cs / ArpgActor.cs：
# 1. 玩家真實職業 AnimatedSprite2D 動畫 (8方向行走/攻擊/站立)
# 2. 怪物真實 L1J AnimatedSprite2D 精靈 (狼人 mob_1011, 妖魔 mob_110, 高崙 mob_1020 等)
# 3. 經典血魔雙球 (hpmp_frame.png / hpmp_hp_fill.png / hpmp_mp_fill.png)
# 4. 經典底欄 (classic_bottom_bar.png)、經驗值條、天幣 HUD、半透明聊天窗與飄字戰鬥

signal return_to_menu

var _char_data: Dictionary = {}
var _slot: int = 1

# 地圖相關
var _map_sprite: Sprite2D
var _map_width: float = 3840.0
var _map_height: float = 2880.0

# 玩家實體
var _player_node: Node2D
var _player_sprite: AnimatedSprite2D
var _player_hp_bar: ColorRect
var _player_name_label: Label
var _player_pos: Vector2 = Vector2(1900, 1400)
var _player_target_pos: Vector2 = Vector2(1900, 1400)
var _player_speed: float = 180.0
var _player_hp: float = 100.0
var _player_max_hp: float = 100.0
var _player_mp: float = 30.0
var _player_max_mp: float = 30.0
var _player_level: int = 1
var _player_exp: float = 0.0
var _player_exp_max: float = 100.0
var _player_adena: int = 1000
var _player_facing: int = 2 # 0~7 方向
var _player_is_attacking: bool = false
var _player_atk_cd: float = 0.0

# 怪物列表 [{node, sprite, name, hp, max_hp, pos, target_pos, state, atk_cd, speed, atlas, is_dead, dead_timer}]
var _mobs: Array[Dictionary] = []
var _target_mob: Dictionary = {}

# HUD 節點
var _hud_layer: CanvasLayer
var _hp_fill_rect: TextureRect
var _mp_fill_rect: TextureRect
var _hp_text_label: Label
var _mp_text_label: Label
var _exp_fill_rect: TextureRect
var _adena_label: Label
var _level_label: Label
var _chat_box: RichTextLabel
var _camera: Camera2D

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

func _setup_map() -> void:
	_map_sprite = Sprite2D.new()
	var map_tex_path = "res://assets/maps/town_talking_island/l1j-map-0-preview.png"
	if ResourceLoader.exists(map_tex_path):
		_map_sprite.texture = load(map_tex_path)
		_map_sprite.centered = false
		_map_width = _map_sprite.texture.get_width()
		_map_height = _map_sprite.texture.get_height()
	add_child(_map_sprite)

func _setup_camera() -> void:
	_camera = Camera2D.new()
	_camera.zoom = Vector2(1.2, 1.2)
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
	
	# 影子 (橢圓)
	var shadow = ColorRect.new()
	shadow.color = Color(0, 0, 0, 0.4)
	shadow.size = Vector2(28, 12)
	shadow.position = Vector2(-14, -6)
	_player_node.add_child(shadow)
	
	# 決定 classanim 圖集名稱
	var c_key = _char_data.get("class", "knight")
	var gender = _char_data.get("gender", "male")
	var class_atlas = _get_player_atlas_name(c_key, gender)
	
	# 由 AtlasLibrary 生成真實 AnimatedSprite2D
	_player_sprite = AtlasLibrary.make_sprite("classanim", class_atlas, "sword1_walk", 8.0, true)
	if _player_sprite == null:
		# Fallback 尋找任意可用
		_player_sprite = AtlasLibrary.make_sprite("classanim", "男騎士", "sword1_walk", 8.0, true)
	
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

# ----------------- 怪物動態生成真實 AnimatedSprite2D -----------------
func _setup_mobs() -> void:
	var spawn_templates = [
		{"name": "狼人", "hp": 120, "max_hp": 120, "exp": 45, "adena": 120, "dmg": 12, "spd": 90.0},
		{"name": "妖魔", "hp": 80,  "max_hp": 80,  "exp": 25, "adena": 60,  "dmg": 8,  "spd": 80.0},
		{"name": "妖魔鬥士", "hp": 160, "max_hp": 160, "exp": 65, "adena": 180, "dmg": 16, "spd": 85.0},
		{"name": "哥布林", "hp": 50,  "max_hp": 50,  "exp": 15, "adena": 35,  "dmg": 6,  "spd": 75.0},
		{"name": "高崙", "hp": 250, "max_hp": 250, "exp": 110, "adena": 300, "dmg": 22, "spd": 55.0},
		{"name": "夏洛伯", "hp": 180, "max_hp": 180, "exp": 85, "adena": 220, "dmg": 18, "spd": 110.0}
	]
	
	# 在說話之島隨機生成 12 隻怪
	for i in range(12):
		var tpl = spawn_templates[i % spawn_templates.size()]
		var spawn_x = 1900 + randf_range(-400, 400)
		var spawn_y = 1400 + randf_range(-350, 350)
		_spawn_mob_instance(tpl, Vector2(spawn_x, spawn_y))

func _spawn_mob_instance(tpl: Dictionary, pos: Vector2) -> void:
	var mob_name = tpl["name"]
	var atlas_name = DataManager.resolve_mob_atlas(mob_name)
	
	var mob_node = Node2D.new()
	mob_node.position = pos
	add_child(mob_node)
	
	# 怪物影子
	var shadow = ColorRect.new()
	shadow.color = Color(0, 0, 0, 0.4)
	shadow.size = Vector2(24, 10)
	shadow.position = Vector2(-12, -5)
	mob_node.add_child(shadow)
	
	# 怪物精靈
	var spr: AnimatedSprite2D = AtlasLibrary.make_sprite("anim", atlas_name, "d5/walk", 8.0, true)
	if spr == null:
		spr = AtlasLibrary.make_sprite("anim", "mob_1011", "d5/walk", 8.0, true)
	
	if spr != null:
		spr.position = Vector2(0, -28)
		mob_node.add_child(spr)
	
	# 怪物血條
	var hp_bg = ColorRect.new()
	hp_bg.color = Color(0.1, 0.1, 0.1, 0.8)
	hp_bg.size = Vector2(30, 3)
	hp_bg.position = Vector2(-15, -50)
	mob_node.add_child(hp_bg)
	
	var hp_fill = ColorRect.new()
	hp_fill.color = Color("#ef4444")
	hp_fill.size = Vector2(30, 3)
	hp_fill.position = Vector2(-15, -50)
	mob_node.add_child(hp_fill)
	
	# 怪物名字
	var lbl = Label.new()
	lbl.text = mob_name
	lbl.position = Vector2(-40, -66)
	lbl.size = Vector2(80, 14)
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_color_override("font_color", Color("#fca5a5"))
	lbl.add_theme_font_size_override("font_size", 10)
	mob_node.add_child(lbl)
	
	var mob_data = {
		"node": mob_node,
		"sprite": spr,
		"hp_fill": hp_fill,
		"name": mob_name,
		"hp": float(tpl["hp"]),
		"max_hp": float(tpl["max_hp"]),
		"exp": tpl["exp"],
		"adena": tpl["adena"],
		"dmg": tpl["dmg"],
		"spd": tpl["spd"],
		"pos": pos,
		"target_pos": pos,
		"atk_cd": 0.0,
		"atlas": atlas_name,
		"is_dead": false,
		"dead_timer": 0.0
	}
	_mobs.append(mob_data)

# ----------------- 經典天堂底欄 HUD 與血魔雙球 -----------------
func _setup_hud() -> void:
	_hud_layer = CanvasLayer.new()
	add_child(_hud_layer)
	
	var vp_size = get_viewport_rect().size
	
	# 1. 經典底部主底欄 (classic_bottom_bar.png)
	var bar_tex = load("res://assets/ui/classic_bottom_bar.png")
	var bottom_bar = TextureRect.new()
	bottom_bar.texture = bar_tex
	bottom_bar.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	bottom_bar.size = Vector2(800, 68)
	bottom_bar.position = Vector2(0, vp_size.y - 68)
	bottom_bar.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bottom_bar.stretch_mode = TextureRect.STRETCH_SCALE
	_hud_layer.add_child(bottom_bar)
	
	# 經驗值條 (在底欄下方)
	_exp_fill_rect = TextureRect.new()
	_exp_fill_rect.texture = load("res://assets/ui/exp_bar_fill.png")
	_exp_fill_rect.position = Vector2(170, vp_size.y - 12)
	_exp_fill_rect.size = Vector2(400 * (_player_exp / _player_exp_max), 6)
	_exp_fill_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(_exp_fill_rect)
	
	# 天幣圖示與數字
	var adena_icon = TextureRect.new()
	adena_icon.texture = load("res://assets/ui/rn_adena_btn.png")
	adena_icon.position = Vector2(600, vp_size.y - 48)
	adena_icon.size = Vector2(18, 18)
	_hud_layer.add_child(adena_icon)
	
	_adena_label = Label.new()
	_adena_label.text = str(_player_adena)
	_adena_label.position = Vector2(624, vp_size.y - 48)
	_adena_label.size = Vector2(100, 18)
	_adena_label.add_theme_color_override("font_color", GOLD)
	_adena_label.add_theme_font_size_override("font_size", 12)
	_hud_layer.add_child(_adena_label)
	
	# 等級按鈕與顯示
	var lvl_icon = TextureRect.new()
	lvl_icon.texture = load("res://assets/ui/rn_level_btn.png")
	lvl_icon.position = Vector2(180, vp_size.y - 48)
	lvl_icon.size = Vector2(18, 18)
	_hud_layer.add_child(lvl_icon)
	
	_level_label = Label.new()
	_level_label.text = "Lv.%d" % _player_level
	_level_label.position = Vector2(204, vp_size.y - 48)
	_level_label.size = Vector2(60, 18)
	_level_label.add_theme_color_override("font_color", TEXT_COLOR)
	_level_label.add_theme_font_size_override("font_size", 12)
	_hud_layer.add_child(_level_label)
	
	# 2. 經典血魔雙球 (hpmp_frame.png, hpmp_hp_fill.png, hpmp_mp_fill.png)
	var hpmp_container = Control.new()
	hpmp_container.position = Vector2(10, vp_size.y - 120)
	hpmp_container.size = Vector2(150, 110)
	_hud_layer.add_child(hpmp_container)
	
	# 紅血球填充
	_hp_fill_rect = TextureRect.new()
	_hp_fill_rect.texture = load("res://assets/ui/hpmp_hp_fill.png")
	_hp_fill_rect.position = Vector2(8, 12)
	_hp_fill_rect.size = Vector2(64, 64)
	_hp_fill_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	hpmp_container.add_child(_hp_fill_rect)
	
	# 藍魔球填充
	_mp_fill_rect = TextureRect.new()
	_mp_fill_rect.texture = load("res://assets/ui/hpmp_mp_fill.png")
	_mp_fill_rect.position = Vector2(76, 12)
	_mp_fill_rect.size = Vector2(64, 64)
	_mp_fill_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	hpmp_container.add_child(_mp_fill_rect)
	
	# 外層框架
	var hpmp_frame = TextureRect.new()
	hpmp_frame.texture = load("res://assets/ui/hpmp_frame.png")
	hpmp_frame.position = Vector2(0, 0)
	hpmp_frame.size = Vector2(148, 90)
	hpmp_frame.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	hpmp_container.add_child(hpmp_frame)
	
	# 數值文字
	_hp_text_label = Label.new()
	_hp_text_label.text = "%d" % _player_hp
	_hp_text_label.position = Vector2(12, 78)
	_hp_text_label.size = Vector2(56, 14)
	_hp_text_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_hp_text_label.add_theme_color_override("font_color", Color("#f87171"))
	_hp_text_label.add_theme_font_size_override("font_size", 10)
	hpmp_container.add_child(_hp_text_label)
	
	_mp_text_label = Label.new()
	_mp_text_label.text = "%d" % _player_mp
	_mp_text_label.position = Vector2(80, 78)
	_mp_text_label.size = Vector2(56, 14)
	_mp_text_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_mp_text_label.add_theme_color_override("font_color", Color("#60a5fa"))
	_mp_text_label.add_theme_font_size_override("font_size", 10)
	hpmp_container.add_child(_mp_text_label)
	
	# 3. 半透明聊天視窗 (rn_chat_window.png)
	var chat_bg = TextureRect.new()
	chat_bg.texture = load("res://assets/ui/rn_chat_window.png")
	chat_bg.position = Vector2(10, vp_size.y - 250)
	chat_bg.size = Vector2(280, 120)
	chat_bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	chat_bg.modulate = Color(1, 1, 1, 0.85)
	_hud_layer.add_child(chat_bg)
	
	_chat_box = RichTextLabel.new()
	_chat_box.position = Vector2(18, vp_size.y - 245)
	_chat_box.size = Vector2(264, 110)
	_chat_box.bbcode_enabled = true
	_chat_box.scroll_following = true
	_chat_box.add_theme_font_size_override("normal_font_size", 10)
	_hud_layer.add_child(_chat_box)
	
	# 4. 右上角「返回選單」按鈕
	var btn_menu = Button.new()
	btn_menu.text = "返回角色選單"
	btn_menu.position = Vector2(vp_size.x - 120, 15)
	btn_menu.size = Vector2(105, 28)
	btn_menu.focus_mode = Control.FOCUS_NONE
	btn_menu.add_theme_color_override("font_color", GOLD)
	btn_menu.add_theme_font_size_override("font_size", 11)
	btn_menu.pressed.connect(_on_return_pressed)
	_hud_layer.add_child(btn_menu)

func _add_chat_msg(msg: String) -> void:
	if _chat_box != null:
		_chat_box.append_text(msg + "\n")

# ----------------- 遊戲循環：走位、戰鬥、AI -----------------
func _process(delta: float) -> void:
	_update_player(delta)
	_update_mobs(delta)
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

func _find_mob_at(wpos: Vector2) -> Dictionary:
	for m in _mobs:
		if not m["is_dead"] and m["pos"].distance_to(wpos) <= 35.0:
			return m
	return {}

func _update_player(delta: float) -> void:
	if _player_atk_cd > 0.0:
		_player_atk_cd -= delta
	
	# 若有選取目標怪，持續追蹤
	if not _target_mob.is_empty():
		if _target_mob["is_dead"]:
			_target_mob = {}
		else:
			_player_target_pos = _target_mob["pos"]
			var dist = _player_pos.distance_to(_target_mob["pos"])
			if dist <= 55.0:
				# 進入近戰距離，發動攻擊
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
		_player_node.position = _player_pos
		
		# 轉向與動畫
		if _player_sprite != null:
			_player_sprite.flip_h = (dir.x < 0)
			if not _player_sprite.is_playing() or _player_sprite.animation.contains("idle"):
				_play_player_anim("walk")
	else:
		if not _player_is_attacking and _player_sprite != null:
			if _player_sprite.animation.contains("walk"):
				_play_player_anim("idle")

func _play_player_anim(act: String) -> void:
	if _player_sprite == null or _player_sprite.sprite_frames == null:
		return
	var sf = _player_sprite.sprite_frames
	var anims = sf.get_animation_names()
	for a in anims:
		if a.contains(act):
			_player_sprite.play(a)
			return

func _player_attack_target(mob: Dictionary) -> void:
	_play_player_anim("attack")
	
	# 計算物理傷害 (力量 + 武器骰子)
	var str_val = _char_data.get("str", 16)
	var dmg = int(randf_range(str_val * 0.8, str_val * 1.5))
	mob["hp"] -= dmg
	
	_show_damage_float(mob["pos"] + Vector2(0, -40), str(dmg), Color("#facc15"))
	_add_chat_msg("[color=#e2e8f0]你對 [%s] 造成了 %d 點傷害！[/color]" % [mob["name"], dmg])
	
	if mob["hp"] <= 0:
		_kill_mob(mob)

func _kill_mob(mob: Dictionary) -> void:
	mob["is_dead"] = true
	mob["dead_timer"] = 3.0
	
	# 獎勵結算
	var g_exp = mob["exp"]
	var g_adena = mob["adena"]
	_player_exp += g_exp
	_player_adena += g_adena
	
	_add_chat_msg("[color=#fbbf24]你擊敗了 [%s]！獲得 %d 經驗值與 %d 天幣。[/color]" % [mob["name"], g_exp, g_adena])
	_show_damage_float(mob["pos"] + Vector2(0, -60), "+%d Exp" % g_exp, Color("#60a5fa"))
	
	# 升級檢查
	if _player_exp >= _player_exp_max:
		_player_exp -= _player_exp_max
		_player_exp_max = int(_player_exp_max * 1.6)
		_player_level += 1
		_player_max_hp += 18
		_player_hp = _player_max_hp
		_add_chat_msg("[color=#4ade80]★ 恭喜升級！等級提升至 Lv.%d，HP 上限大幅增加！[/color]" % _player_level)
		_show_damage_float(_player_pos + Vector2(0, -80), "LEVEL UP!", GOLD)
	
	# 播放怪物死亡動畫
	if mob["sprite"] != null and mob["sprite"].sprite_frames != null:
		var anims = mob["sprite"].sprite_frames.get_animation_names()
		for a in anims:
			if a.contains("die"):
				mob["sprite"].play(a)
				break

func _update_mobs(delta: float) -> void:
	for m in _mobs:
		if m["is_dead"]:
			m["dead_timer"] -= delta
			if m["node"] != null:
				m["node"].modulate.a = max(0.0, m["dead_timer"] / 3.0)
				if m["dead_timer"] <= 0.0:
					# 重生
					m["is_dead"] = false
					m["hp"] = m["max_hp"]
					m["pos"] = Vector2(1900 + randf_range(-400, 400), 1400 + randf_range(-350, 350))
					m["node"].position = m["pos"]
					m["node"].modulate.a = 1.0
			continue
		
		# 更新怪物頭頂血條
		if m["hp_fill"] != null:
			var ratio = clamp(m["hp"] / m["max_hp"], 0.0, 1.0)
			m["hp_fill"].size.x = 30.0 * ratio
		
		# 簡單 AI：靠近玩家主動攻擊
		var dist_to_player = m["pos"].distance_to(_player_pos)
		if dist_to_player < 160.0:
			if dist_to_player > 45.0:
				# 追擊玩家
				var dir = (_player_pos - m["pos"]).normalized()
				m["pos"] += dir * (m["spd"] * delta)
				m["node"].position = m["pos"]
				if m["sprite"] != null:
					m["sprite"].flip_h = (dir.x < 0)
			else:
				# 攻擊玩家
				m["atk_cd"] -= delta
				if m["atk_cd"] <= 0.0:
					m["atk_cd"] = 1.2
					_mob_attack_player(m)
		else:
			# 閒置隨機晃動
			if randf() < 0.01:
				m["target_pos"] = m["pos"] + Vector2(randf_range(-50, 50), randf_range(-50, 50))

func _mob_attack_player(m: Dictionary) -> void:
	var dmg = max(1, m["dmg"] - int(_char_data.get("ac", 10) * 0.3))
	_player_hp = max(0, _player_hp - dmg)
	
	_show_damage_float(_player_pos + Vector2(0, -50), str(dmg), Color("#ef4444"))
	_add_chat_msg("[color=#f87171][%s] 對你造成了 %d 點傷害！[/color]" % [m["name"], dmg])
	
	if _player_hp <= 0:
		_add_chat_msg("[color=#ef4444]你倒下了... 正在說話之島安全區復原中。[/color]")
		_player_hp = _player_max_hp
		_player_pos = Vector2(1900, 1400)
		_player_node.position = _player_pos

func _show_damage_float(pos: Vector2, text: String, color: Color) -> void:
	var lbl = Label.new()
	lbl.text = text
	lbl.position = pos
	lbl.add_theme_color_override("font_color", color)
	lbl.add_theme_font_size_override("font_size", 14)
	add_child(lbl)
	
	var tw = create_tween()
	tw.tween_property(lbl, "position:y", pos.y - 30.0, 0.6)
	tw.parallel().tween_property(lbl, "modulate:a", 0.0, 0.6)
	tw.tween_callback(lbl.queue_free)

func _update_hud() -> void:
	if _hp_fill_rect != null:
		var hp_ratio = clamp(_player_hp / _player_max_hp, 0.0, 1.0)
		_hp_fill_rect.size.y = 64.0 * hp_ratio
		_hp_fill_rect.position.y = 12.0 + (64.0 * (1.0 - hp_ratio))
	if _mp_fill_rect != null:
		var mp_ratio = clamp(_player_mp / _player_max_mp, 0.0, 1.0)
		_mp_fill_rect.size.y = 64.0 * mp_ratio
		_mp_fill_rect.position.y = 12.0 + (64.0 * (1.0 - mp_ratio))
	if _hp_text_label != null:
		_hp_text_label.text = "%d" % _player_hp
	if _mp_text_label != null:
		_mp_text_label.text = "%d" % _player_mp
	if _player_hp_bar != null:
		_player_hp_bar.size.x = 36.0 * clamp(_player_hp / _player_max_hp, 0.0, 1.0)
	if _exp_fill_rect != null:
		_exp_fill_rect.size.x = 400.0 * clamp(_player_exp / _player_exp_max, 0.0, 1.0)
	if _adena_label != null:
		_adena_label.text = str(_player_adena)
	if _level_label != null:
		_level_label.text = "Lv.%d" % _player_level

func _on_return_pressed() -> void:
	# 儲存角色進度
	_char_data["hp"] = _player_hp
	_char_data["mp"] = _player_mp
	_char_data["level"] = _player_level
	_char_data["exp"] = _player_exp
	_char_data["adena"] = _player_adena
	var p = "user://character_slot_%d.json" % _slot
	var f = FileAccess.open(p, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(_char_data))
	
	return_to_menu.emit()

func _get_class_zh(c: String) -> String:
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
