extends Control

signal new_game_requested(slot: int)
signal load_game_requested(slot: int)

const DESIGN_WIDTH = 640.0
const DESIGN_HEIGHT = 480.0

var _stage: Control
var _selected_slot: int = 1
var _slot_buttons: Array = []

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	# 背景底色
	var bg = ColorRect.new()
	bg.color = Color("#0b0e14")
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	
	# 固定 640x480 的舞台置中
	_stage = Control.new()
	_stage.custom_minimum_size = Vector2(DESIGN_WIDTH, DESIGN_HEIGHT)
	_stage.size = Vector2(DESIGN_WIDTH, DESIGN_HEIGHT)
	_stage.anchors_preset = Control.PRESET_CENTER
	add_child(_stage)
	
	build_ui()

func build_ui() -> void:
	# 遊戲大標題
	var title = Label.new()
	title.text = "⚔️ 經典天堂 · 放置單機版 ⚔️"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.position = Vector2(0, 40)
	title.size = Vector2(DESIGN_WIDTH, 40)
	title.add_theme_color_override("font_color", Color("#f1d47a"))
	title.add_theme_font_size_override("font_size", 24)
	_stage.add_child(title)
	
	# 存檔槽位面板 (Slot 1 ~ Slot 8)
	var panel = PanelContainer.new()
	panel.position = Vector2(120, 100)
	panel.size = Vector2(400, 260)
	_stage.add_child(panel)
	
	var vbox = VBoxContainer.new()
	vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.add_child(vbox)
	
	var slot_label = Label.new()
	slot_label.text = "請選擇角色存檔槽位："
	slot_label.add_theme_color_override("font_color", Color("#eee2c7"))
	vbox.add_child(slot_label)
	
	for i in range(1, 9):
		var btn = Button.new()
		var save_path = "user://save_slot_%d.json" % i
		var has_save = FileAccess.file_exists(save_path)
		if has_save:
			btn.text = "槽位 %d: [已建立角色] 進入冒險" % i
		else:
			btn.text = "槽位 %d: [空白存檔] 創建新角色" % i
		
		var slot_idx = i
		btn.pressed.connect(func(): _select_slot(slot_idx))
		vbox.add_child(btn)
		_slot_buttons.append(btn)
	
	# 底部開始按鈕
	var btn_start = Button.new()
	btn_start.text = "▶ 進入遊戲"
	btn_start.position = Vector2(240, 390)
	btn_start.size = Vector2(160, 44)
	btn_start.add_theme_color_override("font_color", Color("#000"))
	btn_start.pressed.connect(self._on_start_pressed)
	_stage.add_child(btn_start)

func _select_slot(slot: int) -> void:
	_selected_slot = slot
	for i in range(_slot_buttons.size()):
		var b: Button = _slot_buttons[i]
		if i + 1 == slot:
			b.modulate = Color(1.3, 1.3, 0.8)
		else:
			b.modulate = Color(1.0, 1.0, 1.0)

func _on_start_pressed() -> void:
	var save_path = "user://save_slot_%d.json" % _selected_slot
	if FileAccess.file_exists(save_path):
		emit_signal("load_game_requested", _selected_slot)
	else:
		emit_signal("new_game_requested", _selected_slot)
