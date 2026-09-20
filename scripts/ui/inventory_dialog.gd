extends Control

# 經典天堂道具背包視窗 (InventoryDialog)
signal closed
signal item_used(item_data: Dictionary)

var _is_dragging: bool = false
var _drag_offset: Vector2 = Vector2.ZERO
var _items: Array = []

func init_dialog(items: Array) -> void:
	_items = items
	_setup_ui()

func update_items(items: Array) -> void:
	_items = items
	for c in get_children():
		c.queue_free()
	_setup_ui()

func _setup_ui() -> void:
	size = Vector2(320, 380)
	position = Vector2(220, 100)
	
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
	title_lbl.text = "🎒 道具背包 (點擊道具使用)"
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
	
	# 道具列表
	var scroll = ScrollContainer.new()
	scroll.position = Vector2(10, 36)
	scroll.size = Vector2(300, 334)
	add_child(scroll)
	
	var vbox = VBoxContainer.new()
	vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	vbox.add_theme_constant_override("separation", 6)
	scroll.add_child(vbox)
	
	for item in _items:
		var row = PanelContainer.new()
		row.custom_minimum_size = Vector2(285, 38)
		vbox.add_child(row)
		
		var h_box = HBoxContainer.new()
		h_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		h_box.add_theme_constant_override("separation", 8)
		row.add_child(h_box)
		
		# 道具小圖示
		var icon_rect = TextureRect.new()
		icon_rect.custom_minimum_size = Vector2(28, 28)
		icon_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		var icon_path = item.get("icon", "res://assets/ui/rn_adena_btn.png")
		if ResourceLoader.exists(icon_path):
			icon_rect.texture = load(icon_path)
		h_box.add_child(icon_rect)
		
		# 道具名稱與數量
		var info_box = VBoxContainer.new()
		info_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		h_box.add_child(info_box)
		
		var name_lbl = Label.new()
		name_lbl.text = "%s  x%d" % [item.get("name", "未知道具"), item.get("count", 1)]
		name_lbl.add_theme_font_size_override("font_size", 11)
		name_lbl.add_theme_color_override("font_color", Color("#fef08a"))
		info_box.add_child(name_lbl)
		
		var desc_lbl = Label.new()
		desc_lbl.text = item.get("desc", "")
		desc_lbl.add_theme_font_size_override("font_size", 9)
		desc_lbl.add_theme_color_override("font_color", Color("#94a3b8"))
		info_box.add_child(desc_lbl)
		
		# 使用按鈕
		if item.get("usable", true):
			var use_btn = Button.new()
			use_btn.text = "使用"
			use_btn.custom_minimum_size = Vector2(46, 24)
			use_btn.focus_mode = Control.FOCUS_NONE
			use_btn.add_theme_font_size_override("font_size", 10)
			use_btn.add_theme_color_override("font_color", Color("#4ade80"))
			use_btn.pressed.connect(func():
				item_used.emit(item)
			)
			h_box.add_child(use_btn)

func _on_title_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_is_dragging = true
			_drag_offset = get_global_mouse_position() - position
		else:
			_is_dragging = false
	elif event is InputEventMouseMotion and _is_dragging:
		position = get_global_mouse_position() - _drag_offset
