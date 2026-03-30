import json
import os
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.error import HTTPError, URLError
from urllib.request import urlopen

from codex_trace_store import read_trace_events


HOST = os.environ.get("CODEX_DOC_HOST", "127.0.0.1")
PORT = int(os.environ.get("CODEX_DOC_PORT", "8050"))
BROKER_BASE = os.environ.get("CODEX_BROKER_BASE", "http://127.0.0.1:8765").rstrip("/")


def http_get_json(path: str) -> dict:
    try:
        with urlopen(f"{BROKER_BASE}{path}", timeout=3) as response:
            return json.loads(response.read().decode("utf-8"))
    except (HTTPError, URLError, json.JSONDecodeError, OSError):
        return {"ok": False, "error": "broker_unavailable"}


HTML = """<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <title>Codex Documentary Panel</title>
  <style>
    body { font-family: Consolas, monospace; background:#0d0f14; color:#e8edf2; margin:0; padding:16px; }
    h1,h2 { margin:0 0 12px 0; }
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:16px; margin-bottom:16px; }
    .card { background:#151923; border:1px solid #2a3040; border-radius:10px; padding:12px; }
    .ok { color:#7CFC98; } .bad { color:#ff8484; } .warn { color:#ffd27a; }
    .trace { white-space:pre-wrap; word-break:break-word; font-size:12px; background:#0f131c; padding:8px; border-radius:8px; border:1px solid #222838; }
    details { margin:8px 0; }
    summary { cursor:pointer; color:#9fd3ff; }
  </style>
</head>
<body>
  <h1>Codex Documentary Panel</h1>
  <div id="overview" class="grid"></div>
  <div class="grid">
    <div class="card"><h2>Slot 1 Trace</h2><div id="slot1"></div></div>
    <div class="card"><h2>Slot 2 Trace</h2><div id="slot2"></div></div>
  </div>
  <script>
    async function loadJson(path) {
      const res = await fetch(path);
      return await res.json();
    }
    function esc(value) {
      return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
    }
    function renderSessionCard(session) {
      const status = session.controllerOwner === "Codex" ? "ok" : "warn";
      return `<div class="card">
        <h2>Slot ${esc(session.slotId)}</h2>
        <div><b>Bot:</b> ${esc(session.botDisplayName || session.botId || "-")}</div>
        <div><b>Model:</b> ${esc(session.agentModel || "-")}</div>
        <div><b>Intent:</b> ${esc(session.intentMode || "-")}</div>
        <div><b>Why:</b> ${esc(session.intentReason || "-")}</div>
        <div><b>Owner:</b> <span class="${status}">${esc(session.controllerOwner || "-")}</span></div>
        <div><b>Phase:</b> ${esc(session.agentPhase || "-")}</div>
        <div><b>Score:</b> ${esc(session.playerOneWins || 0)} x ${esc(session.playerTwoWins || 0)}</div>
      </div>`;
    }
    function renderTrace(events) {
      return events.map((event) => {
        const payload = esc(JSON.stringify(event.payload, null, 2));
        return `<details><summary>${esc(event.timestamp)} | ${esc(event.eventType)}</summary><div class="trace">${payload}</div></details>`;
      }).join("");
    }
    async function refresh() {
      const reportEnvelope = await loadJson('/api/report');
      const report = reportEnvelope.report || { agentSessions: [] };
      const slot1 = await loadJson('/api/traces?slotId=1&limit=40');
      const slot2 = await loadJson('/api/traces?slotId=2&limit=40');
      document.getElementById('overview').innerHTML = report.agentSessions.map(renderSessionCard).join("");
      document.getElementById('slot1').innerHTML = renderTrace(slot1.events || []);
      document.getElementById('slot2').innerHTML = renderTrace(slot2.events || []);
    }
    refresh();
    setInterval(refresh, 1000);
  </script>
</body>
</html>"""


class Handler(BaseHTTPRequestHandler):
    def do_GET(self) -> None:
        if self.path == "/":
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.end_headers()
            self.wfile.write(HTML.encode("utf-8"))
            return
        if self.path == "/api/report":
            self._write_json(http_get_json("/report"))
            return
        if self.path.startswith("/api/traces"):
            from urllib.parse import parse_qs, urlparse
            parsed = urlparse(self.path)
            query = parse_qs(parsed.query)
            slot_id = int((query.get("slotId") or ["0"])[0] or "0")
            limit = int((query.get("limit") or ["100"])[0] or "100")
            self._write_json({"ok": True, "events": read_trace_events(limit=limit, slot_id=slot_id)})
            return
        self.send_response(404)
        self.end_headers()

    def log_message(self, format: str, *args) -> None:
        return

    def _write_json(self, payload: dict) -> None:
        raw = json.dumps(payload, ensure_ascii=True).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)


def main() -> int:
    server = ThreadingHTTPServer((HOST, PORT), Handler)
    print(f"Codex documentary server listening on http://{HOST}:{PORT}", flush=True)
    server.serve_forever()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
