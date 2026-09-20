extends Control

# 經典天堂任務視窗 (QuestDialog)
signal closed
signal quest_reward_claimed(quest_data: Dictionary)

var _is_dragging: bool = false
var _drag_offset: Vector2 = Vector2.ZERO
var _quests: Array = [
	{
		"id": "q1",
		"title": "【說話之島防衛】討伐狼人與妖魔",
		"desc": "說話之島周圍出現大量狼人與妖魔，擊敗牠們維持村莊安寧。",
		"target": 5,
		"current": 3,
		"reward_exp": 500,
		"reward_adena": 2000,
		"claimed": false
	},
	{
		"id": "q2",
		"title": "【哥布林掃蕩】奪回被搶奪的金幣",
		"desc": "哥布林搶走了村民的財物，擊退牠們奪回天幣。",
		"target": 3,
		"current": 3,
		"reward_exp": 300,
		"reward_adena": 1500,
		"claimed": false
	},
	{
		"id": "q3",
		"title": "【遠古巨怪】挑戰石頭高崙與夏洛伯",
		"desc": "村莊外圍有強大的高崙與夏洛伯盤據，消滅強敵證明勇氣。",
		"target": 1,
		"current": 1,
		"reward_exp": 1200,
		"reward_adena": 5000,
		"claimed": false
	}
]

func init_dialog() -> void:
	_setup_ui()

func record_kill(mob_name: String) -> void:
	for q in _quests:
		if q["claimed"]:
			continue
		if q["id"] == "q1" and (mob_name.contains("狼人") or mob_name.contains("妖魔")):
			q["current"] = min(q["target"], q["current"] + 1)
		elif q["id"] == "q2" and mob_name.contains("哥布林"):
			q["current"] = min(q["target"], q["current"] + 1)
		elif q["id"] == "q3" and (mob_name.contains("高崙") or mob_name.contains("夏洛伯")):
			q["current"] = min(q["target"], q["current"] + 1)
	_refresh_ui()

func _refresh_ui() -> void:
	for c in get_children():
		c.queue_free()
	_setup_ui()

func _setup_ui() -> void:
	size = Vector2(340, 380)
	position = Vector2(200, 80)
	
	var frame = ColorRect.new()
	frame.color = Color(0.08, 0.1, 0.14, 0.95)
	frame.size = size
	add_child(frame)
	
	var border = ReferenceRect.new()
	border.border_color = Color("#b8860b")
	border.size = size
	border.editor_only = false
	add_child(border)
	
	# 標題列
	var title_bar = ColorRect.new()
	title_bar.color = Color(0.15, 0.18, 0.25, 1.0)
	title_bar.size = Vector2(size.x, 28)
	title_bar.gui_input.connect(_on_title_gui_input)
	add_child(title_bar)
	
	var title_lbl = Label.new()
	title_lbl.text = "📜 冒險任務日誌"
	title_lbl.position = Vector2(10, 4)
	title_lbl.add_theme_font_size_override("font_size", 12)
	title_lbl.add_theme_color_override("font_color", Color("#fbbf24"))
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
	
	# 任務列表
	var scroll = ScrollContainer.new()
	scroll.position = Vector2(10, 36)
	scroll.size = Vector2(320, 334)
	add_child(scroll)
	
	var vbox = VBoxContainer.new()
	vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	vbox.add_theme_constant_override("separation", 10)
	scroll.add_child(vbox)
	
	for q in _quests:
		var panel = PanelContainer.new()
		panel.custom_minimum_size = Vector2(305, 85)
		vbox.add_child(panel)
		
		var q_box = VBoxContainer.new()
		q_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		q_box.add_theme_constant_override("separation", 4)
		panel.add_child(q_box)
		
		var t_lbl = Label.new()
		t_lbl.text = q["title"]
		t_lbl.add_theme_font_size_override("font_size", 11)
		t_lbl.add_theme_color_override("font_color", Color("#fde68a"))
		q_box.add_child(t_lbl)
		
		var d_lbl = Label.new()
		d_lbl.text = q["desc"]
		d_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD
		d_lbl.add_theme_font_size_override("font_size", 9)
		d_lbl.add_theme_color_override("font_color", Color("#94a3b8"))
		q_box.add_child(d_lbl)
		
		var bottom_row = HBoxContainer.new()
		bottom_row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		q_box.add_child(bottom_row)
		
		var prog_lbl = Label.new()
		prog_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		prog_lbl.text = "進度: %d / %d  |  獎勵: +%d Exp, +%d 天幣" % [q["current"], q["target"], q["reward_exp"], q["reward_adena"]]
		prog_lbl.add_theme_font_size_override("font_size", 9)
		prog_lbl.add_theme_color_override("font_color", Color("#67e8f9"))
		bottom_row.add_child(prog_lbl)
		
		var btn = Button.new()
		btn.custom_minimum_size = Vector2(65, 22)
		btn.focus_mode = Control.FOCUS_NONE
		btn.add_theme_font_size_override("font_size", 9)
		
		if q["claimed"]:
			btn.text = "已領取"
			btn.disabled = true
		elif q["current"] >= q["target"]:
			btn.text = "領取獎勵"
			btn.add_theme_color_override("font_color", Color("#4ade80"))
			btn.pressed.connect(func():
				q["claimed"] = true
				quest_reward_claimed.emit(q)
				_refresh_ui()
			)
		else:
			btn.text = "進行中"
			btn.disabled = true
		bottom_row.add_child(btn)

func _on_title_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_is_dragging = true
			_drag_offset = get_global_mouse_position() - position
		else:
			_is_dragging = false
	elif event is InputEventMouseMotion and _is_dragging:
		position = get_global_mouse_position() - _drag_offset
