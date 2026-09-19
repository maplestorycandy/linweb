extends Control

# 放置天堂 Web 版 - 遊戲根節點 (GameRoot)
# 負責管理遊戲全域畫面（選單 -> 創角 -> ARPG 世界）與存檔狀態

var _current_screen: Control = null
var current_slot: int = 1
var player_data: Dictionary = {}

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	show_menu()

func switch_screen(screen: Control) -> void:
	if _current_screen != null:
		_current_screen.queue_free()
	_current_screen = screen
	add_child(screen)

func show_menu() -> void:
	var menu_script = load("res://scripts/ui/menu_screen.gd")
	if menu_script:
		var menu = menu_script.new()
		menu.connect("new_game_requested", Callable(self, "_on_new_game"))
		menu.connect("load_game_requested", Callable(self, "_on_load_game"))
		switch_screen(menu)

func _on_new_game(slot: int) -> void:
	current_slot = slot
	show_create()

func _on_load_game(slot: int) -> void:
	current_slot = slot
	# 讀取本地 LocalStorage 存檔，若無存檔則進入創角
	var save = load_slot(slot)
	if save.is_empty():
		show_create()
	else:
		start_game(save)

func show_create() -> void:
	var create_script = load("res://scripts/ui/create_screen.gd")
	if create_script:
		var create = create_script.new()
		create.connect("character_created", Callable(self, "_on_character_created"))
		create.connect("back_to_menu", Callable(self, "show_menu"))
		switch_screen(create)

func _on_character_created(data: Dictionary) -> void:
	player_data = data
	save_slot(current_slot, player_data)
	start_game(player_data)

func start_game(data: Dictionary) -> void:
	player_data = data
	var world_script = load("res://scripts/arpg/arpg_world.gd")
	if world_script:
		var world = world_script.new()
		world.init_world(player_data)
		switch_screen(world)

# --- 存檔管理 (LocalStorage / FileAccess) ---
func get_save_path(slot: int) -> String:
	return "user://save_slot_%d.json" % slot

func save_slot(slot: int, data: Dictionary) -> void:
	var path = get_save_path(slot)
	var file = FileAccess.open(path, FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify(data))
		print("[SaveManager] 存檔成功 Slot %d" % slot)

func load_slot(slot: int) -> Dictionary:
	var path = get_save_path(slot)
	if not FileAccess.file_exists(path):
		return {}
	var file = FileAccess.open(path, FileAccess.READ)
	if file:
		var parsed = JSON.parse_string(file.get_as_text())
		if typeof(parsed) == TYPE_DICTIONARY:
			return parsed
	return {}
