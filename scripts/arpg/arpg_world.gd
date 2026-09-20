extends Node2D

# 放置天堂 Web 版 - 說話之島 ARPG 戰鬥世界
# 完整對齊原版 ArpgEngineScreen、ArpgActor、DraggableHpMpBar、DraggableChatBar

signal return_to_menu

const MAP_PREVIEW = "res://assets/maps/l1j-map-0-preview.png"
const GOLD = Color("#f1d47a")
const TEXT_COLOR = Color("#c9d1de")
const Sfx8 = ["2", "F", "", "d3", "d4", "d5", "d6", "d7"]

var _char_data: Dictionary = {}
var _slot: int = 1

# 地圖與相機
var _map_sprite: Sprite2D
var _map_width: float = 2048.0
var _map_height: float = 1024.0
var _camera: Camera2D

# 玩家角色節點
var _player_node: Node2D
var _player_sprite: AnimatedSprite2D
var _player_hp_bar: ColorRect
var _player_name_label: Label
var _player_pos: Vector2 = Vector2(1318, 517)
var _player_target_pos: Vector2 = Vector2(1318, 517)
var _player_speed: float = 150.0
var _player_cur_dir: int = 2
var _player_atlas_base: String = "男騎士"
var _player_dir_frames: Dictionary = {}
var _player_atk_cd: float = 0.0
var _player_is_attacking: bool = false
var _target_mob: Dictionary = {}

# 玩家屬性
var _player_hp: float = 150.0
var _player_max_hp: float = 150.0
var _player_mp: float = 30.0
var _player_max_mp: float = 30.0
var _player_level: int = 1
var _player_exp: float = 0.0
var _player_exp_max: float = 100.0
var _player_adena: int = 1000

# 怪物與掉落物清單
var _mobs: Array = []
var _ground_drops: Array = []

# HUD 節點
var _hud_layer: CanvasLayer
var _level_label: Label
var _exp_pct_label: Label
var _exp_fill_rect: TextureRect
var _adena_label: Label
var _ac_label: Label
var _align_label: Label
var _hpmp_bar_ctrl: Control
var _hp_fill_rect: TextureRect
var _hp_txt_label: Label
var _mp_fill_rect: TextureRect
var _mp_txt_label: Label
var _is_dragging_hpmp: bool = false
var _drag_hpmp_offset: Vector2 = Vector2.ZERO

# 聊天視窗
var _chat_box: RichTextLabel
var _chat_bar_ctrl: Control
var _chat_input: LineEdit
var _is_dragging_chatbar: bool = false
var _drag_chatbar_offset: Vector2 = Vector2.ZERO
var _minimap_radar: Control
var _chat_history: Array = []
var _active_chat_tab: String = "ALL"
var _tab_buttons: Dictionary = {}

# 互動對話視窗實例
var _dlg_equipment: Control = null
var _dlg_skills: Control = null
var _dlg_inventory: Control = null
var _dlg_quest: Control = null
var _dlg_room: Control = null

# 技能與道具清單
var _skills_list: Array = []
var _inventory_items: Array = []

# 多人房間網路
var _room_manager: Node = null
var _remote_players: Dictionary = {}

func init_world(char_data: Dictionary, slot: int) -> void:
	_char_data = char_data
	_slot = slot
	_player_hp = float(char_data.get("hp", 150))
	_player_max_hp = float(char_data.get("max_hp", 150))
	_player_mp = float(char_data.get("mp", 30))
	_player_max_mp = float(char_data.get("max_mp", 30))
	_player_level = int(char_data.get("level", 1))
	_player_adena = int(char_data.get("adena", 1000))
	_player_exp = float(char_data.get("exp", 0))
	_player_exp_max = 100.0 * _player_level

func _ready() -> void:
	_init_all_skills()
	_init_inventory()
	_setup_network()
	_setup_map()
	_setup_player()
	_setup_mobs()
	_setup_hud()
	_setup_dialogs()
	
	if has_node("/root/BackgroundPackLoader"):
		var loader = get_node("/root/BackgroundPackLoader")
		loader.pack_loaded.connect(_on_dlc_pack_loaded)
	
	_add_chat_msg("[color=#facc15]★ 歡迎來到說話之島！全技能已為您學滿，點擊【技能】面板即可施放。[/color]", "ALL")
	_add_chat_msg("[color=#60a5fa]系統：點擊地面移動，點擊怪物自動走位揮刀攻擊，擊敗怪物有金幣掉落。[/color]", "ALL")
	_add_chat_msg("[color=#4ade80]提示：點擊右側【連線開房】按鈕即可創房或輸入房號與好友組隊冒險！[/color]", "ALL")

# ----------------- 網路連線管理器初始化 -----------------
func _setup_network() -> void:
	var rm_script = load("res://scripts/network/room_manager.gd")
	_room_manager = Node.new()
	_room_manager.set_script(rm_script)
	add_child(_room_manager)
	
	_room_manager.remote_player_joined.connect(_on_remote_player_joined)
	_room_manager.remote_player_moved.connect(_on_remote_player_moved)
	_room_manager.remote_player_attacked.connect(_on_remote_player_attacked)
	_room_manager.remote_player_chatted.connect(_on_remote_player_chatted)
	_room_manager.remote_player_left.connect(_on_remote_player_left)
	_room_manager.room_state_changed.connect(_on_room_state_changed)

func _on_room_state_changed(is_in_room: bool, r_id: String, is_host: bool) -> void:
	if is_in_room:
		_add_chat_msg("[color=#38bdf8]連線：已成功加入房間【%s】(身份: %s)[/color]" % [r_id, "房主" if is_host else "隊員"], "PARTY")
		_room_manager.send_handshake(_char_data, _player_pos)
	else:
		_add_chat_msg("[color=#f87171]連線：已離開房間，返回單機模式。[/color]", "PARTY")
		for peer_id in _remote_players.keys():
			_remove_remote_player(peer_id)
	if _dlg_room != null and _dlg_room.has_method("update_display"):
		_dlg_room.update_display()

func _on_remote_player_joined(peer_id: String, info: Dictionary) -> void:
	_add_chat_msg("[color=#4ade80]隊友【%s】進入了房間！[/color]" % info.get("name", "隊友"), "PARTY")
	_spawn_remote_player(peer_id, info)
	if _dlg_room != null and _dlg_room.has_method("update_display"):
		_dlg_room.update_display()

