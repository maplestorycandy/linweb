shader_type canvas_item;
render_mode unshaded;

// 只輸出圖示不透明輪廓外側的光，不重畫或染色圖示本體。
uniform vec4 glow_color : source_color = vec4(1.0, 0.72, 0.18, 0.95);
uniform float near_radius = 1.75;
uniform float far_radius = 3.5;

float ring_alpha(
	float right,
	float left,
	float down,
	float up,
	float down_right,
	float down_left,
	float up_right,
	float up_left
) {
	return max(
		max(max(right, left), max(down, up)),
		max(max(down_right, down_left), max(up_right, up_left))
	);
}

void fragment() {
	// TEXTURE 與 TEXTURE_PIXEL_SIZE 是 canvas_item 的 fragment 內建值；
	// 所有取樣必須留在 fragment，不能由全域自訂函式直接引用。
	vec2 pixel = TEXTURE_PIXEL_SIZE;
	float center = texture(TEXTURE, UV).a;
	float near_glow = max(ring_alpha(
		texture(TEXTURE, UV + pixel * vec2( 1.0,  0.0) * near_radius).a,
		texture(TEXTURE, UV + pixel * vec2(-1.0,  0.0) * near_radius).a,
		texture(TEXTURE, UV + pixel * vec2( 0.0,  1.0) * near_radius).a,
		texture(TEXTURE, UV + pixel * vec2( 0.0, -1.0) * near_radius).a,
		texture(TEXTURE, UV + pixel * vec2( 0.7071,  0.7071) * near_radius).a,
		texture(TEXTURE, UV + pixel * vec2(-0.7071,  0.7071) * near_radius).a,
		texture(TEXTURE, UV + pixel * vec2( 0.7071, -0.7071) * near_radius).a,
		texture(TEXTURE, UV + pixel * vec2(-0.7071, -0.7071) * near_radius).a
	) - center, 0.0);
	float far_glow = max(ring_alpha(
		texture(TEXTURE, UV + pixel * vec2( 1.0,  0.0) * far_radius).a,
		texture(TEXTURE, UV + pixel * vec2(-1.0,  0.0) * far_radius).a,
		texture(TEXTURE, UV + pixel * vec2( 0.0,  1.0) * far_radius).a,
		texture(TEXTURE, UV + pixel * vec2( 0.0, -1.0) * far_radius).a,
		texture(TEXTURE, UV + pixel * vec2( 0.7071,  0.7071) * far_radius).a,
		texture(TEXTURE, UV + pixel * vec2(-0.7071,  0.7071) * far_radius).a,
		texture(TEXTURE, UV + pixel * vec2( 0.7071, -0.7071) * far_radius).a,
		texture(TEXTURE, UV + pixel * vec2(-0.7071, -0.7071) * far_radius).a
	) - center, 0.0) * 0.52;
	float alpha = clamp(max(near_glow, far_glow), 0.0, 1.0) * glow_color.a;
	COLOR = vec4(glow_color.rgb, alpha);
}
