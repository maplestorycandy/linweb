shader_type canvas_item;

// 8971 是 3.3 客戶端 FrameSurf type 3 的灰階形狀遮罩：白色是框外，
// 黑色是視窗內。解包 PNG 沒有 alpha，直接畫會變成一整片白；在這裡按
// 客戶端語意把灰階反轉成 alpha，8970 的彩色金屬框再畫在它上面。
uniform vec4 panel_color : source_color = vec4(0.055, 0.047, 0.041, 0.985);

void fragment() {
	float outside = texture(TEXTURE, UV).r;
	COLOR = vec4(panel_color.rgb, panel_color.a * (1.0 - outside));
}