func _on_remote_player_moved(peer_id: String, pos: Vector2, facing: int, is_moving: bool) -> void:
	if not _remote_players.has(peer_id):
		return
	var rp = _remote_players[peer_id]
	rp["target_pos"] = pos
	rp["facing"] = facing
	rp["moving"] = is_moving

func _on_remote_player_attacked(peer_id: String, pos: Vector2) -> void:
	if not _remote_players.has(peer_id):
		return
	var rp = _remote_players[peer_id]
	if rp["sprite"] != null:
		rp["sprite"].play("attack")
		_show_damage_float(pos + Vector2(0, -30), "⚔ 隊友攻擊", Color("#f59e0b"))

func _on_remote_player_chatted(peer_id: String, p_name: String, msg: String) -> void:
	_add_chat_msg("[color=#67e8f9][隊伍] [b]%s[/b]： %s[/color]" % [p_name, msg], "PARTY")
	if _remote_players.has(peer_id):
		_show_chat_bubble(_remote_players[peer_id]["node"], msg)

func _on_remote_player_left(peer_id: String) -> void:
	_add_chat_msg("[color=#94a3b8]隊友離開了房間。[/color]", "PARTY")
	_remove_remote_player(peer_id)
	if _dlg_room != null and _dlg_room.has_method("update_display"):
		_dlg_room.update_display()

func _spawn_remote_player(peer_id: String, info: Dictionary) -> void:
	if _remote_players.has(peer_id):
		return
	var p_node = Node2D.new()
	var spawn_pos = Vector2(info.get("pos_x", 1318), info.get("pos_y", 517))
	p_node.position = spawn_pos
	add_child(p_node)
	
	var shadow = ColorRect.new()
	shadow.color = Color(0, 0, 0, 0.4)
	shadow.size = Vector2(26, 10)
	shadow.position = Vector2(-13, -5)
	p_node.add_child(shadow)
	
	var c_key = info.get("class", "knight")
	var g_key = info.get("gender", "male")
	var atlas = _get_player_atlas_name(c_key, g_key)
	var spr = AtlasLibrary.make_sprite("classanim", atlas, "sword1_idle", 8.0, true)
	if spr != null:
		p_node.add_child(spr)
	
	var lbl = Label.new()
	lbl.text = "[隊友] " + info.get("name", "勇者")
	lbl.position = Vector2(-45, -60)
	lbl.size = Vector2(90, 14)
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_font_size_override("font_size", 10)
	lbl.add_theme_color_override("font_color", Color("#67e8f9"))
	p_node.add_child(lbl)
	
	_remote_players[peer_id] = {
		"node": p_node,
		"sprite": spr,
		"pos": spawn_pos,
		"target_pos": spawn_pos,
		"facing": 2,
		"moving": false
	}

func _remove_remote_player(peer_id: String) -> void:
	if _remote_players.has(peer_id):
		var rp = _remote_players[peer_id]
		if rp["node"] != null:
			rp["node"].queue_free()
		_remote_players.erase(peer_id)

func _show_chat_bubble(target_node: Node2D, text: String) -> void:
	if target_node == null:
		return
	var bubble = PanelContainer.new()
	bubble.position = Vector2(-50, -85)
	target_node.add_child(bubble)
	
	var lbl = Label.new()
	lbl.text = text
	lbl.add_theme_font_size_override("font_size", 10)
	lbl.add_theme_color_override("font_color", Color.WHITE)
	bubble.add_child(lbl)
	
	var tw = create_tween()
	tw.tween_interval(3.0)
	tw.tween_property(bubble, "modulate:a", 0.0, 0.5)
	tw.tween_callback(bubble.queue_free)

# ----------------- 全技能學滿與道具背包資料 -----------------
func _init_all_skills() -> void:
	_skills_list = [
		{"id": "sk_heal1", "name": "初級治癒術", "mp": 4, "icon": 2056, "desc": "瞬間恢復 35 點生命力", "type": "heal", "amount": 35},
		{"id": "sk_lightarrow", "name": "光箭", "mp": 3, "icon": 2057, "desc": "發射神聖魔法光箭攻擊目標", "type": "attack", "dmg": 22},
		{"id": "sk_shield", "name": "保護罩", "mp": 8, "icon": 2058, "desc": "提升自身防禦力 AC -2", "type": "buff", "buff": "shield"},
		{"id": "sk_teleport", "name": "指定傳送", "mp": 10, "icon": 2059, "desc": "隨機瞬間移動到島內安全地點", "type": "teleport"},
		{"id": "sk_heal2", "name": "中級治癒術", "mp": 12, "icon": 2060, "desc": "瞬間恢復 80 點生命力", "type": "heal", "amount": 80},
		{"id": "sk_firearrow", "name": "火箭", "mp": 4, "icon": 2061, "desc": "發射烈火之箭造成火屬性傷害", "type": "attack", "dmg": 28},
		{"id": "sk_curse_poison", "name": "毒咒", "mp": 10, "icon": 2062, "desc": "使目標陷入中毒狀態持續損血", "type": "attack", "dmg": 18},
		{"id": "sk_enchant_weapon", "name": "擬似魔法武器", "mp": 10, "icon": 2063, "desc": "提升近戰武器攻擊力 +2", "type": "buff", "buff": "atk"},
		{"id": "sk_ice_breath", "name": "寒冰氣息", "mp": 10, "icon": 2064, "desc": "噴吐極寒凍氣攻擊目標", "type": "attack", "dmg": 35},
		{"id": "sk_haste", "name": "加速術", "mp": 10, "icon": 2065, "desc": "提升移動速度與攻擊頻率", "type": "buff", "buff": "haste"},
		{"id": "sk_earth_jail", "name": "地裂術", "mp": 16, "icon": 2066, "desc": "震裂大地造成範圍巨量傷害", "type": "attack", "dmg": 55},
		{"id": "sk_lightning", "name": "極道落雷", "mp": 20, "icon": 2067, "desc": "召喚九天神雷轟擊目標", "type": "attack", "dmg": 75},
		{"id": "sk_fire_storm", "name": "烈炎術", "mp": 24, "icon": 2068, "desc": "召喚熾烈地獄之火吞噬目標", "type": "attack", "dmg": 95},
		{"id": "sk_heal3", "name": "高級治癒術", "mp": 24, "icon": 2069, "desc": "瞬間恢復 180 點生命力", "type": "heal", "amount": 180},
		{"id": "sk_holy_walk", "name": "神聖疾走", "mp": 15, "icon": 2070, "desc": "大幅提高奔跑疾行速度", "type": "buff", "buff": "speed"},
		{"id": "sk_bless_weapon", "name": "祝福魔法武器", "mp": 15, "icon": 2071, "desc": "提升武器命中率與攻擊力 +4", "type": "buff", "buff": "bless"},
		{"id": "sk_sanctuary", "name": "聖結界", "mp": 30, "icon": 2072, "desc": "受到的所有傷害降低 50%", "type": "buff", "buff": "barrier"},
		{"id": "sk_soul_ascent", "name": "靈魂昇華", "mp": 35, "icon": 2073, "desc": "提升最大生命與魔力上限", "type": "buff", "buff": "max_hp"},
		{"id": "sk_body_con", "name": "體魄強健術", "mp": 15, "icon": 2074, "desc": "力量與體質各提升 +5", "type": "buff", "buff": "str"},
		{"id": "sk_dex_boost", "name": "通暢氣脈術", "mp": 15, "icon": 2075, "desc": "敏捷屬性提升 +5", "type": "buff", "buff": "dex"},
		{"id": "sk_invis", "name": "隱身術", "mp": 20, "icon": 2076, "desc": "使自身進入完全隱形狀態", "type": "buff", "buff": "invis"},
		{"id": "sk_dispel", "name": "魔法相消術", "mp": 25, "icon": 2077, "desc": "消除目標身上的一切輔助魔法", "type": "attack", "dmg": 10},
		{"id": "sk_barrier", "name": "絕對屏障", "mp": 50, "icon": 2078, "desc": "進入完全無敵絕對防禦狀態", "type": "buff", "buff": "invincible"},
		{"id": "sk_meteor", "name": "究極光裂術", "mp": 60, "icon": 2079, "desc": "召喚究極光芒裂解目標！", "type": "attack", "dmg": 180},
		{"id": "sk_shock_stun", "name": "衝擊之暈", "mp": 15, "icon": 2080, "desc": "騎士絕技：重擊目標使其陷入暈眩！", "type": "attack", "dmg": 45},
		{"id": "sk_counter_barrier", "name": "反擊屏障", "mp": 30, "icon": 2081, "desc": "迴避近戰攻擊並對敵人雙倍反擊", "type": "buff", "buff": "counter"},
		{"id": "sk_triple_arrow", "name": "三重矢", "mp": 15, "icon": 2082, "desc": "妖精絕技：連續快速射出三枚箭矢", "type": "attack", "dmg": 60},
		{"id": "sk_double_break", "name": "雙重破壞", "mp": 20, "icon": 2083, "desc": "黑暗妖精：有機率造成雙倍暴擊傷害", "type": "buff", "buff": "double"},
		{"id": "sk_slaughter", "name": "屠宰者", "mp": 15, "icon": 2084, "desc": "龍騎士：狂暴連續三段毀滅重擊！", "type": "attack", "dmg": 75}
	]

