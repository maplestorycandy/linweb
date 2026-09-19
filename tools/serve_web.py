"""
Idle Lineage Web - 本地測試與 COOP/COEP 標頭 HTTP 伺服器
=========================================================
Godot 4 Web 匯出需要 Cross-Origin-Opener-Policy 與 Cross-Origin-Embedder-Policy
以啟用 SharedArrayBuffer / 高效能多執行緒支援。
"""

import os
import sys
from http.server import HTTPServer, SimpleHTTPRequestHandler
from pathlib import Path

WEB_DIR = Path(__file__).parent.parent / "dist"

class GodotWebHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(WEB_DIR), **kwargs)

    def end_headers(self):
        # 關鍵安全標頭：啟用 SharedArrayBuffer 與 WebAssembly 多執行緒支援
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Access-Control-Allow-Origin", "*")
        super().end_headers()

def run_server(port=8080):
    WEB_DIR.mkdir(parents=True, exist_ok=True)
    server_address = ("", port)
    httpd = HTTPServer(server_address, GodotWebHandler)
    print(f"==================================================")
    print(f"  Idle Lineage Web 本地伺服器已啟動")
    print(f"  網址: http://localhost:{port}")
    print(f"  資料夾: {WEB_DIR}")
    print(f"  [COOP/COEP 安全標頭已自動啟用]")
    print(f"==================================================")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n伺服器已關閉。")

if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
    run_server(port)
