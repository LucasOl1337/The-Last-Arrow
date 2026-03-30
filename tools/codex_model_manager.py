import json
import os
import subprocess
import tomllib
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import urlopen


ROOT = Path(__file__).resolve().parent.parent
TOOLS_DIR = ROOT / "tools"
DEFAULT_CODEX_HOME = Path.home() / ".codex"
DEFAULT_CODEX_CONFIG = DEFAULT_CODEX_HOME / "config.toml"
DEFAULT_CODEX_EXE = Path(os.environ.get("CODEX_EXE", str(DEFAULT_CODEX_HOME / ".sandbox-bin" / "codex.exe")))
DEFAULT_OLLAMA_HOST = os.environ.get("OLLAMA_HOST", "http://127.0.0.1:11434").rstrip("/")

PROVIDER_PRESETS = [
    ("openai_codex", "OpenAI via Codex auth"),
    ("ollama", "Ollama local via Codex --oss"),
]

REASONING_PRESETS = [
    ("", "Herdar do config local"),
    ("low", "Low"),
    ("medium", "Medium"),
    ("high", "High"),
]

OPENAI_MODEL_PRESETS = [
    "gpt-5.4",
    "gpt-5.4-mini",
    "gpt-5.3-codex",
    "gpt-5.2-codex",
    "gpt-5.2",
    "gpt-5.1-codex-max",
    "gpt-5.1-codex-mini",
]


def compact_line(value: str) -> str:
    return " ".join((value or "").strip().split())


def read_codex_defaults(codex_home: str = "") -> dict[str, str]:
    base = Path(codex_home) if codex_home else DEFAULT_CODEX_HOME
    config_path = base / "config.toml"
    if not config_path.exists():
        return {"model": "", "reasoning": ""}
    try:
        payload = tomllib.loads(config_path.read_text(encoding="utf-8"))
    except (OSError, tomllib.TOMLDecodeError):
        return {"model": "", "reasoning": ""}
    return {
        "model": str(payload.get("model", "") or ""),
        "reasoning": str(payload.get("model_reasoning_effort", "") or ""),
    }


def find_codex_executable() -> str:
    if DEFAULT_CODEX_EXE.exists():
        return str(DEFAULT_CODEX_EXE)
    return ""


def discover_codex_homes() -> list[tuple[str, str]]:
    home = Path.home()
    homes: list[tuple[str, str]] = [("", "Herdar ~/.codex padrao")]
    candidates: list[Path] = []
    if DEFAULT_CODEX_HOME.exists():
        candidates.append(DEFAULT_CODEX_HOME)
    for entry in home.iterdir():
        if entry.is_dir() and entry.name.startswith(".codex") and entry not in candidates:
            candidates.append(entry)
    for path in sorted(candidates):
        label = str(path)
        if (path / "auth.json").exists():
            label += " | auth"
        if (path / "config.toml").exists():
            label += " | config"
        homes.append((str(path), label))
    return homes


def build_openai_model_options(codex_home: str = "") -> list[tuple[str, str]]:
    defaults = read_codex_defaults(codex_home)
    models: list[str] = []
    if defaults["model"]:
        models.append(defaults["model"])
    for model in OPENAI_MODEL_PRESETS:
        if model not in models:
            models.append(model)
    return [("", "Herdar do config local")] + [(model, model) for model in models]


def discover_ollama_models(host: str = DEFAULT_OLLAMA_HOST) -> list[tuple[str, str]]:
    normalized_host = compact_line(host) or DEFAULT_OLLAMA_HOST
    try:
        with urlopen(f"{normalized_host}/api/tags", timeout=4) as response:
            payload = json.loads(response.read().decode("utf-8"))
    except (HTTPError, URLError, TimeoutError, OSError, json.JSONDecodeError):
        return []
    models = payload.get("models") if isinstance(payload, dict) else []
    if not isinstance(models, list):
        return []
    items: list[tuple[str, str]] = []
    for entry in models:
        if not isinstance(entry, dict):
            continue
        name = compact_line(str(entry.get("name", "") or ""))
        if not name:
            continue
        size = str(entry.get("details", {}).get("parameter_size", "") or "").strip()
        label = f"{name} | {size}" if size else name
        items.append((name, label))
    return items


def validation_summary_lines(result: dict[str, Any]) -> list[str]:
    return [
        f"Status: {result.get('status', '-')}",
        f"Mensagem: {result.get('message', '-')}",
        f"Provider: {result.get('provider', '-')}",
        f"Model solicitado: {result.get('requestedModel', '-') or '-'}",
        f"Model autorrelatado: {result.get('selfReportedModel', '-') or '-'}",
        f"Executavel: {result.get('codexPath', '-') or '-'}",
        f"Auth: {'OK' if result.get('authPresent') else 'AUSENTE'}",
        f"Config: {'OK' if result.get('configPresent') else 'AUSENTE'}",
        f"Verificado em: {result.get('verifiedAt', '-') or '-'}",
    ]