func _init_inventory() -> void:
	_inventory_items = [
		{"id": "pot_red", "name": "紅色藥水", "count": 100, "icon": "res://assets/ui/rn_adena_btn.png", "desc": "恢復 45 點 HP", "type": "heal_hp", "val": 45, "usable": true},
		{"id": "pot_orange", "name": "橙色藥水", "count": 50, "icon": "res://assets/ui/rn_adena_btn.png", "desc": "恢復 90 點 HP", "type": "heal_hp", "val": 90, "usable": true},
		{"id": "pot_clear", "name": "白色藥水", "count": 20, "icon": "res://assets/ui/rn_adena_btn.png", "desc": "恢復 160 點 HP", "type": "heal_hp", "val": 160, "usable": true},
		{"id": "pot_brave", "name": "勇敢藥水", "count": 30, "icon": "res://assets/ui/rn_adena_btn.png", "desc": "提升近戰攻速 30%", "type": "buff_brave", "val": 30, "usable": true},
		{"id": "pot_green", "name": "加速藥水", "count": 30, "icon": "res://assets/ui/rn_adena_btn.png", "desc": "提升移動速度 40%", "type": "buff_green", "val": 30, "usable": true},
		{"id": "scroll_tp", "name": "瞬間移動卷軸", "count": 50, "icon": "res://assets/ui/rn_adena_btn.png", "desc": "隨機傳送到安全區域", "type": "teleport_rand", "usable": true},
		{"id": "scroll_home", "name": "傳送回家的卷軸", "count": 20, "icon": "res://assets/ui/rn_adena_btn.png", "desc": "瞬間回到說話之島村莊", "type": "teleport_home", "usable": true},
		{"id": "adena", "name": "天幣", "count": _player_adena, "icon": "res://assets/ui/rn_adena_btn.png", "desc": "亞丁大陸流通的黃金貨幣", "usable": false}
	]

# ----------------- 地圖與玩家設定 -----------------
func _setup_map() -> void:
	_map_sprite = Sprite2D.new()
	if ResourceLoader.exists(MAP_PREVIEW):
		_map_sprite.texture = load(MAP_PREVIEW)
		_map_sprite.centered = false
		_map_sprite.position = Vector2.ZERO
		var sz = _map_sprite.texture.get_size()
		_map_width = sz.x
		_map_height = sz.y
	add_child(_map_sprite)
	
	_camera = Camera2D.new()
	_camera.position = _player_pos
	_camera.limit_left = 0
	_camera.limit_top = 0
	_camera.limit_right = int(_map_width)
	_camera.limit_bottom = int(_map_height)
	_camera.position_smoothing_enabled = true
	_camera.position_smoothing_speed = 8.0
	add_child(_camera)

func _setup_player() -> void:
	_player_node = Node2D.new()
	_player_node.position = _player_pos
	add_child(_player_node)
	
	var shadow = ColorRect.new()
	shadow.color = Color(0, 0, 0, 0.45)
	shadow.size = Vector2(28, 12)
	shadow.position = Vector2(-14, -6)
	_player_node.add_child(shadow)
	
	var c_key = _char_data.get("class", "knight")
	var gender = _char_data.get("gender", "male")
	_player_atlas_base = _get_player_atlas_name(c_key, gender)
	
	_player_sprite = AnimatedSprite2D.new()
	_player_node.add_child(_player_sprite)
	_swap_player_dir(2) # 預設朝下方向
	
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

