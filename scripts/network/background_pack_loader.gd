extends Node

# 放置天堂 Web 版 - 背景資源分包載入器 (BackgroundPackLoader)
# 支援首包極小化 (< 12MB)，邊玩邊在後台默默下載 DLC PCK 分包並動態掛載

signal pack_progress(pack_name: String, progress: float, downloaded_bytes: int, total_bytes: int)
signal pack_loaded(pack_name: String)
signal all_packs_loaded

var _queue: Array[String] = []
var _current_pack: String = ""
var _http_request: HTTPRequest = null
var _loaded_packs: Dictionary = {}
var _is_downloading: bool = false

func _ready() -> void:
	_http_request = HTTPRequest.new()
	_http_request.use_threads = false # WebAssembly 相容安全模式
	add_child(_http_request)
	_http_request.request_completed.connect(_on_request_completed)

func start_downloads(packs: Array[String] = ["dlc_classes.pck", "dlc_world.pck"]) -> void:
	for p in packs:
		if not _loaded_packs.has(p) and not _queue.has(p):
			_queue.append(p)
	
	if not _is_downloading:
		_process_next_pack()

func is_pack_loaded(pack_name: String) -> bool:
	return _loaded_packs.get(pack_name, false)

func _process(delta: float) -> void:
	if _is_downloading and _http_request != null and not _current_pack.is_empty():
		var body_size = _http_request.get_body_size()
		var downloaded = _http_request.get_downloaded_bytes()
		if body_size > 0:
			var progress = float(downloaded) / float(body_size)
			pack_progress.emit(_current_pack, progress, downloaded, body_size)

func _process_next_pack() -> void:
	if _queue.is_empty():
		_is_downloading = false
		_current_pack = ""
		print("[BackgroundPackLoader] 所有後台分包已處理完畢！")
		all_packs_loaded.emit()
		return
	
	_is_downloading = true
	_current_pack = _queue.pop_front()
	
	var save_path = "user://" + _current_pack
	
	# 如果本機 user:// 已經存在該檔案且有效，嘗試直接掛載
	if FileAccess.file_exists(save_path):
		var test_size = FileAccess.get_file_as_bytes(save_path).size()
		if test_size > 1024:
			print("[BackgroundPackLoader] 發現本地緩存 DLC: %s (%d bytes)，直接掛載..." % [_current_pack, test_size])
			if _try_mount_pack(save_path, _current_pack):
				_process_next_pack()
				return
	
	# 發起 HTTP 下載
	_http_request.download_file = save_path
	var url = _get_pack_url(_current_pack)
	print("[BackgroundPackLoader] 開始後台靜默下載分包: ", url)
	var err = _http_request.request(url)
	if err != OK:
		push_warning("[BackgroundPackLoader] 發起下載請求失敗: %s, 錯誤碼: %d" % [url, err])
		_is_downloading = false
		_process_next_pack()

func _get_pack_url(pack_name: String) -> String:
	if OS.has_feature("web"):
		var base = JavaScriptBridge.eval("window.location.origin + window.location.pathname.substring(0, window.location.pathname.lastIndexOf('/') + 1)")
		if base != null and str(base).begins_with("http"):
			return str(base) + pack_name
	return "http://localhost:8000/" + pack_name

func _on_request_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray) -> void:
	var save_path = "user://" + _current_pack
	if response_code == 200:
		print("[BackgroundPackLoader] 下載完成: %s (HTTP 200)" % _current_pack)
		_try_mount_pack(save_path, _current_pack)
	else:
		push_warning("[BackgroundPackLoader] 下載 %s 回傳狀態碼: %d (可能在單機或未發佈 DLC 模式)" % [_current_pack, response_code])
	
	_is_downloading = false
	_process_next_pack()

func _try_mount_pack(path: String, pack_name: String) -> bool:
	var ok = ProjectSettings.load_resource_pack(path)
	if ok:
		_loaded_packs[pack_name] = true
		print("[BackgroundPackLoader] ★ 成功動態掛載 DLC: %s 至 res:// 虛擬檔案系統！" % pack_name)
		pack_loaded.emit(pack_name)
		
		# 若掛載了 dlc_world，通知 DataManager 加載擴充表
		if pack_name.contains("world"):
			if DataManager != null and DataManager.has_method("load_all_data"):
				DataManager.load_all_data()
		return true
	else:
		push_warning("[BackgroundPackLoader] 動態掛載 DLC 失敗: " + path)
		return false
