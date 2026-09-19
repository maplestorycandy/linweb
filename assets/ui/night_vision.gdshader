// 夜間視野遮罩（Claude）：整面壓黑，只在玩家周圍留一圈看得見的範圍。
//
// ⚠️⚠️ 2026-07-31 三改（現行規格）：**連續漸層＋封頂**。
//   「視野外 1 格由 0 到 40%、2 格 40~80%、**3 格以上都維持 80%**＝玩家不可見」
//   → d ≤ clear_px 全透明；往外每格 +40%；**2 格到 80% 之後一路封頂**，
//     ⚠️ 最外圈是 80% **永遠不到全黑**——不要「順手」補成 100%。
//   80% 同時是「看不到」門檻：藏怪與遠距選敵極限（`DayNight.MaxDarkness`）都掛同一個數字。
//
// ❌ 灰階去彩度已取消（用戶 2026-07-31：「取消天黑時灰階設定 單純限制視野即可」）。
//    暗處只壓黑、不褪色。仍讀 `hint_screen_texture`（照原色疊黑要拿得到底圖），
//    呼叫端的 `BackBufferCopy` 保留。
// ⚠️ 用 `SCREEN_UV × screen_px` 還原成**像素座標**再算距離——直接拿 UV 算的話畫面不是正方形，
//    亮區會被壓成橢圓。
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear;
uniform vec2 center_px = vec2(640.0, 235.0);
uniform vec2 screen_px = vec2(1280.0, 720.0);

// 完全看得見的半徑（像素）＝照明範圍。呼叫端由 `DayNight.VisionRadiusPixels` 餵。
uniform float clear_px = 144.0;
// 一格漸層的寬度（像素）＝1 格。
uniform float band_px = 48.0;

// 光圈的形狀：量距離前先把**縱向**乘上這個數（官方 `Source.lmp` 八級遮罩實測一律 2:1）。
// ⚠️ 等角投影下螢幕縱向 1 格只佔 12px、橫向佔 24px，用正圓量距離的話南北向會多看到
//    一倍的格子。呼叫端由 `DayNight.IsoAspectY` 餵，並且**必須**與
//    `CombatEngine.PlayerVisionAspectY` 同值，否則會出現「看起來是亮的卻選不到」的斜向縫。
uniform float iso_aspect_y = 2.0;

// 每一格加深多少（用戶 2026-07-31 三改：每格 +40%）。
uniform float band_step = 0.4;
// 黑度封頂（80%＝「玩家不可見」門檻·**不是全黑**）。
uniform float max_dark = 0.8;
// 障礙物二維影子**最深**的黑度（＝封頂那一階 0.8）。
uniform float shadow_dark = 0.8;
// 影子邊緣的羽化半徑（像素·預設 1 格）——⚠️ 用戶 2026-07-31 四改：
// 「二維影子邊緣也採用從 0%~80% 的漸進」＝不再是踩進去就 80% 的硬邊。
uniform float shadow_feather_px = 48.0;

// 真陰影可視圖（B 案·`NightShadowCaster` 畫的離屏視口）：白＝看得見、黑＝被障礙物擋住。
// ⚠️ 影子**只影響畫面深淺**：障礙物後面的怪照樣不畫（那是 `HasLineOfSight` 管的，不看這張圖）。
uniform sampler2D visibility_tex : repeat_disable, filter_linear;

// 羽化＝把遮罩在半徑內做平均：中心 ＋ 內外兩圈各 8 向＝17 個樣本。
// ⚠️ 要平均**原始值**，不要每個樣本各自二值化——那只會得到 17 階的階梯，邊緣照樣看得出來。
// ⚠️ 刻意寫成展開的加法（不用 const 陣列／迴圈）：這支 shader 是夜間畫面的唯一遮罩，
//    語法上越保守越好，編不過就是整片夜視壞掉。
float feather_ring(vec2 uv, vec2 r) {
	float diagonal = 0.7071;
	return texture(visibility_tex, uv + vec2(r.x, 0.0)).r
		+ texture(visibility_tex, uv + vec2(-r.x, 0.0)).r
		+ texture(visibility_tex, uv + vec2(0.0, r.y)).r
		+ texture(visibility_tex, uv + vec2(0.0, -r.y)).r
		+ texture(visibility_tex, uv + vec2(r.x, r.y) * diagonal).r
		+ texture(visibility_tex, uv + vec2(r.x, -r.y) * diagonal).r
		+ texture(visibility_tex, uv + vec2(-r.x, r.y) * diagonal).r
		+ texture(visibility_tex, uv + vec2(-r.x, -r.y) * diagonal).r;
}

// 這一點有多「在影子裡」：0＝完全在光裡、1＝完全在影子深處。
float shadow_coverage(vec2 uv) {
	vec2 unit = vec2(1.0) / max(screen_px, vec2(1.0));
	vec2 outer = shadow_feather_px * unit;
	float lit = texture(visibility_tex, uv).r
		+ feather_ring(uv, outer * 0.5)
		+ feather_ring(uv, outer);
	return clamp(1.0 - lit / 17.0, 0.0, 1.0);
}

void fragment() {
	vec2 p = SCREEN_UV * screen_px;
	vec2 offset = p - center_px;
	float d = length(vec2(offset.x, offset.y * iso_aspect_y));
	vec3 src = texture(screen_tex, SCREEN_UV).rgb;

	// 連續漸層：照明圈內全透明，往外每格 +band_step，到 max_dark 封頂。原色壓黑、不褪色。
	float bands = max(d - clear_px, 0.0) / max(band_px, 1.0);
	float dark = min(bands * band_step, max_dark);
	// 被障礙物擋住：邊緣 0% → 深處 shadow_dark 的漸進（距離已經更黑就維持更黑）。
	dark = max(dark, shadow_dark * shadow_coverage(SCREEN_UV));
	COLOR = vec4(mix(src, vec3(0.0, 0.0, 0.02), dark), 1.0);
}