# ----------------- 玩家 8 方向轉向核心 (對齊原版 Sfx8) -----------------
func _swap_player_dir(new_dir: int) -> void:
	new_dir = (new_dir % 8 + 8) % 8
	if new_dir == _player_cur_dir and _player_sprite.sprite_frames != null:
		return
	_player_cur_dir = new_dir
	var sfx = Sfx8[new_dir]
	var atlas_name = _player_atlas_base + sfx
	if not _player_dir_frames.has(new_dir):
		var sf = AtlasLibrary.get_sprite_frames("classanim", atlas_name, 8.0, true)
		if sf == null:
			sf = AtlasLibrary.get_sprite_frames("classanim", _player_atlas_base, 8.0, true)
		if sf == null:
			# 若進階職業 DLC 仍在下載中，優雅回退至基礎職業 (男騎士/女騎士)
			var fallback_base = "男騎士" if _char_data.get("gender", "male") == "male" else "女騎士"
			sf = AtlasLibrary.get_sprite_frames("classanim", fallback_base + sfx, 8.0, true)
			if sf == null:
				sf = AtlasLibrary.get_sprite_frames("classanim", fallback_base, 8.0, true)
		if sf != null:
			_player_dir_frames[new_dir] = sf

func _on_dlc_pack_loaded(pack_name: String) -> void:
	if pack_name.contains("classes"):
		_player_dir_frames.clear()
		_swap_player_dir(_player_cur_dir)
		_add_chat_msg("[color=#4ade80]系統：進階職業擴充資源包已載入完畢，外觀已無縫更新！[/color]", "ALL")
	
	var cur_sf = _player_dir_frames.get(new_dir)
	if cur_sf != null and _player_sprite != null:
		var cur_anim = _player_sprite.animation if _player_sprite.animation != StringName() else "sword1_idle"
		var cur_frame = _player_sprite.frame
		var is_playing = _player_sprite.is_playing()
		_player_sprite.sprite_frames = cur_sf
		_player_sprite.centered = false
		_player_sprite.flip_h = false # 絕對不水平翻轉，呈現純正 8 方向姿態
		
		var bounds = AtlasLibrary.get_content_bounds("classanim", atlas_name, cur_anim)
		if bounds["bottom"] > 0.0:
			_player_sprite.offset = Vector2(-bounds["cx"], -bounds["bottom"])
		
		if cur_sf.has_animation(cur_anim):
			_player_sprite.animation = cur_anim
			_player_sprite.frame = min(cur_frame, cur_sf.get_frame_count(cur_anim) - 1)
			if is_playing:
				_player_sprite.play()
		else:
			_play_player_anim("idle")

func _vector_to_dir8(dir: Vector2) -> int:
	var angle = roundi(atan2(dir.y, dir.x) * 4.0 / PI)
	match angle:
		0: return 3
		1: return 4
		2: return 5
		3: return 6
		-4, 4: return 7
		-3: return 0
		-2: return 1
		-1: return 2
		_: return 6

# ----------------- 怪物生成與錨定平滑處理 -----------------
func _setup_mobs() -> void:
	var spawn_configs = [
		{"name": "狼人",     "atlas": "mob_1110", "hp": 120, "exp": 45,  "adena": 120, "dmg": 12, "spd": 85.0, "pos": Vector2(1200, 460)},
		{"name": "妖魔",     "atlas": "mob_56",   "hp": 80,  "exp": 25,  "adena": 60,  "dmg": 8,  "spd": 75.0, "pos": Vector2(1420, 480)},
		{"name": "妖魔鬥士", "atlas": "mob_94",   "hp": 160, "exp": 65,  "adena": 180, "dmg": 16, "spd": 80.0, "pos": Vector2(1360, 620)},
		{"name": "妖魔弓箭手","atlas": "mob_57",   "hp": 70,  "exp": 35,  "adena": 80,  "dmg": 10, "spd": 70.0, "pos": Vector2(1450, 550)},
		{"name": "哥布林",   "atlas": "mob_1022", "hp": 50,  "exp": 15,  "adena": 35,  "dmg": 6,  "spd": 70.0, "pos": Vector2(1250, 580)},
		{"name": "石頭高崙", "atlas": "mob_49",   "hp": 250, "exp": 110, "adena": 300, "dmg": 22, "spd": 50.0, "pos": Vector2(1150, 410)},
		{"name": "夏洛伯",   "atlas": "mob_95",   "hp": 180, "exp": 85,  "adena": 220, "dmg": 18, "spd": 105.0,"pos": Vector2(1120, 520)},
		{"name": "侏儒",     "atlas": "mob_54",   "hp": 65,  "exp": 20,  "adena": 50,  "dmg": 7,  "spd": 65.0, "pos": Vector2(1380, 430)},
		{"name": "骷髏",     "atlas": "mob_30",   "hp": 90,  "exp": 30,  "adena": 90,  "dmg": 11, "spd": 75.0, "pos": Vector2(1280, 510)},
		{"name": "史萊姆",   "atlas": "mob_31",   "hp": 40,  "exp": 10,  "adena": 20,  "dmg": 4,  "spd": 60.0, "pos": Vector2(1220, 540)}
	]
	
	for cfg in spawn_configs:
		_spawn_single_mob(cfg)

func _spawn_single_mob(tpl: Dictionary) -> void:
	var pos = tpl["pos"]
	var atlas_name = tpl["atlas"]
	var mob_name = tpl["name"]
	
	var mob_node = Node2D.new()
	mob_node.position = pos
	add_child(mob_node)
	
	var shadow = ColorRect.new()
	shadow.color = Color(0, 0, 0, 0.45)
	shadow.size = Vector2(28, 12)
	shadow.position = Vector2(-14, -6)
	mob_node.add_child(shadow)
	
	var spr = AtlasLibrary.make_sprite("anim", atlas_name, "d5/idle", 8.0, true)
	if spr != null:
		mob_node.add_child(spr)
	
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

func _play_mob_anim(mob: Dictionary, act: String) -> void:
	if mob["sprite"] == null or mob["sprite"].sprite_frames == null:
		return
	var sf = mob["sprite"].sprite_frames
	var h = mob.get("heading", 5)
	var anim_name = "d%d/%s" % [h, act]
	if not sf.has_animation(anim_name):
		anim_name = "d0/%s" % act
	
	if sf.has_animation(anim_name):
		if mob["sprite"].animation != anim_name or not mob["sprite"].is_playing():
			var bounds = AtlasLibrary.get_content_bounds("anim", mob["atlas"], anim_name)
			if bounds["bottom"] > 0.0:
				mob["sprite"].offset = Vector2(-bounds["cx"], -bounds["bottom"])
			mob["sprite"].play(anim_name)
	else:
		for a in sf.get_animation_names():
			if a.contains(act):
				if mob["sprite"].animation != a or not mob["sprite"].is_playing():
					mob["sprite"].play(a)
				return

