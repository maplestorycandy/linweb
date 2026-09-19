extends Control

# 放置天堂 Web 版 - ARPG 核心遊戲世界 (ArpgEngineScreen 轉譯)
# 完整還原經典天堂等角視角、點擊走位、打怪戰鬥、掉落與 HUD

var player_data: Dictionary = {}
var camera: Camera2D
var world_container: Node2D
var map_sprite: Sprite2D
var player_node: Node2D
var mobs_container: Node2D
var text_container: Node2D

var player_pos: Vector2 = Vector2(1024, 512)
var target_pos: Vector2 = Vector2(1024, 512)
var player_speed: float = 160.0
var target_mob: Node2D = null
var auto_combat: bool = true
var last_attack_time: float = 0.0

var ui_lv: Label
var ui_hp_bar: ProgressBar
var ui_mp_bar: ProgressBar
var ui_hp_txt: Label
var ui_adena_txt: Label
var ui_exp_bar: ProgressBar
var chat_vbox: VBoxContainer

func init_world(data: Dictionary) -> void:
	player_data = data

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	# 世界容器
	world_container = Node2D.new()
	add_child(world_container)
	
	# 載入說話之島地圖
	map_sprite = Sprite2D.new()
	var map_tex = load("res://assets/maps/town_talking_island/l1j-map-0-preview.png")
	if map_tex:
		map_sprite.texture = map_tex
		map_sprite.centered = false
	world_container.add_child(map_sprite)
	
	# 怪物容器
	mobs_container = Node2D.new()
	world_container.add_child(mobs_container)
	
	# 玩家實體
	player_node = Node2D.new()
	player_node.position = player_pos
	world_container.add_child(player_node)
	build_player_graphics()
	
	# 飄字特效容器
	text_container = Node2D.new()
	world_container.add_child(text_container)
	
	# 攝影機
	camera = Camera2D.new()
	camera.position_smoothing_enabled = true
	camera.position_smoothing_speed = 8.0
	player_node.add_child(camera)
	
	# 產生初始野外怪物
	spawn_initial_mobs()
	
	# 建立 HUD 介面
	build_hud()
	
	add_chat("系統：成功載入「說話之島」！", Color("#60a5fa"))
	add_chat("操作提示：點擊地面移動，點擊怪物揮刀攻擊！快捷鍵 [1] 喝水，[A] 切換自動掛機。", Color("#facc15"))

func build_player_graphics() -> void:
	# 角色影子
	var shadow = Polygon2D.new()
	shadow.color = Color(0, 0, 0, 0.45)
	var pts = PackedVector2Array()
	for i in range(12):
		var ang = (i / 12.0) * PI * 2.0
		pts.append(Vector2(cos(ang) * 16, sin(ang) * 8 + 14))
	shadow.polygon = pts
	player_node.add_child(shadow)
	
	# 角色精靈圖
	var sprite = Sprite2D.new()
	var dk_tex = load("res://assets/sprites/dk_walk_composite_8dirs.png")
	if dk_tex:
		sprite.texture = dk_tex
		sprite.region_enabled = true
		sprite.region_rect = Rect2(0, 0, 64, 64)
	player_node.add_child(sprite)
	
	# 頭頂名稱標籤
	var name_lbl = Label.new()
	name_lbl.text = "[%s] %s" % [player_data.get("class_name", "王族"), player_data.get("name", "亞丁勇者")]
	name_lbl.position = Vector2(-70, -36)
	name_lbl.size = Vector2(140, 20)
	name_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_lbl.add_theme_color_override("font_color", Color("#fde047"))
	name_lbl.add_theme_font_size_override("font_size", 11)
	player_node.add_child(name_lbl)

func spawn_initial_mobs() -> void:
	var mob_defs = [
		{"name": "哥布林", "hp": 25, "exp": 15, "color": Color("#22c55e"), "x": 950, "y": 480},
		{"name": "妖魔", "hp": 35, "exp": 22, "color": Color("#eab308"), "x": 1080, "y": 550},
		{"name": "侏儒", "hp": 45, "exp": 30, "color": Color("#f97316"), "x": 1140, "y": 460},
		{"name": "狼人", "hp": 65, "exp": 45, "color": Color("#a855f7"), "x": 900, "y": 580},
		{"name": "歐克", "hp": 55, "exp": 35, "color": Color("#ef4444"), "x": 1050, "y": 400}
	]
	
	for d in mob_defs:
		create_mob_instance(d)

