extends Control

# 放置天堂 Web 版 - 遊戲主控制器 (GameRoot)
# 仿照原版 GameRoot.cs，負責視窗排版、主題字型載入與畫面切換

var _current_screen: Control = null
var _current_slot: int = 1

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_apply_theme()
	show_menu()

func _apply_theme() -> void:
	# 強制載入繁體中文字型 NotoSansTC，解決 WebAssembly 平台缺字 tofu 亂碼問題
	var font_path = "res://assets/fonts/NotoSansTC-VF.ttf"
	if ResourceLoader.exists(font_path):
		var f: FontFile = load(font_path)
		if f != null:
			var th = Theme.new()
			th.default_font = f
			th.default_font_size = 13
			self.theme = th
			print("[GameRoot] 已成功套用全域中文字型：NotoSansTC-VF.ttf")

func show_screen(screen: Control) -> void:
	if _current_screen != null:
		_current_screen.queue_free()
	_current_screen = screen
	add_child(screen)

func show_menu() -> void:
	var menu_script = load("res://scripts/ui/menu_screen.gd")
	var menu = Control.new()
	menu.set_script(menu_script)
	
	menu.start_new_game.connect(func(slot: int):
		_current_slot = slot
		show_create(slot)
	)
	
	menu.load_game.connect(func(slot: int):
		_current_slot = slot
		_load_and_enter_game(slot)
	)
	
	show_screen(menu)

func show_create(slot: int) -> void:
	var create_script = load("res://scripts/ui/create_screen.gd")
	var create = Control.new()
	create.set_script(create_script)
	
	create.character_created.connect(func(char_data: Dictionary):
		# 儲存角色到 Slot
		var p = "user://character_slot_%d.json" % slot
		var f = FileAccess.open(p, FileAccess.WRITE)
		if f != null:
			f.store_string(JSON.stringify(char_data))
		enter_world(char_data, slot)
	)
	
	create.back_to_menu.connect(show_menu)
	show_screen(create)

func _load_and_enter_game(slot: int) -> void:
	var p = "user://character_slot_%d.json" % slot
	if FileAccess.file_exists(p):
		var f = FileAccess.open(p, FileAccess.READ)
		if f != null:
			var parsed = JSON.parse_string(f.get_as_text())
			if typeof(parsed) == TYPE_DICTIONARY:
				enter_world(parsed, slot)
				return
	# 若沒有存檔，創立預設角色
	var default_char = {
		"name": "冒險者",
		"class": "knight",
		"gender": "male",
		"level": 1,
		"hp": 150,
		"max_hp": 150,
		"mp": 30,
		"max_mp": 30,
		"ac": 10,
		"str": 16,
		"dex": 12,
		"con": 14,
		"wis": 9,
		"cha": 12,
		"int": 8,
		"alignment": 32767,
		"adena": 1000,
		"exp": 0
	}
	enter_world(default_char, slot)

func enter_world(char_data: Dictionary, slot: int) -> void:
	var world_script = load("res://scripts/arpg/arpg_world.gd")
	var world = Node2D.new()
	world.set_script(world_script)
	world.init_world(char_data, slot)
	world.return_to_menu.connect(show_menu)
	
	# 包裝在 Control 容器內
	var container = Control.new()
	container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	container.add_child(world)
	show_screen(container)
