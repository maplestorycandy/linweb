extends Control

# 開房連線視窗 (RoomDialog)
signal closed
signal create_room_requested
signal join_room_requested(room_code: String)
signal leave_room_requested

var _is_dragging: bool = false
var _drag_offset: Vector2 = Vector2.ZERO
var _room_manager: Node = null

var _status_lbl: Label
var _room_input: LineEdit
var _btn_create: Button
var _btn_join: Button
var _btn_leave: Button
var _peer_list_lbl: Label

func init_dialog(rm: Node) -> void:
	_room_manager = rm
	_setup_ui()
	_update_state()

func update_display() -> void:
	_update_state()

func _setup_ui() -> void:
	size = Vector2(320, 310)
	position = Vector2(240, 100)
	
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
	title_lbl.text = "🌐 多人組隊開房連線"
	title_lbl.position = Vector2(10, 4)
	title_lbl.add_theme_font_size_override("font_size", 12)
	title_lbl.add_theme_color_override("font_color", Color("#38bdf8"))
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
	
	# 連線狀態提示
	_status_lbl = Label.new()
	_status_lbl.position = Vector2(16, 40)
	_status_lbl.size = Vector2(288, 22)
	_status_lbl.add_theme_font_size_override("font_size", 11)
	_status_lbl.add_theme_color_override("font_color", Color("#e2e8f0"))
	add_child(_status_lbl)
	
	# 創建房間區塊
	var create_box = HBoxContainer.new()
	create_box.position = Vector2(16, 72)
	create_box.size = Vector2(288, 30)
	create_box.add_theme_constant_override("separation", 10)
	add_child(create_box)
	
	_btn_create = Button.new()
	_btn_create.text = "➕ 創建新房間 (房主)"
	_btn_create.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_btn_create.focus_mode = Control.FOCUS_NONE
	_btn_create.add_theme_font_size_override("font_size", 11)
	_btn_create.add_theme_color_override("font_color", Color("#fbbf24"))
	_btn_create.pressed.connect(func():
		create_room_requested.emit()
		_update_state()
	)
	create_box.add_child(_btn_create)
	
	# 分隔線
	var sep = HSeparator.new()
	sep.position = Vector2(16, 114)
	sep.size = Vector2(288, 10)
	add_child(sep)
	
	# 加入房間區塊
	var join_lbl = Label.new()
	join_lbl.text = "或輸入 4 碼房號加入隊友房間："
	join_lbl.position = Vector2(16, 130)
	join_lbl.add_theme_font_size_override("font_size", 10)
	join_lbl.add_theme_color_override("font_color", Color("#94a3b8"))
	add_child(join_lbl)
	
	var join_box = HBoxContainer.new()
	join_box.position = Vector2(16, 154)
	join_box.size = Vector2(288, 30)
	join_box.add_theme_constant_override("separation", 8)
	add_child(join_box)
	
	_room_input = LineEdit.new()
	_room_input.placeholder_text = "請輸入房號 (例如 8888)"
	_room_input.max_length = 6
	_room_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_room_input.add_theme_font_size_override("font_size", 11)
	join_box.add_child(_room_input)
	
	_btn_join = Button.new()
	_btn_join.text = "加入房間"
	_btn_join.custom_minimum_size = Vector2(80, 0)
	_btn_join.focus_mode = Control.FOCUS_NONE
	_btn_join.add_theme_font_size_override("font_size", 11)
	_btn_join.add_theme_color_override("font_color", Color("#4ade80"))
	_btn_join.pressed.connect(func():
		join_room_requested.emit(_room_input.text.strip_edges())
		_update_state()
	)
	join_box.add_child(_btn_join)
	
	# 房間內玩家名單
	_peer_list_lbl = Label.new()
	_peer_list_lbl.position = Vector2(16, 196)
	_peer_list_lbl.size = Vector2(288, 60)
	_peer_list_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD
	_peer_list_lbl.add_theme_font_size_override("font_size", 10)
	_peer_list_lbl.add_theme_color_override("font_color", Color("#67e8f9"))
	add_child(_peer_list_lbl)
	
	# 離開房間按鈕
	_btn_leave = Button.new()
	_btn_leave.text = "離開房間 (返回單機)"
	_btn_leave.position = Vector2(16, 266)
	_btn_leave.size = Vector2(288, 28)
	_btn_leave.focus_mode = Control.FOCUS_NONE
	_btn_leave.add_theme_font_size_override("font_size", 11)
	_btn_leave.add_theme_color_override("font_color", Color("#f87171"))
	_btn_leave.pressed.connect(func():
		leave_room_requested.emit()
		_update_state()
	)
	add_child(_btn_leave)

func _update_state() -> void:
	if _room_manager == null:
		return
	if _room_manager.is_connected and not _room_manager.room_id.is_empty():
		var role = "房主" if _room_manager.is_host else "成員"
		_status_lbl.text = "● 已連線房間【%s】(你的身份: %s)" % [_room_manager.room_id, role]
		_status_lbl.add_theme_color_override("font_color", Color("#4ade80"))
		_btn_create.disabled = true
		_btn_join.disabled = true
		_room_input.editable = false
		_btn_leave.visible = true
		
		var peer_names = []
		for p in _room_manager.connected_peers.values():
			peer_names.append(p.get("name", "隊友"))
		if peer_names.is_empty():
			_peer_list_lbl.text = "目前房間成員：只有你 (可將房號 %s 分享給好友加入)" % _room_manager.room_id
		else:
			_peer_list_lbl.text = "房間隊友清單 (%d人)：%s" % [peer_names.size() + 1, ", ".join(peer_names)]
	else:
		_status_lbl.text = "○ 目前處於單機模式，尚未加入房間"
		_status_lbl.add_theme_color_override("font_color", Color("#94a3b8"))
		_btn_create.disabled = false
		_btn_join.disabled = false
		_room_input.editable = true
		_btn_leave.visible = false
		_peer_list_lbl.text = "提示：開房後即可在同張說話之島地圖與好友即時打怪、組隊聊天！"

func _on_title_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_is_dragging = true
			_drag_offset = get_global_mouse_position() - position
		else:
			_is_dragging = false
	elif event is InputEventMouseMotion and _is_dragging:
		position = get_global_mouse_position() - _drag_offset