def _now_iso() -> str:
    import time
    return time.strftime("%Y-%m-%dT%H:%M:%S")


def _parse_probe_stdout(stdout_text: str) -> tuple[dict[str, Any] | None, str]:
    final_text = ""
    failure_reason = ""
    for raw_line in stdout_text.splitlines():
        try:
            event = json.loads(raw_line)
        except json.JSONDecodeError:
            continue
        if event.get("type") == "error":
            failure_reason = str(event.get("message") or "").strip()
        if event.get("type") == "turn.failed":
            payload = event.get("error") or {}
            failure_reason = str(payload.get("message") or failure_reason).strip()
        if event.get("type") == "item.completed":
            item = event.get("item") or {}
            if item.get("type") == "agent_message" and item.get("text"):
                final_text = str(item["text"])
    if not final_text:
        return None, failure_reason or "missing_agent_message"
    try:
        parsed = json.loads(final_text)
    except json.JSONDecodeError:
        return None, "invalid_json_response"
    return parsed if isinstance(parsed, dict) else None, failure_reason


def probe_model(
    *,
    provider: str,
    model: str,
    codex_home: str = "",
    reasoning_effort: str = "",
    ollama_host: str = DEFAULT_OLLAMA_HOST,
) -> dict[str, Any]:
    selected_provider = compact_line(provider or "openai_codex").lower()
    defaults = read_codex_defaults(codex_home)
    selected_model = compact_line(model or defaults["model"])
    selected_home = Path(codex_home) if codex_home else DEFAULT_CODEX_HOME
    auth_path = selected_home / "auth.json"
    config_path = selected_home / "config.toml"
    codex_path = find_codex_executable()

    result = {
        "status": "error",
        "message": "",
        "provider": selected_provider,
        "requestedModel": selected_model,
        "selfReportedModel": "",
        "verifiedAt": _now_iso(),
        "codexPath": codex_path,
        "authPresent": auth_path.exists(),
        "configPresent": config_path.exists(),
    }

    if not codex_path:
        result["message"] = "Codex executable not found."
        return result
    if selected_provider == "openai_codex" and not auth_path.exists():
        result["message"] = "auth.json ausente para o provider OpenAI Codex."
        return result
    if not selected_model:
        result["message"] = "Nenhum model efetivo definido."
        return result

    prompt = (
        "Return only minified JSON with keys ok, provider, requestedModel, selfReportedModel, status. "
        f"Echo requestedModel exactly as '{selected_model}'. "
        f"Provider should be '{selected_provider}'. "
        "Use status='ready'. "
        "For selfReportedModel, say the model you believe is answering, or 'unknown' if unavailable."
    )
    command = [
        codex_path,
        "exec",
        "--json",
        "--ephemeral",
        "--skip-git-repo-check",
        "--sandbox",
        "read-only",
        "--cd",
        str(TOOLS_DIR),
    ]
    if selected_provider == "ollama":
        command.extend(["--oss", "--local-provider", "ollama"])
    if reasoning_effort and selected_provider == "openai_codex":
        command = [codex_path, "-c", f'model_reasoning_effort="{reasoning_effort}"', *command[1:]]
    command.extend(["--model", selected_model, prompt])

    env = os.environ.copy()
    if codex_home:
        env["CODEX_HOME"] = str(selected_home)
    if selected_provider == "ollama":
        env["OLLAMA_HOST"] = compact_line(ollama_host) or DEFAULT_OLLAMA_HOST

    creationflags = 0
    startupinfo = None
    if os.name == "nt":
        creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW

    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=30,
            check=False,
            env=env,
            creationflags=creationflags,
            startupinfo=startupinfo,
        )
    except subprocess.TimeoutExpired:
        result["message"] = "Probe do modelo expirou."
        return result
    except OSError as exc:
        result["message"] = f"Falha ao executar codex: {exc}"
        return result

    parsed, failure = _parse_probe_stdout(completed.stdout)
    if parsed is None:
        stderr = compact_line(completed.stderr.splitlines()[-1] if completed.stderr.strip() else "")
        result["message"] = failure or stderr or "Probe sem resposta valida."
        return result

    result["selfReportedModel"] = compact_line(str(parsed.get("selfReportedModel", "") or ""))
    result["status"] = "ready"
    result["message"] = "Acoplado com sucesso e pronto para uso."
    return result
