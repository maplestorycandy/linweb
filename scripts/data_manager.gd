extends Node

# 放置天堂 Web 版 - 全域資料管理器 (DataManager)
# 原生讀取 data/ 下的所有 L1J 經典數據，100% 保留公式與數值

var mobs_data: Dictionary = {}
var maps_data: Dictionary = {}
var drops_data: Dictionary = {}
var mob_sprites_data: Dictionary = {}
var mob_sprites_by_display: Dictionary = {}

func _ready() -> void:
	load_all_data()

func load_all_data() -> void:
	print("[DataManager] 正在載入 L1J 核心數據表...")
	load_maps()
	load_mobs()
	load_drops()
	load_mob_sprites()
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

func load_mob_sprites() -> void:
	var parsed: Variant = load_json("res://data/tables/L1J_MOB_SPRITES.json")
	if typeof(parsed) == TYPE_DICTIONARY:
		mob_sprites_data = parsed
		if parsed.has("byDisplay") and typeof(parsed["byDisplay"]) == TYPE_DICTIONARY:
			mob_sprites_by_display = parsed["byDisplay"]
		elif parsed.has("byMobKey") and typeof(parsed["byMobKey"]) == TYPE_DICTIONARY:
			for k in parsed["byMobKey"].keys():
				var item = parsed["byMobKey"][k]
				if typeof(item) == TYPE_DICTIONARY and item.has("display") and item.has("atlas"):
					mob_sprites_by_display[item["display"]] = item["atlas"]
		print("[DataManager] 已成功載入怪物圖集映射表 (共 %d 項)。" % mob_sprites_by_display.size())

func resolve_mob_atlas(identity: String) -> String:
	if identity.is_empty():
		return "mob_1110"
	if mob_sprites_by_display.has(identity):
		var val = mob_sprites_by_display[identity]
		if typeof(val) == TYPE_DICTIONARY and val.has("atlas"):
			return val["atlas"]
		elif typeof(val) == TYPE_STRING:
			return val
	if mob_sprites_data.has("ambiguousDisplay") and mob_sprites_data["ambiguousDisplay"].has(identity):
		var arr = mob_sprites_data["ambiguousDisplay"][identity]
		if typeof(arr) == TYPE_ARRAY and arr.size() > 0:
			return arr[0]
	if mob_sprites_data.has("byMobKey") and mob_sprites_data["byMobKey"].has(identity):
		var val2 = mob_sprites_data["byMobKey"][identity]
		if typeof(val2) == TYPE_DICTIONARY and val2.has("atlas"):
			return val2["atlas"]
	# 預設常見怪物 fallback
	match identity:
		"狼人": return "mob_1110"
		"妖魔", "歐克": return "mob_56"
		"妖魔鬥士": return "mob_94"
		"妖魔弓箭手": return "mob_57"
		"哥布林": return "mob_1022"
		"侏儒": return "mob_54"
		"夏洛伯": return "mob_95"
		"高崙", "石頭高崙": return "mob_49"
		"史萊姆": return "mob_31"
		"骷髏": return "mob_30"
		"骷髏弓箭手": return "mob_30"
		"黑妖魔": return "mob_94"
	return "mob_1110"

func get_mob(mob_id: String) -> Dictionary:
	if mobs_data.has(mob_id):
		return mobs_data[mob_id]
	return {}
