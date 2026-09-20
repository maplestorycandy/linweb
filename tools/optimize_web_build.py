import os
import sys
import gzip
import re

def optimize(web_dir):
    print(f"[Optimizer] 開始優化 Web 輸出目錄: {web_dir}")
    
    # 1. 處理 index.html
    html_path = os.path.join(web_dir, "index.html")
    if os.path.exists(html_path):
        with open(html_path, "r", encoding="utf-8") as f:
            html = f.read()
        
        # 移除舊版 coi-serviceworker 與停用 CrossOriginIsolation 強制重整
        html = html.replace('<script src="coi-serviceworker.js"></script>', '')
        html = html.replace('"ensureCrossOriginIsolationHeaders":true', '"ensureCrossOriginIsolationHeaders":false')
        html = html.replace('"ensureCrossOriginIsolationHeaders": true', '"ensureCrossOriginIsolationHeaders": false')
        
        # 注入高速串流 Gzip 解壓器與 ServiceWorker 清理器
        gzip_loader_script = """
<script>
// 1. 清理舊版 ServiceWorker 避免瀏覽器快取凍結
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.getRegistrations().then(function(regs) {
        for (var r of regs) { r.unregister(); }
    });
}
// 2. 針對 GitHub Pages 靜態伺服器的高速 Gzip 串流解壓載入器
// 將 35.4MB 的 index.wasm 透過 index.wasm.gz (僅 7.6MB) 瞬間下載解壓，載入速度提升 400%
(function() {
    if (typeof DecompressionStream === 'undefined') return;
    var _fetch = window.fetch;
    window.fetch = async function(resource, init) {
        var url = typeof resource === 'string' ? resource : (resource && resource.url ? resource.url : '');
        if (url.endsWith('.wasm') || url.endsWith('.pck')) {
            try {
                var gzRes = await _fetch(url + '.gz', init);
                if (gzRes.ok) {
                    var ds = new DecompressionStream('gzip');
                    var decompressed = gzRes.body.pipeThrough(ds);
                    var headers = new Headers(gzRes.headers);
                    headers.set('Content-Type', url.endsWith('.wasm') ? 'application/wasm' : 'application/octet-stream');
                    return new Response(decompressed, {
                        status: gzRes.status,
                        statusText: gzRes.statusText,
                        headers: headers
                    });
                }
            } catch(e) {
                console.warn('Gzip stream fallback:', e);
            }
        }
        return _fetch(resource, init);
    };
})();
</script>
"""
        if "<head>" in html and "DecompressionStream" not in html:
            html = html.replace("<head>", "<head>" + gzip_loader_script, 1)
            with open(html_path, "w", encoding="utf-8") as f:
                f.write(html)
            print("[Optimizer] ★ 成功在 index.html 注入高速串流 Gzip 解壓器與 SW 清理器！")
    
    # 2. 壓縮 index.wasm
    wasm_path = os.path.join(web_dir, "index.wasm")
    if os.path.exists(wasm_path):
        wasm_gz = wasm_path + ".gz"
        with open(wasm_path, "rb") as f_in, gzip.open(wasm_gz, "wb", compresslevel=9) as f_out:
            f_out.write(f_in.read())
        orig_sz = os.path.getsize(wasm_path)
        gz_sz = os.path.getsize(wasm_gz)
        print(f"[Optimizer] ★ index.wasm 壓縮完畢: {orig_sz / 1024 / 1024:.2f} MB -> {gz_sz / 1024 / 1024:.2f} MB (節省 {(1 - gz_sz / orig_sz) * 100:.1f}%)")
        
    # 3. 壓縮 index.pck
    pck_path = os.path.join(web_dir, "index.pck")
    if os.path.exists(pck_path):
        pck_gz = pck_path + ".gz"
        with open(pck_path, "rb") as f_in, gzip.open(pck_gz, "wb", compresslevel=9) as f_out:
            f_out.write(f_in.read())
        orig_sz = os.path.getsize(pck_path)
        gz_sz = os.path.getsize(pck_gz)
        print(f"[Optimizer] ★ index.pck 壓縮完畢: {orig_sz / 1024 / 1024:.2f} MB -> {gz_sz / 1024 / 1024:.2f} MB (節省 {(1 - gz_sz / orig_sz) * 100:.1f}%)")

    # 4. 移除 coi-serviceworker
    sw_path = os.path.join(web_dir, "coi-serviceworker.js")
    if os.path.exists(sw_path):
        try:
            os.remove(sw_path)
            print("[Optimizer] 已移除 coi-serviceworker.js")
        except Exception:
            pass

if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else "build/web"
    optimize(target)