# ----------------- 經典天堂 UI 與四大互動面板 -----------------
func _setup_dialogs() -> void:
	# 1. 裝備面板
	var eq_script = load("res://scripts/ui/equipment_dialog.gd")
	_dlg_equipment = Control.new()
	_dlg_equipment.set_script(eq_script)
	_dlg_equipment.init_dialog(_char_data)
	_dlg_equipment.visible = false
	_hud_layer.add_child(_dlg_equipment)
	
	# 2. 技能面板
	var sk_script = load("res://scripts/ui/skills_dialog.gd")
	_dlg_skills = Control.new()
	_dlg_skills.set_script(sk_script)
	_dlg_skills.init_dialog(_skills_list)
	_dlg_skills.visible = false
	_dlg_skills.skill_cast.connect(_on_skill_cast)
	_hud_layer.add_child(_dlg_skills)
	
	# 3. 道具面板
	var inv_script = load("res://scripts/ui/inventory_dialog.gd")
	_dlg_inventory = Control.new()
	_dlg_inventory.set_script(inv_script)
	_dlg_inventory.init_dialog(_inventory_items)
	_dlg_inventory.visible = false
	_dlg_inventory.item_used.connect(_on_item_used)
	_hud_layer.add_child(_dlg_inventory)
	
	# 4. 任務面板
	var q_script = load("res://scripts/ui/quest_dialog.gd")
	_dlg_quest = Control.new()
	_dlg_quest.set_script(q_script)
	_dlg_quest.init_dialog()
	_dlg_quest.visible = false
	_dlg_quest.quest_reward_claimed.connect(_on_quest_reward_claimed)
	_hud_layer.add_child(_dlg_quest)
	
	# 5. 開房連線面板
	var rm_dlg_script = load("res://scripts/ui/room_dialog.gd")
	_dlg_room = Control.new()
	_dlg_room.set_script(rm_dlg_script)
	_dlg_room.init_dialog(_room_manager)
	_dlg_room.visible = false
	_dlg_room.create_room_requested.connect(func():
		var r_code = _room_manager.create_room()
		_add_chat_msg("[color=#fbbf24]成功創建房間！房號為【%s】，已複製房號請發給隊友加入！[/color]" % r_code, "PARTY")
	)
	_dlg_room.join_room_requested.connect(func(code: String):
		if code.is_empty():
			_add_chat_msg("[color=#f87171]請輸入 4 碼房號！[/color]", "PARTY")
			return
		_room_manager.join_room(code)
	)
	_dlg_room.leave_room_requested.connect(func():
		_room_manager.leave_room()
	)
	_hud_layer.add_child(_dlg_room)

func _setup_hud() -> void:
	_hud_layer = CanvasLayer.new()
	add_child(_hud_layer)
	
	var vp_size = get_viewport_rect().size
	
	# 地名標籤
	var area_lbl = Label.new()
	area_lbl.text = "說話之島 (安全區)"
	area_lbl.position = Vector2(16, vp_size.y - 150)
	area_lbl.add_theme_color_override("font_color", Color("#6fa8dc"))
	area_lbl.add_theme_color_override("font_shadow_color", Color("#101c2e"))
	area_lbl.add_theme_constant_override("shadow_offset_x", 2)
	area_lbl.add_theme_constant_override("shadow_offset_y", 2)
	area_lbl.add_theme_font_size_override("font_size", 13)
	_hud_layer.add_child(area_lbl)
	
	# 左側面板 (rn_left_panel.png)
	var left_panel = TextureRect.new()
	left_panel.texture = load("res://assets/ui/rn_left_panel.png")
	left_panel.position = Vector2(0, vp_size.y - 127)
	left_panel.size = Vector2(138, 127)
	left_panel.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(left_panel)
	
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
	
	# 右側小地圖與巨集面板
	var mini_x = vp_size.x - 315
	var panel_y = vp_size.y - 134
	
	var minimap_base = TextureRect.new()
	minimap_base.texture = load("res://assets/ui/rn_minimap_base.png")
	minimap_base.position = Vector2(mini_x + 12, panel_y + 29)
	minimap_base.size = Vector2(135, 101)
	minimap_base.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(minimap_base)
	
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
	
	var macro_x = vp_size.x - 167
	var macro_panel = TextureRect.new()
	macro_panel.texture = load("res://assets/ui/rn_macro_panel.png")
	macro_panel.position = Vector2(macro_x, panel_y)
	macro_panel.size = Vector2(167, 134)
	macro_panel.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hud_layer.add_child(macro_panel)
	
	# 巨集面板上的四大互動面板按鈕 (裝備、道具、技能、任務、選單)
	var btn_configs = [
		{"name": "裝備", "action": func(): _toggle_dialog(_dlg_equipment)},
		{"name": "道具", "action": func(): _toggle_dialog(_dlg_inventory)},
		{"name": "技能", "action": func(): _toggle_dialog(_dlg_skills)},
		{"name": "任務", "action": func(): _toggle_dialog(_dlg_quest)},
		{"name": "選單", "action": _on_return_pressed}
	]
	for bi in range(btn_configs.size()):
		var cfg = btn_configs[bi]
		var m_btn = Button.new()
		m_btn.text = cfg["name"]
		m_btn.position = Vector2(macro_x + 20 + (bi % 2) * 68, panel_y + 35 + int(bi / 2) * 32)
		m_btn.size = Vector2(62, 26)
		m_btn.focus_mode = Control.FOCUS_NONE
		m_btn.add_theme_font_size_override("font_size", 10)
		m_btn.add_theme_color_override("font_color", GOLD)
		m_btn.pressed.connect(cfg["action"])
		_hud_layer.add_child(m_btn)
	
	# 中央對話框
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
	
	# 對話框分頁 Tabs (全部 / 隊伍聊天 / 掉落/經驗)
	var tabs_box = HBoxContainer.new()
	tabs_box.position = Vector2(chat_x + 14, chat_y + 12)
	tabs_box.size = Vector2(chat_w - 28, 18)
	_hud_layer.add_child(tabs_box)
	
	var tab_defs = [
		{"key": "ALL", "name": "全部"},
		{"key": "PARTY", "name": "隊伍聊天"},
		{"key": "DROP_EXP", "name": "掉落/經驗"}
	]
	for td in tab_defs:
		var tb = Button.new()
		tb.text = td["name"]
		tb.focus_mode = Control.FOCUS_NONE
		tb.add_theme_font_size_override("font_size", 9)
		tb.pressed.connect(func(): _switch_chat_tab(td["key"]))
		tabs_box.add_child(tb)
		_tab_buttons[td["key"]] = tb
	_update_chat_tab_colors()
	
	# 聊天 RichTextLabel
	_chat_box = RichTextLabel.new()
	_chat_box.position = Vector2(chat_x + 16, chat_y + 32)
	_chat_box.size = Vector2(chat_w - 32, 86)
	_chat_box.bbcode_enabled = true
	_chat_box.scroll_following = true
	_chat_box.add_theme_font_size_override("normal_font_size", 9)
	_hud_layer.add_child(_chat_box)
	
	# 經典血魔條 (334x34 可拖曳)
	_hpmp_bar_ctrl = Control.new()
	_hpmp_bar_ctrl.size = Vector2(334, 34)
	_hpmp_bar_ctrl.position = Vector2(chat_x + (chat_w - 334) * 0.5, chat_y - 40)
	_hpmp_bar_ctrl.mouse_filter = Control.MOUSE_FILTER_STOP
	_hpmp_bar_ctrl.gui_input.connect(_on_hpmp_gui_input)
	_hud_layer.add_child(_hpmp_bar_ctrl)
	
	var hpmp_bg = TextureRect.new()
	hpmp_bg.texture = load("res://assets/ui/hpmp_frame.png")
	hpmp_bg.size = Vector2(334, 34)
	hpmp_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hpmp_bar_ctrl.add_child(hpmp_bg)
	
	_hp_fill_rect = TextureRect.new()
	_hp_fill_rect.texture = load("res://assets/ui/hpmp_hp_fill.png")
	_hp_fill_rect.position = Vector2(8, 9)
	_hp_fill_rect.size = Vector2(142 * clamp(_player_hp / _player_max_hp, 0.0, 1.0), 14)
	_hp_fill_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hp_fill_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hpmp_bar_ctrl.add_child(_hp_fill_rect)
	
	_hp_txt_label = Label.new()
	_hp_txt_label.text = "%d / %d" % [_player_hp, _player_max_hp]
	_hp_txt_label.position = Vector2(8, 8)
	_hp_txt_label.size = Vector2(142, 14)
	_hp_txt_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_hp_txt_label.add_theme_font_size_override("font_size", 10)
	_hp_txt_label.add_theme_color_override("font_color", Color.WHITE)
	_hp_txt_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hpmp_bar_ctrl.add_child(_hp_txt_label)
	
	_mp_fill_rect = TextureRect.new()
	_mp_fill_rect.texture = load("res://assets/ui/hpmp_mp_fill.png")
	_mp_fill_rect.position = Vector2(184, 9)
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
	
	# 可拖曳聊天輸入框
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
	
	# 右上角功能按鈕組 (返回選單 + 連線開房)
	var top_box = HBoxContainer.new()
	top_box.position = Vector2(vp_size.x - 225, 12)
	top_box.size = Vector2(210, 28)
	top_box.add_theme_constant_override("separation", 8)
	_hud_layer.add_child(top_box)
	
	var btn_room = Button.new()
	btn_room.text = "🌐 連線開房"
	btn_room.focus_mode = Control.FOCUS_NONE
	btn_room.add_theme_color_override("font_color", Color("#38bdf8"))
	btn_room.add_theme_font_size_override("font_size", 11)
	btn_room.pressed.connect(func(): _toggle_dialog(_dlg_room))
	top_box.add_child(btn_room)
	
	var btn_menu = Button.new()
	btn_menu.text = "返回角色選單"
	btn_menu.focus_mode = Control.FOCUS_NONE
	btn_menu.add_theme_color_override("font_color", GOLD)
	btn_menu.add_theme_font_size_override("font_size", 11)
	btn_menu.pressed.connect(_on_return_pressed)
	top_box.add_child(btn_menu)

