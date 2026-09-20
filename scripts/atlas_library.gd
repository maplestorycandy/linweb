extends Node


























const ATLAS_ROOT: = "res://assets/atlas"

var _manifests: Dictionary = {}
var _textures: Dictionary = {}
var _frame_cache: Dictionary = {}
var _missing: Dictionary = {}


func _ready() -> void:
	if has_node("/root/BackgroundPackLoader"):
		var loader = get_node("/root/BackgroundPackLoader")
		loader.pack_loaded.connect(_on_pack_loaded)

func _on_pack_loaded(pack_name: String) -> void:
	_missing.clear()
	_manifests.clear()
	_textures.clear()
	_frame_cache.clear()
	print("[AtlasLibrary] DLC %s 掛載完畢，已清空資源缺失快取。" % pack_name)

func has_atlas(group: String, atlas_name: String) -> bool:
	return get_manifest(group, atlas_name) != null



func get_manifest(group: String, atlas_name: String):
	var key: = group + "/" + atlas_name
	if _manifests.has(key):
		return _manifests[key]
	var path: = "%s/%s/%s.json" % [ATLAS_ROOT, group, atlas_name]
	if not ResourceLoader.exists(path) and not FileAccess.file_exists(path):
		_warn_once(key, "找不到 manifest：" + path)
		return null
	var text: = FileAccess.get_file_as_string(path)
	if text.is_empty():
		_warn_once(key, "manifest 讀不到內容：" + path)
		return null
	var parsed = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		_warn_once(key, "manifest 不是 JSON 物件：" + path)
		return null
	_manifests[key] = parsed
	return parsed



func get_texture(group: String, atlas_name: String) -> Texture2D:
	var key: = group + "/" + atlas_name
	if _textures.has(key):
		return _textures[key]
	var path: = "%s/%s/%s.png" % [ATLAS_ROOT, group, atlas_name]
	var tex: = load(path) as Texture2D
	if tex == null:
		_warn_once(key + ":png", "圖集貼圖載入失敗：" + path)
		return null
	_textures[key] = tex
	return tex



func get_actions(group: String, atlas_name: String) -> PackedStringArray:
	var man = get_manifest(group, atlas_name)
	if man == null:
		return PackedStringArray()
	var out: = PackedStringArray()
	for k in (man["frames"] as Dictionary).keys():
		out.append(k)
	return out



func get_frame_count(group: String, atlas_name: String, action: String) -> int:
	var man = get_manifest(group, atlas_name)
	if man == null:
		return 0
	var frames: Dictionary = man["frames"]
	if not frames.has(action):
		return 0
	return (frames[action] as Array).size()



func get_frame(group: String, atlas_name: String, action: String, index: int) -> AtlasTexture:
	var ck: = "%s/%s/%s/%d" % [group, atlas_name, action, index]
	if _frame_cache.has(ck):
		return _frame_cache[ck]

	var man = get_manifest(group, atlas_name)
	if man == null:
		return null
	var frames: Dictionary = man["frames"]
	if not frames.has(action):
		_warn_once(ck, "沒有這個動作：%s/%s → %s" % [group, atlas_name, action])
		return null
	var arr: Array = frames[action]
	if index < 0 or index >= arr.size():
		_warn_once(ck, "幀號超界：%s/%s/%s[%d]，共 %d 幀" % [group, atlas_name, action, index, arr.size()])
		return null
	var tex: = get_texture(group, atlas_name)
	if tex == null:
		return null

	var f: Dictionary = arr[index]
	var dx = float(f.get("dx", 0.0))
	var dy = float(f.get("dy", 0.0))
	var w = float(f.get("w", 0.0))
	var h = float(f.get("h", 0.0))
	var cw = float(f.get("cw", w))
	var ch = float(f.get("ch", h))

	# 走路動畫補償：消除原版 Lineage 走格內置位移導致的每循環末尾拉回/倒退現象
	if action.contains("walk") and arr.size() > 1:
		var f0: Dictionary = arr[0]
		var foot_x0 = float(f0.get("dx", 0.0)) + float(f0.get("w", 0.0)) * 0.5
		var foot_y0 = float(f0.get("dy", 0.0)) + float(f0.get("h", 0.0))
		var cur_foot_x = dx + w * 0.5
		var cur_foot_y = dy + h
		dx -= (cur_foot_x - foot_x0)
		dy -= (cur_foot_y - foot_y0)

	var at: = AtlasTexture.new()
	at.atlas = tex
	at.region = Rect2(f["x"], f["y"], f["w"], f["h"])
	at.margin = Rect2(dx, dy, int(cw) - int(w), int(ch) - int(h))
	at.filter_clip = true
	_frame_cache[ck] = at
	return at



