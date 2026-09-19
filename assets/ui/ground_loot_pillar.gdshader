shader_type canvas_item;
render_mode unshaded, blend_add;

// 詞綴／技能書品質的地面光柱。橫向輪廓在整個高度保持平行，禁止再做成三角箭頭。
uniform vec4 pillar_color : source_color = vec4(0.5, 0.82, 0.55, 1.0);

void fragment() {
	float distance_from_center = abs(UV.x * 2.0 - 1.0);
	float soft_column = 1.0 - smoothstep(0.42, 1.0, distance_from_center);
	float bright_core = 1.0 - smoothstep(0.0, 0.24, distance_from_center);

	// 只讓柱頂柔和消失；柱身寬度不隨高度改變，因此不會再形成箭頭。
	float top_fade = smoothstep(0.0, 0.22, UV.y);
	float vertical_flow = 0.88 + 0.12 * sin(TIME * 2.2 - UV.y * 15.0);
	float alpha = (soft_column * 0.42 + bright_core * 0.48) * top_fade * vertical_flow;

	vec3 light_color = pillar_color.rgb * (0.82 + bright_core * 0.38);
	COLOR = vec4(light_color, clamp(alpha * pillar_color.a, 0.0, 1.0));
}
