// 3.8c 水下地圖環境層。
//
// main 的 S_MapID 只傳 underwater 旗標，藍色畫面由客戶端生成；Godot 沒有原客戶端，
// 因此以獨立透明層還原該呈現語意。這支 shader 不取樣螢幕、不改 HUD，也不參與
// 水下扣血或視野判定；兩道低對比移動水光只讓畫面有水體感，不能亮到蓋住地圖細節。
shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform vec4 water_tint : source_color = vec4(0.015, 0.255, 0.46, 0.27);
uniform vec4 caustic_tint : source_color = vec4(0.16, 0.68, 0.88, 1.0);
uniform float caustic_strength : hint_range(0.0, 0.08) = 0.035;

void fragment() {
	vec2 uv = UV;
	float wave_a = sin((uv.x * 1.30 + uv.y * 0.82) * 18.0 + TIME * 0.55);
	float wave_b = sin((uv.x * -0.72 + uv.y * 1.45) * 23.0 - TIME * 0.42);
	float caustic = smoothstep(0.70, 1.0, wave_a * 0.55 + wave_b * 0.45);
	float glow = caustic * caustic_strength;
	vec3 colour = mix(water_tint.rgb, caustic_tint.rgb, glow);
	COLOR = vec4(colour, water_tint.a + glow);
}
