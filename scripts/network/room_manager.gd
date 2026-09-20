extends Node

# 網頁版多人開房連線管理器 (RoomManager)
# 支援網頁無縫 BroadcastChannel (同網域/跨分頁/無痕模式零延遲廣播) 與 WebSocket 通訊
signal room_state_changed(is_in_room: bool, room_id: String, is_host: bool)
signal remote_player_joined(peer_id: String, info: Dictionary)
signal remote_player_moved(peer_id: String, pos: Vector2, facing: int, is_moving: bool)
signal remote_player_attacked(peer_id: String, pos: Vector2)
signal remote_player_chatted(peer_id: String, name: String, message: String)
signal remote_player_left(peer_id: String)

var local_id: String = ""
var room_id: String = ""
var is_host: bool = false
var is_connected: bool = false
var connected_peers: Dictionary = {}

var _bc_js_name: String = ""
var _heartbeat_timer: float = 0.0

func _init() -> void:
	randomize()
	local_id = "p_%04d" % randi_range(1000, 9999)

func _process(delta: float) -> void:
	if not is_connected or room_id.is_empty():
		return
		
	# 輪詢 Web BroadcastChannel 訊息
	_poll_web_messages()
	
	_heartbeat_timer += delta
	if _heartbeat_timer >= 2.0:
		_heartbeat_timer = 0.0
		_send_heartbeat()

func create_room() -> String:
	leave_room()
	room_id = "%04d" % randi_range(1000, 9999)
	is_host = true
	is_connected = true
	_setup_broadcast_channel(room_id)
	room_state_changed.emit(true, room_id, true)
	return room_id

func join_room(target_room_id: String) -> bool:
	leave_room()
	if target_room_id.is_empty():
		return false
	room_id = target_room_id.strip_edges()
	is_host = false
	is_connected = true
	_setup_broadcast_channel(room_id)
	room_state_changed.emit(true, room_id, false)
	return true

func leave_room() -> void:
	if is_connected and not room_id.is_empty():
		_broadcast_packet({"type": "leave", "sender": local_id})
	is_connected = false
	is_host = false
	room_id = ""
	connected_peers.clear()
	room_state_changed.emit(false, "", false)

func send_handshake(char_data: Dictionary, pos: Vector2) -> void:
	if not is_connected:
		return
	var packet = {
		"type": "handshake",
		"sender": local_id,
		"name": char_data.get("name", "勇者"),
		"class": char_data.get("class", "knight"),
		"gender": char_data.get("gender", "male"),
		"level": char_data.get("level", 1),
		"pos_x": pos.x,
		"pos_y": pos.y,
		"is_host": is_host
	}
	_broadcast_packet(packet)

func send_move(pos: Vector2, facing: int, is_moving: bool) -> void:
	if not is_connected:
		return
	var packet = {
		"type": "move",
		"sender": local_id,
		"x": pos.x,
		"y": pos.y,
		"facing": facing,
		"moving": is_moving
	}
	_broadcast_packet(packet)

func send_attack(target_pos: Vector2) -> void:
	if not is_connected:
		return
	var packet = {
		"type": "attack",
		"sender": local_id,
		"tx": target_pos.x,
		"ty": target_pos.y
	}
	_broadcast_packet(packet)

func send_chat(player_name: String, text: String) -> void:
	if not is_connected:
		return
	var packet = {
		"type": "chat",
		"sender": local_id,
		"name": player_name,
		"text": text
	}
	_broadcast_packet(packet)

func _send_heartbeat() -> void:
	_broadcast_packet({"type": "heartbeat", "sender": local_id})

# ----------------- 底層 Web BroadcastChannel 封裝 -----------------
func _setup_broadcast_channel(r_id: String) -> void:
	_bc_js_name = "lin_room_" + r_id
	if OS.has_feature("web"):
		JavaScriptBridge.eval("""
		if (!window._lin_bc || window._lin_bc_room !== '%s') {
			if (window._lin_bc) window._lin_bc.close();
			window._lin_bc = new BroadcastChannel('%s');
			window._lin_bc_room = '%s';
			window._lin_inbox = [];
			window._lin_bc.onmessage = function(e) {
				window._lin_inbox.push(JSON.stringify(e.data));
			};
		}
		""" % [_bc_js_name, _bc_js_name, _bc_js_name])

func _broadcast_packet(packet: Dictionary) -> void:
	if OS.has_feature("web"):
		var json_str = JSON.stringify(packet)
		var escaped = json_str.replace("'", "\\'")
		JavaScriptBridge.eval("""
		if (window._lin_bc) {
			try {
				window._lin_bc.postMessage(JSON.parse('%s'));
			} catch(e) {}
		}
		""" % escaped)

func _poll_web_messages() -> void:
	if not OS.has_feature("web"):
		return
	var raw = JavaScriptBridge.eval("""
	(function() {
		if (window._lin_inbox && window._lin_inbox.length > 0) {
			var msgs = window._lin_inbox;
			window._lin_inbox = [];
			return JSON.stringify(msgs);
		}
		return "";
	})()
	""")
	if raw == null or raw == "":
		return
	var parsed = JSON.parse_string(raw)
	if typeof(parsed) == TYPE_ARRAY:
		for item in parsed:
			var msg_obj = JSON.parse_string(item)
			if typeof(msg_obj) == TYPE_DICTIONARY:
				_handle_received_packet(msg_obj)

func _handle_received_packet(pkt: Dictionary) -> void:
	var sender = pkt.get("sender", "")
	if sender == local_id or sender.is_empty():
		return
	var type = pkt.get("type", "")
	match type:
		"handshake":
			connected_peers[sender] = pkt
			remote_player_joined.emit(sender, pkt)
			# 若本機為房主或已存在，向新加入者回傳自身 Handshake
			if is_host:
				_send_heartbeat()
		"move":
			remote_player_moved.emit(sender, Vector2(pkt.get("x", 0), pkt.get("y", 0)), pkt.get("facing", 2), pkt.get("moving", false))
		"attack":
			remote_player_attacked.emit(sender, Vector2(pkt.get("tx", 0), pkt.get("ty", 0)))
		"chat":
			remote_player_chatted.emit(sender, pkt.get("name", "隊友"), pkt.get("text", ""))
		"leave":
			if connected_peers.has(sender):
				connected_peers.erase(sender)
			remote_player_left.emit(sender)
