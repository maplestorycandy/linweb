extends Control

# 經典天堂技能與魔法視窗 (SkillsDialog)
signal closed
signal skill_cast(skill_data: Dictionary)

var _is_dragging: bool = false
var _drag_offset: Vector2 = Vector2.ZERO
var _skills_list: Array = []

func init_dialog(skills: Array) -> void:
	_skills_list = skills
	_setup_ui()

func _setup_ui() -> void:
	size = Vector2(340, 420)
	position = Vector2(160, 60)
	
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
	title_lbl.text = "★ 魔法與技能 (全技能已掌握)"
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
	
	# 捲動容器
	var scroll = ScrollContainer.new()
	scroll.position = Vector2(10, 36)
	scroll.size = Vector2(320, 372)
	add_child(scroll)
	
	var vbox = VBoxContainer.new()
	vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	vbox.add_theme_constant_override("separation", 6)
	scroll.add_child(vbox)
	
	for sk in _skills_list:
		var row = PanelContainer.new()
		row.custom_minimum_size = Vector2(305, 42)
		vbox.add_child(row)
		
		var h_box = HBoxContainer.new()
		h_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		h_box.add_theme_constant_override("separation", 10)
		row.add_child(h_box)
		
		# 技能圖示
		var icon_rect = TextureRect.new()
		icon_rect.custom_minimum_size = Vector2(32, 32)
		icon_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		var icon_id = sk.get("icon", 2056)
		var icon_path = "res://assets/ui/skills/%d.png" % icon_id
		if ResourceLoader.exists(icon_path):
			icon_rect.texture = load(icon_path)
		h_box.add_child(icon_rect)
		
		# 技能描述
		var desc_box = VBoxContainer.new()
		desc_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		h_box.add_child(desc_box)
		
		var name_lbl = Label.new()
		name_lbl.text = sk.get("name", "神秘魔法")
		name_lbl.add_theme_font_size_override("font_size", 11)
		name_lbl.add_theme_color_override("font_color", Color("#fde68a"))
		desc_box.add_child(name_lbl)
		
		var cost_lbl = Label.new()
		cost_lbl.text = "消耗 MP: %d  |  %s" % [sk.get("mp", 10), sk.get("desc", "")]
		cost_lbl.add_theme_font_size_override("font_size", 9)
		cost_lbl.add_theme_color_override("font_color", Color("#94a3b8"))
		desc_box.add_child(cost_lbl)
		
		# 施放按鈕
		var cast_btn = Button.new()
		cast_btn.text = "施放"
		cast_btn.custom_minimum_size = Vector2(50, 26)
		cast_btn.focus_mode = Control.FOCUS_NONE
		cast_btn.add_theme_font_size_override("font_size", 10)
		cast_btn.add_theme_color_override("font_color", Color("#38bdf8"))
		cast_btn.pressed.connect(func():
			skill_cast.emit(sk)
		)
		h_box.add_child(cast_btn)

func _on_title_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_is_dragging = true
			_drag_offset = get_global_mouse_position() - position
		else:
			_is_dragging = false
	elif event is InputEventMouseMotion and _is_dragging:
		position = get_global_mouse_position() - _drag_offset