func get_sprite_frames(group: String, atlas_name: String, fps: float = 8.0, loop: bool = true) -> SpriteFrames:
	var man = get_manifest(group, atlas_name)
	if man == null:
		return null
	var sf: = SpriteFrames.new()
	sf.remove_animation("default")
	var frames: Dictionary = man["frames"]
	for action in frames.keys():
		sf.add_animation(action)
		sf.set_animation_speed(action, fps)
		sf.set_animation_loop(action, loop)
		var n: int = (frames[action] as Array).size()
		for i in n:
			var t: = get_frame(group, atlas_name, action, i)
			if t != null:
				sf.add_frame(action, t)
	return sf



func clear_cache(group: String = "", atlas_name: String = "") -> void :
	if group.is_empty():
		_manifests.clear()
		_textures.clear()
		_frame_cache.clear()
		return
	var key: = group + "/" + atlas_name
	_manifests.erase(key)
	_textures.erase(key)
	for k in _frame_cache.keys():
		if (k as String).begins_with(key + "/"):
			_frame_cache.erase(k)


func get_content_bounds(group: String, atlas_name: String, action: String = "") -> Dictionary:
	var man = get_manifest(group, atlas_name)
	if man == null or not man.has("frames"):
		return {"cx": 0.0, "top": 0.0, "bottom": 0.0, "height": 0.0}
	var frames_dict: Dictionary = man["frames"]
	
	# 尋找標準 idle 動作計算基準腳底錨點，確保動作切換時腳底絕對鎖定不抽搐
	var base_act = ""
	for candidate in ["d0/idle", "d5/idle", "sword1_idle", "idle"]:
		if frames_dict.has(candidate):
			base_act = candidate
			break
	if base_act.is_empty():
		for k in frames_dict.keys():
			if (k as String).contains("idle"):
				base_act = k
				break
	if base_act.is_empty() and frames_dict.size() > 0:
		base_act = frames_dict.keys()[0]
	
	var arr: Array = frames_dict[base_act]
	var min_x = 1e9
	var max_x = -1e9
	var min_y = 1e9
	var max_y = -1e9
	for f in arr:
		var dx = float(f.get("dx", 0.0))
		var dy = float(f.get("dy", 0.0))
		var w = float(f.get("w", 0.0))
		var h = float(f.get("h", 0.0))
		min_x = min(min_x, dx)
		max_x = max(max_x, dx + w)
		min_y = min(min_y, dy)
		max_y = max(max_y, dy + h)
	
	if max_x < min_x:
		return {"cx": 0.0, "top": 0.0, "bottom": 0.0, "height": 0.0}
	var cx = (min_x + max_x) * 0.5
	return {"cx": cx, "top": min_y, "bottom": max_y, "height": max_y - min_y}

func make_sprite(group: String, atlas_name: String, action: String = "idle", fps: float = 8.0, loop: bool = true) -> AnimatedSprite2D:
	var sf = get_sprite_frames(group, atlas_name, fps, loop)
	if sf == null:
		return null
	var spr = AnimatedSprite2D.new()
	spr.sprite_frames = sf
	spr.centered = false
	
	var bounds = get_content_bounds(group, atlas_name, action)
	if bounds["bottom"] > 0.0:
		spr.offset = Vector2(-bounds["cx"], -bounds["bottom"])
	else:
		spr.centered = true
		
	var anims = sf.get_animation_names()
	if anims.size() == 0:
		return spr
	if sf.has_animation(action):
		spr.play(action)
	else:
		# 嘗試找相似 action，如 d5/walk, walk, idle 等
		var found = false
		for a in anims:
			if a.ends_with("/" + action) or a.begins_with(action) or a.contains(action):
				spr.play(a)
				found = true
				break
		if not found:
			spr.play(anims[0])
	return spr

func _warn_once(key: String, msg: String) -> void :
	if _missing.has(key):
		return
	_missing[key] = true
	push_warning("[AtlasLibrary] " + msg)