func create_mob_instance(d: Dictionary) -> void:
	var mob = Node2D.new()
	mob.position = Vector2(d["x"], d["y"])
	mob.set_meta("mob_name", d["name"])
	mob.set_meta("hp", d["hp"])
	mob.set_meta("max_hp", d["hp"])
	mob.set_meta("exp", d["exp"])
	mob.set_meta("alive", true)
	
	# 怪物影子
	var shadow = Polygon2D.new()
	shadow.color = Color(0, 0, 0, 0.4)
	var pts = PackedVector2Array()
	for i in range(10):
		var ang = (i / 10.0) * PI * 2.0
		pts.append(Vector2(cos(ang) * 14, sin(ang) * 7 + 10))
	shadow.polygon = pts
	mob.add_child(shadow)
	
	# 怪物圓形本體
	var body = Polygon2D.new()
	body.color = d["color"]
	var b_pts = PackedVector2Array()
	for i in range(12):
		var ang = (i / 12.0) * PI * 2.0
		b_pts.append(Vector2(cos(ang) * 14, sin(ang) * 14))
	body.polygon = b_pts
	mob.add_child(body)
	
	# 怪物名稱
	var lbl = Label.new()
	lbl.text = d["name"]
	lbl.position = Vector2(-50, -28)
	lbl.size = Vector2(100, 16)
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_font_size_override("font_size", 10)
	mob.add_child(lbl)
	
	mobs_container.add_child(mob)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var click_world_pos = world_container.get_local_mouse_position()
		
		# 檢查是否點中怪物
		var clicked_mob: Node2D = null
		for m in mobs_container.get_children():
			if m.get_meta("alive", false) and m.position.distance_to(click_world_pos) < 24.0:
				clicked_mob = m
				break
		
		if clicked_mob:
			target_mob = clicked_mob
			target_pos = clicked_mob.position
			add_chat("鎖定目標：%s" % clicked_mob.get_meta("mob_name"), Color("#eee2c7"))
		else:
			target_mob = null
			target_pos = click_world_pos

func _process(delta: float) -> void:
	update_combat_and_movement(delta)

func update_combat_and_movement(delta: float) -> void:
	# 自動尋怪
	if auto_combat and target_mob == null:
		var nearest: Node2D = null
		var min_dist: float = 380.0
		for m in mobs_container.get_children():
			if m.get_meta("alive", false):
				var d = player_pos.distance_to(m.position)
				if d < min_dist:
					min_dist = d
					nearest = m
		if nearest:
			target_mob = nearest
			target_pos = nearest.position
	
	if target_mob:
		if not target_mob.get_meta("alive", false):
			target_mob = null
		else:
			target_pos = target_mob.position
	
	var dist = player_pos.distance_to(target_pos)
	var stop_dist = 28.0 if target_mob != null else 4.0
	
	if dist > stop_dist:
		var dir = (target_pos - player_pos).normalized()
		player_pos += dir * player_speed * delta
		player_node.position = player_pos
	else:
		# 進入攻擊距離
		if target_mob and target_mob.get_meta("alive", false):
			var now = Time.get_ticks_msec() / 1000.0
			if now - last_attack_time > 0.65:
				last_attack_time = now
				attack_mob(target_mob)

func attack_mob(mob: Node2D) -> void:
	var dmg = randi_range(14, 26)
	var cur_hp = mob.get_meta("hp") - dmg
	mob.set_meta("hp", cur_hp)
	
	spawn_floating_text(mob.position, "-%d" % dmg, Color("#ef4444"))
	add_chat("你對 %s 造成了 %d 點傷害。" % [mob.get_meta("mob_name"), dmg], Color("#f87171"))
	
	# 怪物反擊
	var mob_dmg = randi_range(2, 6)
	player_data["hp"] = max(0, player_data["hp"] - mob_dmg)
	spawn_floating_text(player_pos, "-%d" % mob_dmg, Color("#f97316"))
	update_hud()
	
	if cur_hp <= 0:
		mob.set_meta("alive", false)
		mob.visible = false
		var exp_gain = mob.get_meta("exp")
		var adena_gain = randi_range(20, 60)
		player_data["exp"] += exp_gain
		player_data["adena"] += adena_gain
		
		add_chat("⚔️ 擊敗了 %s！獲得經驗 +%d，天幣 +%d！" % [mob.get_meta("mob_name"), exp_gain, adena_gain], Color("#facc15"))
		
		# 升級判定
		if player_data["exp"] >= player_data["max_exp"]:
			player_data["lv"] += 1
			player_data["exp"] -= player_data["max_exp"]
			player_data["max_exp"] = int(player_data["max_exp"] * 1.5)
			player_data["max_hp"] += 15
			player_data["hp"] = player_data["max_hp"]
			player_data["max_mp"] += 5
			player_data["mp"] = player_data["max_mp"]
			spawn_floating_text(player_pos, "LEVEL UP! Lv.%d" % player_data["lv"], Color("#fde047"))
			add_chat("🌟 恭喜升級！等級提升至 Lv.%d！全狀態全滿！" % player_data["lv"], Color("#4ade80"))
		
		update_hud()
		
		# 5 秒後重生
		get_tree().create_timer(5.0).timeout.connect(func():
			mob.set_meta("hp", mob.get_meta("max_hp"))
			mob.set_meta("alive", true)
			mob.visible = true
			mob.position = Vector2(randf_range(880, 1180), randf_range(380, 600))
			add_chat("%s 重新出現在原野！" % mob.get_meta("mob_name"), Color("#60a5fa"))
		)