func _toggle_dialog(dlg: Control) -> void:
	if dlg == null:
		return
	dlg.visible = not dlg.visible
	if dlg.visible:
		dlg.move_to_front()

# ----------------- 分頁聊天與訊息過濾 -----------------
func _switch_chat_tab(tab_key: String) -> void:
	_active_chat_tab = tab_key
	_update_chat_tab_colors()
	_refresh_chat_display()

func _update_chat_tab_colors() -> void:
	for k in _tab_buttons.keys():
		var b = _tab_buttons[k]
		if k == _active_chat_tab:
			b.add_theme_color_override("font_color", GOLD)
		else:
			b.add_theme_color_override("font_color", Color("#94a3b8"))

func _add_chat_msg(msg: String, category: String = "ALL") -> void:
	_chat_history.append({"text": msg, "cat": category})
	if _active_chat_tab == "ALL" or _active_chat_tab == category or category == "ALL":
		if _chat_box != null:
			_chat_box.append_text(msg + "\n")

func _refresh_chat_display() -> void:
	if _chat_box == null:
		return
	_chat_box.text = ""
	for m in _chat_history:
		var c = m["cat"]
		if _active_chat_tab == "ALL" or _active_chat_tab == c or c == "ALL":
			_chat_box.append_text(m["text"] + "\n")

# ----------------- 技能施放與道具使用回調 -----------------
func _on_skill_cast(sk: Dictionary) -> void:
	var mp_cost = sk.get("mp", 10)
	if _player_mp < mp_cost:
		_add_chat_msg("[color=#f87171]魔力不足！無法施放【%s】。[/color]" % sk["name"], "ALL")
		_show_damage_float(_player_pos + Vector2(0, -40), "MP不足", Color("#f87171"))
		return
		
	_player_mp -= mp_cost
	_play_player_anim("attack")
	_show_damage_float(_player_pos + Vector2(0, -60), "✦ " + sk["name"], Color("#38bdf8"))
	_add_chat_msg("[color=#38bdf8]你施放了魔法【%s】！[/color]" % sk["name"], "ALL")
	
	var s_type = sk.get("type", "buff")
	if s_type == "heal":
		var amt = sk.get("amount", 50)
		_player_hp = min(_player_max_hp, _player_hp + amt)
		_show_damage_float(_player_pos + Vector2(0, -40), "+%d HP" % amt, Color("#4ade80"))
	elif s_type == "attack":
		if not _target_mob.is_empty() and not _target_mob["is_dead"]:
			var dmg = sk.get("dmg", 30) + randi_range(5, 15)
			_target_mob["hp"] -= dmg
			_play_mob_anim(_target_mob, "hurt")
			_show_damage_float(_target_mob["pos"] + Vector2(0, -40), str(dmg), Color("#f43f5e"))
			_add_chat_msg("[color=#f43f5e]【%s】對 [%s] 造成了 %d 點魔法傷害！[/color]" % [sk["name"], _target_mob["name"], dmg], "ALL")
			if _target_mob["hp"] <= 0:
				_kill_mob(_target_mob)
		else:
			_add_chat_msg("[color=#94a3b8]（未鎖定目標，魔法能量釋放至空中）[/color]", "ALL")
	elif s_type == "teleport":
		_teleport_random()

