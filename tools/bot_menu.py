import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any

try:
    from bot_manager import BotManager, compact_line
    from codex_model_manager import (
        DEFAULT_OLLAMA_HOST,
        PROVIDER_PRESETS,
        REASONING_PRESETS,
        build_openai_model_options,
        build_openrouter_model_options,
        discover_codex_homes,
        discover_ollama_models,
        probe_model,
        read_codex_defaults,
        validation_summary_lines,
    )
except ModuleNotFoundError:
    from tools.bot_manager import BotManager, compact_line
    from tools.codex_model_manager import (
        DEFAULT_OLLAMA_HOST,
        PROVIDER_PRESETS,
        REASONING_PRESETS,
        build_openai_model_options,
        build_openrouter_model_options,
        discover_codex_homes,
        discover_ollama_models,
        probe_model,
        read_codex_defaults,
        validation_summary_lines,
    )


ROOT = Path(__file__).resolve().parent.parent
MAINBOT_SCRIPT = ROOT / "mainbot.py"

PLAYSTYLE_PRESETS = [
    "adaptive pressure with respect for projectile threat",
    "aggressive rushdown with frequent dash checks",
    "defensive spacing with punish focus",
    "counter zoning with slow measured advance",
    "corner pressure first with anti air discipline",
]

COMBAT_PRIORITY_PRESETS = [
    "stay in threatening range",
    "punish visible recovery",
    "respect projectile threat",
    "deny jump ins",
    "escape corner early",
    "hold center stage",
    "bait dodge before commit",
    "force short range scrambles",
    "use projectile only as spacing tool",
]

SKILL_PRESETS = [
    "spacing",
    "anti-air",
    "corner pressure",
    "projectile awareness",
    "whiff punish",
    "dash timing",
    "round reset discipline",
    "ultimate dodge",
]

BEHAVIOR_FLAG_PRESETS = [
    "preferShortPlans",
    "reuseRoundCoaching",
    "reuseSeriesPlanning",
    "shareGameplayConcernsGlobally",
]


def clear_screen() -> None:
    os.system("cls" if os.name == "nt" else "clear")


def pause(message: str = "Pressione Enter para continuar...") -> None:
    input(message)


def parse_csv_list(raw_value: str) -> list[str]:
    return [item for item in (compact_line(part) for part in raw_value.split(",")) if item]


def parse_json_object(raw_value: str) -> dict[str, Any] | None:
    text = raw_value.strip()
    if not text:
        return {}
    try:
        payload = json.loads(text)
    except json.JSONDecodeError:
        return None
    return payload if isinstance(payload, dict) else None


def start_mainbot_window() -> None:
    if os.name != "nt":
        subprocess.Popen([sys.executable, str(MAINBOT_SCRIPT)], cwd=str(ROOT))
        return
    command = (
        f'Start-Process cmd.exe -ArgumentList @("/k", "title MainBot && python ""{MAINBOT_SCRIPT}""") '
        f'-WorkingDirectory "{ROOT}"'
    )
    subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command],
        check=False,
        cwd=str(ROOT),
    )


