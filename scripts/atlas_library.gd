extends Node


























const ATLAS_ROOT: = "res://assets/atlas"

var _manifests: Dictionary = {}
var _textures: Dictionary = {}
var _frame_cache: Dictionary = {}
var _missing: Dictionary = {}


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
	var at: = AtlasTexture.new()
	at.atlas = tex
	at.region = Rect2(f["x"], f["y"], f["w"], f["h"])


	at.margin = Rect2(f["dx"], f["dy"], int(f["cw"]) - int(f["w"]), int(f["ch"]) - int(f["h"]))

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


func _warn_once(key: String, msg: String) -> void :
	if _missing.has(key):
		return
	_missing[key] = true
	push_warning("[AtlasLibrary] " + msg)
