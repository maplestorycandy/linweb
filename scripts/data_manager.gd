extends Node

# 放置天堂 Web 版 - 全域資料管理器 (DataManager)
# 原生讀取 data/ 下的所有 L1J 經典數據，100% 保留公式與數值

var mobs_data: Dictionary = {}
var maps_data: Dictionary = {}
var drops_data: Dictionary = {}
var items_data: Dictionary = {}

func _ready() -> void:
	load_all_data()

func load_all_data() -> void:
	print("[DataManager] 正在載入 L1J 核心數據表...")
	load_maps()
	load_mobs()
	load_drops()
	print("[DataManager] 核心數據表載入完成。")

func load_json(path: String) -> Variant:
	if not FileAccess.file_exists(path):
		push_warning("[DataManager] 找不到檔案：" + path)
		return null
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		push_warning("[DataManager] 無法開啟檔案：" + path)
		return null
	var text := file.get_as_text()
	var json := JSON.new()
	var err := json.parse(text)
	if err != OK:
		push_error("[DataManager] JSON 解析錯誤 (" + str(err) + "): " + path)
		return null
	return json.data

func load_maps() -> void:
	var parsed: Variant = load_json("res://data/l1j-classic-maps.json")
	if typeof(parsed) == TYPE_DICTIONARY and parsed.has("maps"):
		for m in parsed["maps"]:
			if typeof(m) == TYPE_DICTIONARY and m.has("mapKey"):
				maps_data[m["mapKey"]] = m
		print("[DataManager] 已成功註冊 %d 張地圖。" % maps_data.size())

func load_mobs() -> void:
	var parsed: Variant = load_json("res://data/l1j-mobs.json")
	if typeof(parsed) == TYPE_DICTIONARY and parsed.has("mobs"):
		mobs_data = parsed["mobs"]
		print("[DataManager] 已成功註冊 %d 隻 L1J 怪物。" % mobs_data.size())

func load_drops() -> void:
	var parsed: Variant = load_json("res://data/l1j-drops.json")
	if typeof(parsed) == TYPE_DICTIONARY:
		drops_data = parsed
		print("[DataManager] 已成功載入掉落物總表。")

func get_mob(mob_id: String) -> Dictionary:
	if mobs_data.has(mob_id):
		return mobs_data[mob_id]
	return {}

func get_map_info(map_key: String) -> Dictionary:
	if maps_data.has(map_key):
		return maps_data[map_key]
	return {}