def stop_mainbot_stack() -> None:
    script = """
        $CurrentPid = $PID
        $ProcessMatch = 'codex_broker\\.py|codex_live_agent\\.py|codex_report_console\\.py|start_codex_report_console\\.ps1|mainbot\\.py|http\\.server 8765|Codex Broker Live Report|Codex Thought HUD'
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
    subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script],
        check=False,
        cwd=str(ROOT),
    )


def validation_badge(profile: dict[str, Any]) -> str:
    validation = profile.get("modelValidation", {}) if isinstance(profile.get("modelValidation"), dict) else {}
    status = str(validation.get("status", "unvalidated") or "unvalidated").lower()
    if status == "ready":
        return "\x1b[92mREADY\x1b[0m"
    if status == "error":
        return "\x1b[91mERROR\x1b[0m"
    return "\x1b[93mUNVALIDATED\x1b[0m"


def effective_model(profile: dict[str, Any]) -> str:
    defaults = read_codex_defaults(str(profile.get("codexHome", "") or ""))
    return str(profile.get("model", "") or defaults.get("model", "") or "-")


def effective_reasoning(profile: dict[str, Any]) -> str:
    defaults = read_codex_defaults(str(profile.get("codexHome", "") or ""))
    return str(profile.get("reasoningEffort", "") or defaults.get("reasoning", "") or "-")


def compact_validation_message(profile: dict[str, Any]) -> str:
    validation = profile.get("modelValidation", {}) if isinstance(profile.get("modelValidation"), dict) else {}
    return str(validation.get("message", "-") or "-")


def choose_single_option(title: str, options: list[tuple[str, str]], current_value: str) -> str | None:
    while True:
        clear_screen()
        print(title)
        print()
        for index, (value, label) in enumerate(options, start=1):
            marker = "*" if value == current_value else " "
            shown_value = value or "<herdar>"
            print(f" {marker} {index}. {label} [{shown_value}]")
        print()
        print("0. Voltar")
        print()
        choice = compact_line(input("Opcao: "))
        if choice == "0":
            return None
        if choice.isdigit():
            index = int(choice)
            if 1 <= index <= len(options):
                return options[index - 1][0]


def choose_multi_option(title: str, options: list[str], current_values: list[str]) -> list[str] | None:
    selected = list(current_values)
    while True:
        clear_screen()
        print(title)
        print()
        for index, value in enumerate(options, start=1):
            marker = "x" if value in selected else " "
            print(f" [{marker}] {index}. {value}")
        print()
        print("Digite o numero para alternar.")
        print("S. Salvar")
        print("0. Voltar")
        print()
        choice = compact_line(input("Opcao: ")).lower()
        if choice == "0":
            return None
        if choice == "s":
            return selected
        if choice.isdigit():
            index = int(choice)
            if 1 <= index <= len(options):
                value = options[index - 1]
                if value in selected:
                    selected.remove(value)
                else:
                    selected.append(value)


def choose_behavior_flags(current_flags: dict[str, Any]) -> dict[str, bool] | None:
    selected = {key: bool(current_flags.get(key, False)) for key in BEHAVIOR_FLAG_PRESETS}
    while True:
        clear_screen()
        print("Behavior Flags")
        print()
        for index, key in enumerate(BEHAVIOR_FLAG_PRESETS, start=1):
            marker = "ON" if selected.get(key, False) else "OFF"
            print(f" {index}. {key}: {marker}")
        print()
        print("Digite o numero para alternar.")
        print("S. Salvar")
        print("0. Voltar")
        print()
        choice = compact_line(input("Opcao: ")).lower()
        if choice == "0":
            return None
        if choice == "s":
            return selected
        if choice.isdigit():
            index = int(choice)
            if 1 <= index <= len(BEHAVIOR_FLAG_PRESETS):
                key = BEHAVIOR_FLAG_PRESETS[index - 1]
                selected[key] = not selected.get(key, False)


def format_list(values: Any) -> str:
    if not isinstance(values, list) or not values:
        return "-"
    return ", ".join(str(item) for item in values)


def print_slot_summary(manager: BotManager, slot_id: int) -> None:
    assignment = manager.get_assignment(slot_id)
    profile = manager.get_profile(assignment["botId"])
    status = "ON" if assignment.get("enabled", True) else "OFF"
    print(f"Slot {slot_id} [{status}]")
    print(f"  bot: {profile.get('displayName', assignment['botId'])} ({assignment['botId']})")
    print(f"  provider: {profile.get('provider', 'openai_codex')}")
    print(f"  model: {effective_model(profile)}")
    print(f"  validation: {validation_badge(profile)}")
    print(f"  msg: {compact_validation_message(profile)}")


def show_bot_brief(profile: dict[str, Any]) -> None:
    print(f"Bot: {profile.get('displayName')} ({profile.get('botId')})")
    print(f"Provider: {profile.get('provider', 'openai_codex')}")
    print(f"Model: {effective_model(profile)}")
    print(f"Reasoning: {effective_reasoning(profile)}")
    print(f"Validation: {validation_badge(profile)}")
    print(f"Status: {compact_validation_message(profile)}")


def show_overview(manager: BotManager) -> None:
    manager.reload()
    clear_screen()
    print("Bot Manager")
    print()
    print_slot_summary(manager, 1)
    print()
    print_slot_summary(manager, 2)
    print()


def create_bot(manager: BotManager) -> None:
    clear_screen()
    print("Criar bot")
    print()
    bot_id = compact_line(input("botId: "))
    if not bot_id:
        print("botId vazio.")
        pause()
        return
    display_name = compact_line(input("Nome de exibicao: "))
    profile = manager.create_bot(bot_id, display_name=display_name)
    print(f"Bot criado: {profile['botId']}")
    pause()


def choose_bot(manager: BotManager, *, title: str = "Selecionar bot") -> str | None:
    clear_screen()
    print(title)
    print()
    profiles = manager.list_profiles()
    for index, profile in enumerate(profiles, start=1):
        print(
            f"  {index}. {profile.get('displayName')} ({profile.get('botId')}) | "
            f"{profile.get('provider', 'openai_codex')} | {effective_model(profile)} | {validation_badge(profile)}"
        )
    print()
    print("N. Criar novo bot")
    print("0. Voltar")
    print()
    raw_value = compact_line(input("Opcao: ")).lower()
    if raw_value == "0":
        return None
    if raw_value == "n":
        create_bot(manager)
        manager.reload()
        return choose_bot(manager, title=title)
    if raw_value.isdigit():
        index = int(raw_value)
        if 1 <= index <= len(profiles):
            return str(profiles[index - 1].get("botId"))
        return None
    return raw_value or None


def run_model_validation(profile: dict[str, Any], *, provider: str | None = None, model: str | None = None) -> dict[str, Any]:
    selected_provider = provider or str(profile.get("provider", "openai_codex") or "openai_codex")
    selected_model = model if model is not None else str(profile.get("model", "") or "")
    validation = probe_model(
        provider=selected_provider,
        model=selected_model,
        codex_home=str(profile.get("codexHome", "") or ""),
        reasoning_effort=str(profile.get("reasoningEffort", "") or ""),
        ollama_host=str(profile.get("ollamaHost", "") or DEFAULT_OLLAMA_HOST),
        openrouter_api_key_env_var=str(profile.get("openRouterApiKeyEnvVar", "") or "OPENROUTER_API_KEY"),
        openrouter_base_url=str(profile.get("openRouterBaseUrl", "") or "https://openrouter.ai/api/v1"),
    )
    clear_screen()
    print("Resultado da validacao")
    print()
    for line in validation_summary_lines(validation):
        print(f"- {line}")
    print()
    return validation


def edit_provider(profile: dict[str, Any]) -> dict[str, Any] | None:
    current = str(profile.get("provider", "openai_codex") or "openai_codex")
    selected = choose_single_option("Selecionar provider", PROVIDER_PRESETS, current)
    if selected is None:
        return None
    patch: dict[str, Any] = {
        "provider": selected,
        "model": "",
        "modelValidation": {
            "status": "unvalidated",
            "message": "Provider changed; validation required.",
            "verifiedAt": "",
            "provider": selected,
            "requestedModel": "",
            "selfReportedModel": "",
        },
    }
    if selected == "ollama" and not str(profile.get("ollamaHost", "") or "").strip():
        patch["ollamaHost"] = DEFAULT_OLLAMA_HOST
    if selected == "openrouter":
        patch["openRouterApiKeyEnvVar"] = str(profile.get("openRouterApiKeyEnvVar", "") or "OPENROUTER_API_KEY")
        patch["openRouterBaseUrl"] = str(profile.get("openRouterBaseUrl", "") or "https://openrouter.ai/api/v1")
        patch["openRouterAppName"] = str(profile.get("openRouterAppName", "") or "The Last Arrow Bot Arena")
    return patch


def edit_model(profile: dict[str, Any]) -> dict[str, Any] | None:
    provider = str(profile.get("provider", "openai_codex") or "openai_codex")
    if provider == "ollama":
        options = discover_ollama_models(str(profile.get("ollamaHost", "") or DEFAULT_OLLAMA_HOST))
        if not options:
            clear_screen()
            print("Nenhum modelo do Ollama encontrado.")
            print()
            print(f"Host atual: {profile.get('ollamaHost', DEFAULT_OLLAMA_HOST) or DEFAULT_OLLAMA_HOST}")
            pause()
            return None
    elif provider == "openrouter":
        options = build_openrouter_model_options()
    else:
        options = build_openai_model_options(str(profile.get("codexHome", "") or ""))
    current = str(profile.get("model", "") or "")
    selected = choose_single_option(f"Selecionar model para {provider}", options, current)
    if selected is None:
        return None
    preview = dict(profile)
    preview["provider"] = provider
    preview["model"] = selected
    validation = run_model_validation(preview, provider=provider, model=selected)
    confirm = compact_line(input("Salvar esse model com esse status? (s/n): ")).lower()
    if confirm != "s":
        return None
    return {"model": selected, "modelValidation": validation}


def edit_reasoning(profile: dict[str, Any]) -> dict[str, Any] | None:
    current = str(profile.get("reasoningEffort", "") or "")
    selected = choose_single_option("Selecionar reasoningEffort", REASONING_PRESETS, current)
    if selected is None:
        return None
    return {
        "reasoningEffort": selected,
        "modelValidation": {
            "status": "unvalidated",
            "message": "Reasoning changed; validation required.",
            "verifiedAt": "",
            "provider": str(profile.get("provider", "openai_codex") or "openai_codex"),
            "requestedModel": str(profile.get("model", "") or ""),
            "selfReportedModel": "",
        },
    }


def edit_codex_home(profile: dict[str, Any]) -> dict[str, Any] | None:
    current = str(profile.get("codexHome", "") or "")
    selected = choose_single_option("Selecionar codexHome", discover_codex_homes(), current)
    if selected is None:
        return None
    return {
        "codexHome": selected,
        "modelValidation": {
            "status": "unvalidated",
            "message": "Codex home changed; validation required.",
            "verifiedAt": "",
            "provider": str(profile.get("provider", "openai_codex") or "openai_codex"),
            "requestedModel": str(profile.get("model", "") or ""),
            "selfReportedModel": "",
        },
    }


def edit_ollama_host(profile: dict[str, Any]) -> dict[str, Any] | None:
    clear_screen()
    print("Editar ollamaHost")
    print()
    print(f"Atual: {profile.get('ollamaHost', DEFAULT_OLLAMA_HOST) or DEFAULT_OLLAMA_HOST}")
    raw_value = compact_line(input("Novo host Ollama: "))
    selected = raw_value or DEFAULT_OLLAMA_HOST
    return {
        "ollamaHost": selected,
        "modelValidation": {
            "status": "unvalidated",
            "message": "Ollama host changed; validation required.",
            "verifiedAt": "",
            "provider": str(profile.get("provider", "ollama") or "ollama"),
            "requestedModel": str(profile.get("model", "") or ""),
            "selfReportedModel": "",
        },
    }


def edit_play_style(profile: dict[str, Any]) -> dict[str, Any] | None:
    options = [(value, value) for value in PLAYSTYLE_PRESETS]
    current = str(profile.get("playStyle", "") or "")
    selected = choose_single_option("Selecionar playStyle", options, current)
    if selected is None:
        return None
    return {"playStyle": selected}


def edit_priority_list(profile: dict[str, Any]) -> dict[str, Any] | None:
    current = [str(item) for item in profile.get("combatPriorities", []) if str(item).strip()]
    selected = choose_multi_option("Selecionar combatPriorities", COMBAT_PRIORITY_PRESETS, current)
    if selected is None:
        return None
    return {"combatPriorities": selected}


def edit_skill_list(profile: dict[str, Any]) -> dict[str, Any] | None:
    current = [str(item) for item in profile.get("skills", []) if str(item).strip()]
    selected = choose_multi_option("Selecionar skills", SKILL_PRESETS, current)
    if selected is None:
        return None
    return {"skills": selected}


def edit_behavior_flags(profile: dict[str, Any]) -> dict[str, Any] | None:
    current = profile.get("behaviorFlags", {}) if isinstance(profile.get("behaviorFlags"), dict) else {}
    payload = choose_behavior_flags(current)
    if payload is None:
        return None
    return {"behaviorFlags": payload}


def edit_character_preferences(profile: dict[str, Any]) -> dict[str, Any] | None:
    clear_screen()
    print("Editar characterPreferences")
    print()
    current = profile.get("characterPreferences", {}) if isinstance(profile.get("characterPreferences"), dict) else {}
    print(json.dumps(current, indent=2, ensure_ascii=True))
    print()
    raw_value = input("Novo JSON object: ")
    payload = parse_json_object(raw_value)
    if payload is None:
        print("JSON invalido.")
        pause()
        return None
    return {"characterPreferences": payload}


def manage_advanced_settings(profile: dict[str, Any]) -> dict[str, Any] | None:
    while True:
        clear_screen()
        print("Configuracoes avancadas")
        print()
        print(f"1. playStyle: {profile.get('playStyle', '')}")
        print(f"2. combatPriorities: {format_list(profile.get('combatPriorities'))}")
        print(f"3. skills: {format_list(profile.get('skills'))}")
        print(f"4. notes: {format_list(profile.get('notes'))}")
        print("5. behaviorFlags")
        print("6. characterPreferences")
        print(f"7. personaSummary: {profile.get('personaSummary', '')}")
        print("0. Voltar")
        print()
        choice = compact_line(input("Opcao: "))
        if choice == "0":
            return None
        if choice == "1":
            return edit_play_style(profile)
        if choice == "2":
            return edit_priority_list(profile)
        if choice == "3":
            return edit_skill_list(profile)
        if choice == "4":
            return {"notes": parse_csv_list(input("Novo valor (separado por virgula): "))}
        if choice == "5":
            return edit_behavior_flags(profile)
        if choice == "6":
            return edit_character_preferences(profile)
        if choice == "7":
            return {"personaSummary": input("Nova personaSummary: ").strip()}


def manage_bot_profile(manager: BotManager, bot_id: str | None = None) -> None:
    if not bot_id:
        bot_id = choose_bot(manager)
    if not bot_id:
        return

    while True:
        manager.reload()
        profile = manager.get_profile(bot_id)
        clear_screen()
        print("Configurar bot")
        print()
        show_bot_brief(profile)
        print(f"Codex home: {profile.get('codexHome', '') or '<herdar>'}")
        if str(profile.get("provider", "openai_codex")) == "ollama":
            print(f"Ollama host: {profile.get('ollamaHost', DEFAULT_OLLAMA_HOST) or DEFAULT_OLLAMA_HOST}")
        if str(profile.get("provider", "openai_codex")) == "openrouter":
            print(f"OpenRouter key env: {profile.get('openRouterApiKeyEnvVar', 'OPENROUTER_API_KEY') or 'OPENROUTER_API_KEY'}")
            print(f"OpenRouter base: {profile.get('openRouterBaseUrl', 'https://openrouter.ai/api/v1') or 'https://openrouter.ai/api/v1'}")
        print()
        print("1. Provider")
        print("2. Model")
        print("3. Validar model agora")
        print("4. Reasoning")
        print("5. Codex home")
        if str(profile.get("provider", "openai_codex")) == "ollama":
            print("6. Ollama host")
            print("7. Nome do bot")
            print("8. Ajustes avancados")
        else:
            print("6. Nome do bot")
            print("7. Ajustes avancados")
        print("0. Voltar")
        print()
        choice = compact_line(input("Opcao: "))

        patch: dict[str, Any] | None = None
        if choice == "0":
            return
        if choice == "1":
            patch = edit_provider(profile)
        elif choice == "2":
            patch = edit_model(profile)
        elif choice == "3":
            patch = {"modelValidation": run_model_validation(profile)}
        elif choice == "4":
            patch = edit_reasoning(profile)
        elif choice == "5":
            patch = edit_codex_home(profile)
        elif choice == "6":
            if str(profile.get("provider", "openai_codex")) == "ollama":
                patch = edit_ollama_host(profile)
            else:
                patch = {"displayName": input("Novo nome: ").strip()}
        elif choice == "7":
            if str(profile.get("provider", "openai_codex")) == "ollama":
                patch = {"displayName": input("Novo nome: ").strip()}
            else:
                patch = manage_advanced_settings(profile)
        elif choice == "8" and str(profile.get("provider", "openai_codex")) == "ollama":
            patch = manage_advanced_settings(profile)

        if patch is not None:
            manager.update_profile(bot_id, patch)


def manage_slot(manager: BotManager, slot_id: int) -> None:
    while True:
        manager.reload()
        assignment = manager.get_assignment(slot_id)
        profile = manager.get_profile(assignment["botId"])
        clear_screen()
        print(f"Configurar Slot {slot_id}")
        print()
        print_slot_summary(manager, slot_id)
        print()
        print("1. Trocar bot")
        print("2. Ativar / desativar slot")
        print("3. Configurar bot atual")
        print("0. Voltar")
        print()
        choice = compact_line(input("Opcao: "))
        if choice == "0":
            return
        if choice == "1":
            bot_id = choose_bot(manager, title=f"Selecionar bot para Slot {slot_id}")
            if bot_id:
                manager.assign_bot(slot_id, bot_id, enabled=True)
        elif choice == "2":
            manager.set_slot_enabled(slot_id, not bool(assignment.get("enabled", True)))
        elif choice == "3":
            manage_bot_profile(manager, bot_id=str(profile.get("botId")))


def main() -> int:
    manager = BotManager()
    while True:
        show_overview(manager)
        print("1. Iniciar MainBot")
        print("2. Parar MainBot")
        print("3. Configurar Slot 1")
        print("4. Configurar Slot 2")
        print("5. Criar bot")
        print("6. Configurar bot")
        print("7. Recarregar")
        print("0. Sair")
        print()
        choice = compact_line(input("Opcao: "))
        if choice == "1":
            start_mainbot_window()
            pause()
        elif choice == "2":
            stop_mainbot_stack()
            pause()
        elif choice == "3":
            manage_slot(manager, 1)
        elif choice == "4":
            manage_slot(manager, 2)
        elif choice == "5":
            create_bot(manager)
        elif choice == "6":
            manage_bot_profile(manager)
        elif choice == "7":
            manager.reload()
        elif choice == "0":
            return 0


if __name__ == "__main__":
    raise SystemExit(main())
