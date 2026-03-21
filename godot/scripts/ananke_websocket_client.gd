extends Node
class_name AnankeWebSocketClient

signal frame_received(frame: Dictionary)
signal connection_state_changed(connected: bool)

@export var stream_url := "ws://127.0.0.1:3001/stream"
@export var reconnect_delay_seconds := 1.0

var _socket := WebSocketPeer.new()
var _connected := false
var _time_until_reconnect := 0.0

func _ready() -> void:
	_connect_socket()

func _process(delta: float) -> void:
	if _connected:
		_socket.poll()
		var state := _socket.get_ready_state()
		if state == WebSocketPeer.STATE_OPEN:
			while _socket.get_available_packet_count() > 0:
				var payload := _socket.get_packet().get_string_from_utf8()
				var json := JSON.parse_string(payload)
				if json is Dictionary:
					emit_signal("frame_received", json)
		elif state == WebSocketPeer.STATE_CLOSED:
			_set_connected(false)
			_time_until_reconnect = reconnect_delay_seconds
	else:
		_time_until_reconnect -= delta
		if _time_until_reconnect <= 0.0:
			_connect_socket()

func _connect_socket() -> void:
	_socket = WebSocketPeer.new()
	var error := _socket.connect_to_url(stream_url)
	if error != OK:
		push_warning("Unable to connect to %s: %s" % [stream_url, error_string(error)])
		_set_connected(false)
		_time_until_reconnect = reconnect_delay_seconds
		return

	_set_connected(true)

func _set_connected(value: bool) -> void:
	if _connected == value:
		return
	_connected = value
	emit_signal("connection_state_changed", _connected)