func _on_item_used(item: Dictionary) -> void:
	var t = item.get("type", "")
	item["count"] = max(0, item.get("count", 1) - 1)
	if _dlg_inventory != null and _dlg_inventory.has_method("update_items"):
		_dlg_inventory.update_items(_inventory_items)
		
	match t:
		"heal_hp":
			var val = item.get("val", 45)
			_player_hp = min(_player_max_hp, _player_hp + val)
			_show_damage_float(_player_pos + Vector2(0, -40), "+%d HP" % val, Color("#4ade80"))
			_add_chat_msg("[color=#4ade80]使用了【%s】，恢復了 %d 點體力。[/color]" % [item["name"], val], "ALL")
		"buff_brave":
			_show_damage_float(_player_pos + Vector2(0, -50), "★ 勇敢狀態！", Color("#fbbf24"))
			_add_chat_msg("[color=#fbbf24]喝下了【勇敢藥水】，全身充滿了力量！[/color]", "ALL")
		"buff_green":
			_show_damage_float(_player_pos + Vector2(0, -50), "⚡ 加速狀態！", Color("#60a5fa"))
			_add_chat_msg("[color=#60a5fa]喝下了【加速藥水】，腳步變得無比輕快！[/color]", "ALL")
		"teleport_home":
			_player_pos = Vector2(1318, 517)
			_player_target_pos = _player_pos
			_player_node.position = _player_pos
			_show_damage_float(_player_pos + Vector2(0, -40), "返抵村莊", GOLD)
			_add_chat_msg("[color=#f1d47a]使用了【傳送回家的卷軸】，回到了說話之島村莊中心。[/color]", "ALL")
		"teleport_rand":
			_teleport_random()

func _on_quest_reward_claimed(q: Dictionary) -> void:
	var exp_g = q.get("reward_exp", 500)
	var adena_g = q.get("reward_adena", 2000)
	_player_exp += exp_g
	_player_adena += adena_g
	_show_damage_float(_player_pos + Vector2(0, -60), "+%d Exp  +%d 天幣" % [exp_g, adena_g], GOLD)
	_add_chat_msg("[color=#facc15]★ 恭喜完成任務【%s】！獲得經驗值 %d 與天幣 %d。[/color]" % [q["title"], exp_g, adena_g], "DROP_EXP")

func _teleport_random() -> void:
	var rx = randf_range(1180, 1420)
	var ry = randf_range(440, 600)
	_player_pos = Vector2(rx, ry)
	_player_target_pos = _player_pos
	_player_node.position = _player_pos
	_show_damage_float(_player_pos + Vector2(0, -50), "✨ 空間傳送", Color("#c084fc"))
	_add_chat_msg("[color=#c084fc]使用了空間瞬移，已轉移至全新位置！[/color]", "ALL")

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
		_add_chat_msg("[color=#4ade80]系統：治癒術！生命與魔力已完全恢復。[/color]", "ALL")
	elif msg == "/adena":
		_player_adena += 10000
		_add_chat_msg("[color=#f1d47a]系統：獲得天幣 10,000！[/color]", "DROP_EXP")
	else:
		var p_name = _char_data.get("name", "勇者")
		_add_chat_msg("[color=#e2e8f0][b]%s[/b]： %s[/color]" % [p_name, msg], "PARTY")
		if _room_manager != null and _room_manager.is_connected:
			_room_manager.send_chat(p_name, msg)
		_show_chat_bubble(_player_node, msg)

# ----------------- 遊戲循環：走位、戰鬥、AI -----------------
func _process(delta: float) -> void:
	_update_player(delta)
	_update_mobs(delta)
	_update_ground_drops(delta)
	_update_remote_players(delta)
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
		_player_pos.x = clamp(_player_pos.x, 30.0, _map_width - 30.0)
		_player_pos.y = clamp(_player_pos.y, 30.0, _map_height - 30.0)
		_player_node.position = _player_pos
		
		var d8 = _vector_to_dir8(dir)
		_swap_player_dir(d8)
		_play_player_anim("walk")
		
		if _room_manager != null and _room_manager.is_connected:
			_room_manager.send_move(_player_pos, d8, true)
	else:
		if not _player_is_attacking:
			_play_player_anim("idle")
			if _room_manager != null and _room_manager.is_connected:
				_room_manager.send_move(_player_pos, _player_cur_dir, false)

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
	_add_chat_msg("[color=#e2e8f0]你對 [%s] 造成了 %d 點傷害！[/color]" % [mob["name"], dmg], "ALL")
	
	_play_mob_anim(mob, "hurt")
	
	if _room_manager != null and _room_manager.is_connected:
		_room_manager.send_attack(mob["pos"])
	
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
	
	_add_chat_msg("[color=#fbbf24]你擊敗了 [%s]！獲得 %d 經驗值。[/color]" % [mob["name"], g_exp], "DROP_EXP")
	_show_damage_float(mob["pos"] + Vector2(0, -60), "+%d Exp" % g_exp, Color("#60a5fa"))
	
	# 記錄任務殺怪
	if _dlg_quest != null and _dlg_quest.has_method("record_kill"):
		_dlg_quest.record_kill(mob["name"])
	
	# 地面生成金幣
	_spawn_ground_drop(mob["pos"], "adena", g_adena)
	
	# 升級檢查
	if _player_exp >= _player_exp_max:
		_player_exp -= _player_exp_max
		_player_exp_max = int(_player_exp_max * 1.5)
		_player_level += 1
		_player_max_hp += 20
		_player_hp = _player_max_hp
		_add_chat_msg("[color=#4ade80]★ 恭喜升級！等級提升至 Lv.%d，HP 上限大幅增加！[/color]" % _player_level, "ALL")
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
			_player_adena += d["amount"]
			_add_chat_msg("[color=#facc15]拾取了地面上的 %d 天幣。[/color]" % d["amount"], "DROP_EXP")
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
					m["is_dead"] = false
					m["hp"] = m["max_hp"]
					m["pos"] = m["origin_pos"] + Vector2(randf_range(-40, 40), randf_range(-40, 40))
					m["node"].position = m["pos"]
					m["node"].modulate.a = 1.0
					_play_mob_anim(m, "idle")
			continue
		
		if m["hp_fill"] != null:
			var ratio = clamp(m["hp"] / m["max_hp"], 0.0, 1.0)
			m["hp_fill"].size.x = 30.0 * ratio
		
		var dist_to_player = m["pos"].distance_to(_player_pos)
		if dist_to_player < 150.0:
			if dist_to_player > 45.0:
				var dir = (_player_pos - m["pos"]).normalized()
				m["pos"] += dir * (m["spd"] * delta)
				m["node"].position = m["pos"]
				m["heading"] = _vector_to_dir8(dir)
				_play_mob_anim(m, "walk")
			else:
				m["atk_cd"] -= delta
				if m["atk_cd"] <= 0.0:
					m["atk_cd"] = 1.2
					_play_mob_anim(m, "attack")
					_mob_attack_player(m)
		else:
			if randf() < 0.008:
				m["target_pos"] = m["origin_pos"] + Vector2(randf_range(-60, 60), randf_range(-60, 60))
			var to_tp = m["target_pos"] - m["pos"]
			if to_tp.length() > 4.0:
				var d_step = min(m["spd"] * 0.45 * delta, to_tp.length())
				var dir = to_tp.normalized()
				m["pos"] += dir * d_step
				m["node"].position = m["pos"]
				m["heading"] = _vector_to_dir8(dir)
				_play_mob_anim(m, "walk")
			else:
				_play_mob_anim(m, "idle")

