import json
import os
import signal
import subprocess
import sys
import threading
import time
from collections import deque
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import urlopen

from tools.bot_manager import BotManager


ROOT = Path(__file__).resolve().parent
TOOLS_DIR = ROOT / "tools"
BROKER_SCRIPT = TOOLS_DIR / "codex_broker.py"
AGENT_SCRIPT = TOOLS_DIR / "codex_live_agent.py"
REPORT_CONSOLE_SCRIPT = TOOLS_DIR / "start_codex_report_console.ps1"
SLOT_CONSOLE_SCRIPT = TOOLS_DIR / "codex_slot_console.py"
DOCUMENTARY_SERVER_SCRIPT = TOOLS_DIR / "codex_documentary_server.py"
BROKER_BASE = os.environ.get("CODEX_BROKER_BASE", "http://127.0.0.1:8765").rstrip("/")
DOCUMENTARY_BASE = os.environ.get("CODEX_DOCUMENTARY_BASE", "http://127.0.0.1:8050")
REFRESH_SECONDS = float(os.environ.get("MAINBOT_REFRESH_SECONDS", "1.0"))
STALL_RESTART_SECONDS = float(os.environ.get("MAINBOT_STALL_RESTART_SECONDS", "30"))
UNITY_EDITOR_LOG = Path(os.environ.get(
    "UNITY_EDITOR_LOG",
    str(Path(os.environ.get("LOCALAPPDATA", "")) / "Unity" / "Editor" / "Editor.log"),
))


def now_ms() -> int:
    return int(time.time() * 1000)


def clear_screen() -> None:
    os.system("cls" if os.name == "nt" else "clear")


def compact_line(value: str) -> str:
    return " ".join((value or "").strip().split())


def http_get_json(path: str) -> tuple[int, Any]:
    try:
        with urlopen(f"{BROKER_BASE}{path}", timeout=4) as response:
            return response.status, json.loads(response.read().decode("utf-8"))
    except HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        try:
            return exc.code, json.loads(body)
        except json.JSONDecodeError:
            return exc.code, {"ok": False, "error": body}
    except URLError as exc:
        return 0, {"ok": False, "error": str(exc.reason)}


def powershell(command: str) -> None:
    subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command],
        check=False,
        cwd=str(ROOT),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def kill_existing_stack() -> None:
    current_pid = os.getpid()
    script = """
        $CurrentPid = __CURRENT_PID__
        $ProcessMatch = 'codex_broker\\.py|codex_live_agent\\.py|codex_report_console\\.py|start_codex_report_console\\.ps1|codex_slot_console\\.py|codex_documentary_server\\.py|mainbot\\.py|http\\.server 8765|Codex Broker Live Report|Codex Thought HUD|Bot Slot [12] Overlay|Codex Documentary'
        Get-CimInstance Win32_Process |
            Where-Object {
                $_.ProcessId -ne $CurrentPid -and (
                    $_.CommandLine -match $ProcessMatch
                )
            } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

        Get-NetTCPConnection -LocalPort 8765 -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique |
            Where-Object { $_ -gt 0 } |
            ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
        """
    powershell(script.replace("__CURRENT_PID__", str(current_pid)))