func spawn_floating_text(world_p: Vector2, txt: String, col: Color) -> void:
	var lbl = Label.new()
	lbl.text = txt
	lbl.position = world_p + Vector2(-40, -30)
	lbl.size = Vector2(80, 20)
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_color_override("font_color", col)
	lbl.add_theme_font_size_override("font_size", 14)
	text_container.add_child(lbl)
	
	var tween = create_tween()
	tween.tween_property(lbl, "position:y", lbl.position.y - 30, 0.6)
	tween.parallel().tween_property(lbl, "modulate:a", 0.0, 0.6)
	tween.tween_callback(lbl.queue_free)

func build_hud() -> void:
	var hud = Control.new()
	hud.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	hud.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(hud)
	
	# 左上角角色狀態卡
	var card = PanelContainer.new()
	card.position = Vector2(16, 16)
	card.size = Vector2(200, 70)
	hud.add_child(card)
	
	var cvbox = VBoxContainer.new()
	card.add_child(cvbox)
	
	ui_lv = Label.new()
	ui_lv.text = "Lv.%d %s" % [player_data.get("lv", 1), player_data.get("name", "勇者")]
	ui_lv.add_theme_color_override("font_color", Color("#f1d47a"))
	cvbox.add_child(ui_lv)
	
	ui_hp_bar = ProgressBar.new()
	ui_hp_bar.custom_minimum_size = Vector2(180, 10)
	ui_hp_bar.show_percentage = false
	cvbox.add_child(ui_hp_bar)
	
	ui_mp_bar = ProgressBar.new()
	ui_mp_bar.custom_minimum_size = Vector2(180, 8)
	ui_mp_bar.show_percentage = false
	cvbox.add_child(ui_mp_bar)
	
	ui_hp_txt = Label.new()
	ui_hp_txt.add_theme_font_size_override("font_size", 10)
	cvbox.add_child(ui_hp_txt)
	
	# 底部對話框
	var chat_panel = PanelContainer.new()
	chat_panel.position = Vector2(16, 460)
	chat_panel.size = Vector2(400, 120)
	hud.add_child(chat_panel)
	
	chat_vbox = VBoxContainer.new()
	chat_panel.add_child(chat_vbox)
	
	# 經驗條
	ui_exp_bar = ProgressBar.new()
	ui_exp_bar.position = Vector2(0, 594)
	ui_exp_bar.size = Vector2(800, 6)
	ui_exp_bar.show_percentage = false
	hud.add_child(ui_exp_bar)
	
	update_hud()

func update_hud() -> void:
	ui_lv.text = "Lv.%d %s" % [player_data.get("lv", 1), player_data.get("name", "勇者")]
	ui_hp_bar.max_value = player_data["max_hp"]
	ui_hp_bar.value = player_data["hp"]
	ui_mp_bar.max_value = player_data["max_mp"]
	ui_mp_bar.value = player_data["mp"]
	ui_hp_txt.text = "HP: %d/%d ｜ 天幣: %s" % [player_data["hp"], player_data["max_hp"], str(player_data["adena"])]
	ui_exp_bar.max_value = player_data["max_exp"]
	ui_exp_bar.value = player_data["exp"]

func add_chat(txt: String, col: Color = Color.WHITE) -> void:
	var l = Label.new()
	l.text = txt
	l.add_theme_color_override("font_color", col)
	l.add_theme_font_size_override("font_size", 11)
	chat_vbox.add_child(l)
	while chat_vbox.get_child_count() > 6:
		var c = chat_vbox.get_child(0)
		chat_vbox.remove_child(c)
		c.queue_free()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_1:
			use_potion()
		elif event.keycode == KEY_A:
			auto_combat = !auto_combat
			add_chat("已切換為：%s" % ("自動掛機" if auto_combat else "手動操作"), Color("#4ade80"))

func use_potion() -> void:
	if player_data["hp"] >= player_data["max_hp"]: return
	var heal = randi_range(30, 50)
	player_data["hp"] = min(player_data["max_hp"], player_data["hp"] + heal)
	spawn_floating_text(player_pos, "+%d" % heal, Color("#4ade80"))
	add_chat("使用體力恢復劑，恢復了 %d 點 HP。" % heal, Color("#4ade80"))
	update_hud()