func _mob_attack_player(m: Dictionary) -> void:
	var dmg = max(1, m["dmg"] - int(_char_data.get("ac", 10) * 0.25))
	_player_hp = max(0.0, _player_hp - dmg)
	_show_damage_float(_player_pos + Vector2(0, -50), str(dmg), Color("#ef4444"))
	_add_chat_msg("[color=#f87171][%s] 對你造成了 %d 點傷害！[/color]" % [m["name"], dmg], "ALL")

func _update_remote_players(delta: float) -> void:
	for p in _remote_players.values():
		var node = p["node"]
		if node == null:
			continue
		var to_tgt = p["target_pos"] - p["pos"]
		if to_tgt.length() > 2.0:
			p["pos"] += to_tgt.normalized() * min(_player_speed * delta, to_tgt.length())
			node.position = p["pos"]
			if p["sprite"] != null and not p["sprite"].animation.contains("walk"):
				p["sprite"].play("sword1_walk")
		else:
			if p["sprite"] != null and not p["sprite"].animation.contains("idle"):
				p["sprite"].play("sword1_idle")

# ----------------- HUD 與雷達同步 -----------------
func _update_hud() -> void:
	if _hp_fill_rect != null:
		_hp_fill_rect.size.x = 142.0 * clamp(_player_hp / _player_max_hp, 0.0, 1.0)
	if _hp_txt_label != null:
		_hp_txt_label.text = "%d / %d" % [int(_player_hp), int(_player_max_hp)]
	if _mp_fill_rect != null:
		_mp_fill_rect.size.x = 142.0 * clamp(_player_mp / _player_max_mp, 0.0, 1.0)
	if _mp_txt_label != null:
		_mp_txt_label.text = "%d / %d" % [int(_player_mp), int(_player_max_mp)]
	if _adena_label != null:
		_adena_label.text = str(_player_adena)
	if _level_label != null:
		_level_label.text = "Lv.%d" % _player_level
	if _exp_pct_label != null:
		_exp_pct_label.text = "%d%%" % int((_player_exp / _player_exp_max) * 100.0)
	if _exp_fill_rect != null:
		_exp_fill_rect.size.x = 98.0 * clamp(_player_exp / _player_exp_max, 0.0, 1.0)
	if _player_hp_bar != null:
		_player_hp_bar.size.x = 36.0 * clamp(_player_hp / _player_max_hp, 0.0, 1.0)
	
	_update_radar()

func _update_radar() -> void:
	if _minimap_radar == null:
		return
	for c in _minimap_radar.get_children():
		c.queue_free()
	
	var r_w = 135.0
	var r_h = 101.0
	var center = Vector2(r_w * 0.5, r_h * 0.5)
	
	# 玩家自己 (綠色光點)
	var p_dot = ColorRect.new()
	p_dot.color = Color("#22c55e")
	p_dot.size = Vector2(4, 4)
	p_dot.position = center - Vector2(2, 2)
	_minimap_radar.add_child(p_dot)
	
	# 怪物 (紅色光點)
	for m in _mobs:
		if m["is_dead"]:
			continue
		var offset = (m["pos"] - _player_pos) * 0.12
		var dot_pos = center + offset
		if dot_pos.x >= 0 and dot_pos.x <= r_w and dot_pos.y >= 0 and dot_pos.y <= r_h:
			var m_dot = ColorRect.new()
			m_dot.color = Color("#ef4444")
			m_dot.size = Vector2(3, 3)
			m_dot.position = dot_pos - Vector2(1.5, 1.5)
			_minimap_radar.add_child(m_dot)
	
	# 遠端隊友 (藍色光點)
	for rp in _remote_players.values():
		var offset = (rp["pos"] - _player_pos) * 0.12
		var dot_pos = center + offset
		if dot_pos.x >= 0 and dot_pos.x <= r_w and dot_pos.y >= 0 and dot_pos.y <= r_h:
			var t_dot = ColorRect.new()
			t_dot.color = Color("#38bdf8")
			t_dot.size = Vector2(4, 4)
			t_dot.position = dot_pos - Vector2(2, 2)
			_minimap_radar.add_child(t_dot)

func _show_damage_float(world_pos: Vector2, text: String, color: Color) -> void:
	var lbl = Label.new()
	lbl.text = text
	lbl.position = world_pos + Vector2(-30, -20)
	lbl.size = Vector2(60, 20)
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_color_override("font_color", color)
	lbl.add_theme_color_override("font_shadow_color", Color.BLACK)
	lbl.add_theme_constant_override("shadow_offset_x", 1)
	lbl.add_theme_constant_override("shadow_offset_y", 1)
	lbl.add_theme_font_size_override("font_size", 11)
	add_child(lbl)
	
	var tw = create_tween()
	tw.tween_property(lbl, "position:y", world_pos.y - 45, 0.55).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(lbl, "modulate:a", 0.0, 0.55)
	tw.tween_callback(lbl.queue_free)

func _on_return_pressed() -> void:
	return_to_menu.emit()
