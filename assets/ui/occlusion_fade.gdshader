// 前景頁（遮蔽群組層）的逐群組退淡。
//
// 客戶端語意：玩家站上 s32 第五節登記格 → **該群組**半透明——不是整頁。
// 每張前景頁配一張群組遮罩（mask_YY_XX.webp·R+G*256＝頁內群組編號+1、
// 黑＝無群組），呈現層把玩家所站群組換算成頁內編號餵進 active_ids，
// 只有那幾個群組的像素退到 fade_alpha，同頁其他樹冠／屋頂維持不透明。
shader_type canvas_item;

uniform sampler2D group_mask : filter_nearest, repeat_disable;
uniform int active_count = 0;
// ⚠️ 容量與 ArpgEngineScreen.Exploration 的 ForeActiveIdCapacity 必須同值。
//    全量實掃單格最多被 13 個群組登記（mineral_cave），8 不夠、16 才蓋得住。
uniform int active_ids[16];
uniform float fade_alpha = 0.35;

void fragment() {
    vec4 base = texture(TEXTURE, UV);
    if (active_count > 0) {
        vec3 mask = texture(group_mask, UV).rgb;
        int id = int(round(mask.r * 255.0)) + 256 * int(round(mask.g * 255.0));
        if (id > 0) {
            for (int i = 0; i < active_count; i++) {
                if (active_ids[i] == id) {
                    base.a *= fade_alpha;
                    break;
                }
            }
        }
    }
    COLOR = base;
}