class ManagedProcess:
    def __init__(self, name: str, command: list[str], log_path: Path, env_overrides: dict[str, str] | None = None):
        self.name = name
        self.command = command
        self.log_path = log_path
        self.env_overrides = dict(env_overrides or {})
        self.proc: subprocess.Popen[str] | None = None
        self.lines: deque[str] = deque(maxlen=80)
        self._reader: threading.Thread | None = None

    def start(self) -> None:
        self.stop()
        env = os.environ.copy()
        env["PYTHONUNBUFFERED"] = "1"
        env.update(self.env_overrides)
        creationflags = 0
        startupinfo = None
        if os.name == "nt":
            creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
            startupinfo = subprocess.STARTUPINFO()
            startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW

        self.log_path.parent.mkdir(parents=True, exist_ok=True)
        if self.log_path.exists():
            self.log_path.unlink()

        self.proc = subprocess.Popen(
            self.command,
            cwd=str(ROOT),
            env=env,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            bufsize=1,
            creationflags=creationflags,
            startupinfo=startupinfo,
        )
        self._reader = threading.Thread(target=self._pump_output, daemon=True)
        self._reader.start()

    def _pump_output(self) -> None:
        if self.proc is None or self.proc.stdout is None:
            return
        with self.log_path.open("a", encoding="utf-8") as handle:
            for raw_line in self.proc.stdout:
                line = compact_line(raw_line)
                if not line:
                    continue
                self.lines.append(line)
                handle.write(line + "\n")
                handle.flush()

    def stop(self) -> None:
        if self.proc is None:
            return
        if self.proc.poll() is None:
            self.proc.terminate()
            try:
                self.proc.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.proc.kill()
        self.proc = None

    def is_running(self) -> bool:
        return self.proc is not None and self.proc.poll() is None

    def pid(self) -> int:
        return self.proc.pid if self.proc is not None else 0

    def recent(self, count: int) -> list[str]:
        items = list(self.lines)
        if not items:
            return []
        compacted: list[str] = []
        for item in items:
            if not compacted or compacted[-1] != item:
                compacted.append(item)
        return compacted[-count:]


class UnityEditorLogTail:
    def __init__(self, path: Path):
        self.path = path
        self.lines: deque[str] = deque(maxlen=40)
        self.last_mtime = 0.0

    def refresh(self) -> None:
        if not self.path.exists():
            return
        try:
            stat = self.path.stat()
        except OSError:
            return
        if stat.st_mtime <= self.last_mtime and self.lines:
            return
        try:
            raw_text = self.path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            return
        self.last_mtime = stat.st_mtime
        filtered = [compact_line(line) for line in raw_text.splitlines() if "[CodexBot]" in line or "ProjectPVP" in line or "NullReferenceException" in line]
        self.lines = deque([line for line in filtered if line][-40:], maxlen=40)

    def recent(self, count: int) -> list[str]:
        return list(self.lines)[-count:]


