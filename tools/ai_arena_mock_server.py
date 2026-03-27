import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


HOST = "127.0.0.1"
PORT = 8765
PATH = "/arena/act"


def decide(snapshot):
    self_state = snapshot.get("self") or {}
    opponent = snapshot.get("opponent") or {}
    features = snapshot.get("features") or {}

    horizontal = float(features.get("horizontalDistance", 0.0))
    vertical = float(features.get("verticalDistance", 0.0))
    distance = float(features.get("euclideanDistance", 0.0))
    hostile_projectile = bool(features.get("hostileProjectileThreat", False))

    facing = 1 if float(self_state.get("facing", 1)) >= 0 else -1
    aim_x = horizontal
    aim_y = vertical
    length = (aim_x * aim_x + aim_y * aim_y) ** 0.5
    if length > 0.001:
        aim_x /= length
        aim_y /= length
    else:
        aim_x = float(facing)
        aim_y = 0.0

    action = {
        "axis": 0.0,
        "aimX": aim_x,
        "aimY": aim_y,
        "left": False,
        "right": False,
        "up": vertical > 80.0,
        "down": vertical < -80.0,
        "jumpPressed": False,
        "jumpHeld": False,
        "shootPressed": False,
        "shootHeld": False,
        "meleePressed": False,
        "ultimatePressed": False,
        "dashPrimaryPressed": False,
        "dashSecondaryPressed": False,
    }

    if hostile_projectile:
        action["axis"] = 1.0 if horizontal >= 0.0 else -1.0
        action["dashPrimaryPressed"] = True
        return action

    if distance < 90.0:
        action["axis"] = 1.0 if horizontal >= 0.0 else -1.0
        action["meleePressed"] = True
        return action

    if distance > 220.0:
        action["axis"] = 1.0 if horizontal >= 0.0 else -1.0
        action["shootHeld"] = True
        return action

    action["axis"] = 1.0 if horizontal >= 0.0 else -1.0
    return action


class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        if self.path != PATH:
            self.send_response(404)
            self.end_headers()
            return

        try:
            content_length = int(self.headers.get("Content-Length", "0"))
            raw = self.rfile.read(content_length)
            payload = json.loads(raw.decode("utf-8"))
            snapshot = payload.get("snapshot") or {}
            response = {
                "protocolVersion": "ai-arena-v1",
                "targetFrame": int(snapshot.get("simulationFrame", 0)),
                "debugText": "python-mock",
                "action": decide(snapshot),
            }
            encoded = json.dumps(response).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(encoded)))
            self.end_headers()
            self.wfile.write(encoded)
        except Exception as exc:
            encoded = json.dumps({"error": str(exc)}).encode("utf-8")
            self.send_response(500)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(encoded)))
            self.end_headers()
            self.wfile.write(encoded)

    def log_message(self, format, *args):
        return


if __name__ == "__main__":
    server = ThreadingHTTPServer((HOST, PORT), Handler)
    print(f"AI arena mock server on http://{HOST}:{PORT}{PATH}")
    server.serve_forever()