class MainBot:
    def __init__(self) -> None:
        self.manager = BotManager()
        self.broker = ManagedProcess("broker", [sys.executable, str(BROKER_SCRIPT)], TOOLS_DIR / "codex_broker.out.log")
        self.documentary = ManagedProcess("documentary", [sys.executable, str(DOCUMENTARY_SERVER_SCRIPT)], TOOLS_DIR / "codex_documentary.out.log")
        self.agents: dict[int, ManagedProcess] = {}
        self.agent_bot_ids: dict[int, str] = {}
        self.last_report: dict[str, Any] | None = None
        self.last_report_at_ms = 0
        self.last_broker_ok_at_ms = 0
        self.last_agent_restart_at_ms: dict[int, int] = {}
        self.unity_log = UnityEditorLogTail(UNITY_EDITOR_LOG)
        self.running = True

    def start(self) -> None:
        kill_existing_stack()
        self.broker.start()
        time.sleep(1.0)
        self.sync_agents(force_restart=True)
        self.start_report_console()
        self.start_slot_console(1)
        self.start_slot_console(2)
        self.start_documentary_server()
        self.open_documentary_panel()

    def stop(self) -> None:
        self.running = False
        for process in self.agents.values():
            process.stop()
        self.agents.clear()
        self.agent_bot_ids.clear()
        self.documentary.stop()
        self.broker.stop()

    def start_report_console(self) -> None:
        if os.name != "nt" or not REPORT_CONSOLE_SCRIPT.exists():
            return
        command = rf"""
            Start-Process cmd.exe -ArgumentList @(
                '/k',
                'title Codex Thought HUD && powershell -ExecutionPolicy Bypass -NoExit -File "{REPORT_CONSOLE_SCRIPT}"'
            ) -WorkingDirectory '{ROOT}'
        """
        powershell(command)

    def start_slot_console(self, slot_id: int) -> None:
        if os.name != "nt" or not SLOT_CONSOLE_SCRIPT.exists():
            return
        title = f"Bot Slot {slot_id} Overlay"
        command = rf"""
            Start-Process cmd.exe -ArgumentList @(
                '/k',
                'title {title} && mode con: cols=64 lines=20 && "{sys.executable}" "{SLOT_CONSOLE_SCRIPT}" --slot {slot_id}'
            ) -WorkingDirectory '{ROOT}'
        """
        powershell(command)

    def start_documentary_server(self) -> None:
        if self.documentary.is_running():
            return
        self.documentary.start()

    def open_documentary_panel(self) -> None:
        if os.name != "nt":
            return
        command = rf"Start-Process '{DOCUMENTARY_BASE}'"
        powershell(command)

    def sync_agents(self, *, force_restart: bool = False) -> None:
        self.manager.reload()
        desired = {item["slotId"]: item["botId"] for item in self.manager.list_active_assignments()}

        for slot_id in list(self.agents.keys()):
            if slot_id not in desired:
                self.agents[slot_id].stop()
                del self.agents[slot_id]
                self.agent_bot_ids.pop(slot_id, None)

        for slot_id, bot_id in desired.items():
            process = self.agents.get(slot_id)
            should_restart = force_restart or process is None or not process.is_running() or self.agent_bot_ids.get(slot_id) != bot_id
            if not should_restart:
                continue

            profile = self.manager.get_profile(bot_id)
            env_overrides = {
                "CODEX_AGENT_SLOT_ID": str(slot_id),
                "CODEX_BOT_ID": bot_id,
            }
            provider = compact_line(str(profile.get("provider", "") or "openai_codex")).lower()
            model = compact_line(str(profile.get("model", "") or ""))
            reasoning_effort = compact_line(str(profile.get("reasoningEffort", "") or ""))
            codex_home = str(profile.get("codexHome", "") or "").strip()
            ollama_host = str(profile.get("ollamaHost", "") or "").strip()
            openrouter_api_key_env_var = str(profile.get("openRouterApiKeyEnvVar", "") or "").strip()
            openrouter_base_url = str(profile.get("openRouterBaseUrl", "") or "").strip()
            openrouter_site_url = str(profile.get("openRouterSiteUrl", "") or "").strip()
            openrouter_app_name = str(profile.get("openRouterAppName", "") or "").strip()
            if provider:
                env_overrides["CODEX_MODEL_PROVIDER"] = provider
            if model:
                env_overrides["CODEX_MODEL"] = model
            if reasoning_effort:
                env_overrides["CODEX_REASONING_EFFORT"] = reasoning_effort
            if codex_home:
                env_overrides["CODEX_HOME"] = codex_home
            if provider == "ollama" and ollama_host:
                env_overrides["OLLAMA_HOST"] = ollama_host
            if provider == "openrouter":
                if openrouter_api_key_env_var:
                    env_overrides["OPENROUTER_API_KEY_ENV_VAR"] = openrouter_api_key_env_var
                if openrouter_base_url:
                    env_overrides["OPENROUTER_BASE_URL"] = openrouter_base_url
                if openrouter_site_url:
                    env_overrides["OPENROUTER_SITE_URL"] = openrouter_site_url
                if openrouter_app_name:
                    env_overrides["OPENROUTER_APP_NAME"] = openrouter_app_name

            if process is not None:
                process.stop()
            process = ManagedProcess(
                f"agent-slot-{slot_id}",
                [sys.executable, str(AGENT_SCRIPT)],
                TOOLS_DIR / f"codex_live_agent_slot{slot_id}.out.log",
                env_overrides=env_overrides,
            )
            process.start()
            self.agents[slot_id] = process
            self.agent_bot_ids[slot_id] = bot_id
            self.last_agent_restart_at_ms[slot_id] = now_ms()

    def poll(self) -> None:
        self.unity_log.refresh()
        if not self.broker.is_running():
            self.broker.start()
            time.sleep(1.0)
            self.sync_agents(force_restart=True)
        if not self.documentary.is_running():
            self.documentary.start()

        status, payload = http_get_json("/report")
        if status == 200 and isinstance(payload, dict) and payload.get("ok") and isinstance(payload.get("report"), dict):
            self.last_report = payload["report"]
            self.last_report_at_ms = now_ms()
            self.last_broker_ok_at_ms = self.last_report_at_ms
        elif self.last_broker_ok_at_ms > 0 and now_ms() - self.last_broker_ok_at_ms > 5000:
            self.broker.stop()
            for process in self.agents.values():
                process.stop()
            self.broker.start()
            time.sleep(1.0)
            self.sync_agents(force_restart=True)
            return

        self.sync_agents()
        sessions = {int(item.get("slotId", 0) or 0): item for item in (self.last_report or {}).get("agentSessions", [])}
        for slot_id, session in sessions.items():
            heartbeat_age_ms = int(session.get("agentHeartbeatAgeMs", -1) or -1)
            if heartbeat_age_ms <= STALL_RESTART_SECONDS * 1000:
                continue
            if now_ms() - self.last_agent_restart_at_ms.get(slot_id, 0) <= 5000:
                continue
            process = self.agents.get(slot_id)
            if process is None:
                continue
            process.start()
            self.last_agent_restart_at_ms[slot_id] = now_ms()

    def render(self) -> None:
        clear_screen()
        print("MainBot")
        print(f"Broker: {'ONLINE' if self.broker.is_running() else 'OFFLINE'} pid={self.broker.pid()}")
        print(f"Broker URL: {BROKER_BASE}")
        print()
        print("Managed agents:")
        if not self.agents:
            print("  - none")
        for slot_id in sorted(self.agents):
            process = self.agents[slot_id]
            print(f"  - slot {slot_id}: {'ONLINE' if process.is_running() else 'OFFLINE'} pid={process.pid()} bot={self.agent_bot_ids.get(slot_id, '-')}")
        print()

        sessions = (self.last_report or {}).get("agentSessions", []) if self.last_report else []
        if not sessions:
            print("Status: Waiting for Unity to publish active bot sessions.")
        else:
            print("Live sessions:")
            for session in sessions:
                print(
                    "  - "
                    f"slot={session.get('slotId')} "
                    f"bot={session.get('botDisplayName') or session.get('botId') or '-'} "
                    f"owner={session.get('controllerOwner') or '-'} "
                    f"phase={session.get('agentPhase') or '-'} "
                    f"intent={session.get('intentMode') or '-'} "
                    f"actions={session.get('agentActionCount', 0)} "
                    f"hb={session.get('agentHeartbeatAgeMs', -1)}ms"
                )
        print()

        print("Recent broker events:")
        for line in self.broker.recent(8) or ["-"]:
            print(f"  {line}")
        print()

        print("Recent agent events:")
        any_agent_lines = False
        for slot_id in sorted(self.agents):
            lines = self.agents[slot_id].recent(4)
            if not lines:
                continue
            any_agent_lines = True
            print(f"  slot {slot_id}:")
            for line in lines:
                print(f"    {line}")
        if not any_agent_lines:
            print("  -")
        print()

        print(f"Unity Editor log: {self.unity_log.path}")
        for line in self.unity_log.recent(8) or ["-"]:
            print(f"  {line}")
        print()
        print("Press Ctrl+C to stop MainBot.")


def main() -> int:
    bot = MainBot()

    def handle_exit(signum: int, frame: Any) -> None:
        raise KeyboardInterrupt

    if os.name != "nt":
        signal.signal(signal.SIGTERM, handle_exit)

    try:
        bot.start()
        while True:
            bot.poll()
            bot.render()
            time.sleep(REFRESH_SECONDS)
    except KeyboardInterrupt:
        bot.stop()
        print("\nMainBot stopped.")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
