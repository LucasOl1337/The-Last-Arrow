using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    /// <summary>
    /// Adiciona o menu "Bot Arena" na barra superior do Unity.
    /// Permite iniciar/parar os bots sem abrir PowerShell manualmente.
    /// </summary>
    public static class BotArenaLauncher
    {
        // Caminho raiz do projeto (pai da pasta Assets)
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        // Caminho da pasta tools/
        private static string ToolsDir => Path.Combine(ProjectRoot, "tools");

        // ─────────────────────────────────────────────
        //  Menu: Bot Arena > Abrir Bot Menu (interativo)
        // ─────────────────────────────────────────────
        [MenuItem("Bot Arena/🤖 Abrir Bot Menu (interativo)", priority = 1)]
        public static void OpenBotMenu()
        {
            string script = Path.Combine(ToolsDir, "bot_menu.py");

            if (!File.Exists(script))
            {
                UnityEngine.Debug.LogError($"<color=red>[Bot Arena]</color> bot_menu.py não encontrado em: {script}");
                EditorUtility.DisplayDialog(
                    "Bot Arena – Erro",
                    $"bot_menu.py não encontrado em:\n{script}",
                    "OK");
                return;
            }

            // Abre uma janela cmd interativa (como se você tivesse rodado python bot_menu.py)
            var psi = new ProcessStartInfo
            {
                FileName               = "cmd.exe",
                Arguments              = $"/k \"title Bot Menu && python \"{script}\"\"",
                WorkingDirectory       = ProjectRoot,
                UseShellExecute        = true,   // necessário para abrir janela visível
                CreateNoWindow         = false,
            };

            Process.Start(psi);
            UnityEngine.Debug.Log("<color=cyan>[Bot Arena]</color> Bot Menu iniciado numa janela CMD.");
        }

        // ─────────────────────────────────────────────
        //  Menu: Bot Arena > Iniciar MainBot direto
        // ─────────────────────────────────────────────
        [MenuItem("Bot Arena/▶ Iniciar MainBot direto", priority = 2)]
        public static void StartMainBot()
        {
            string script = Path.Combine(ProjectRoot, "mainbot.py");

            if (!File.Exists(script))
            {
                UnityEngine.Debug.LogError($"<color=red>[Bot Arena]</color> mainbot.py não encontrado em: {script}");
                EditorUtility.DisplayDialog(
                    "Bot Arena – Erro",
                    $"mainbot.py não encontrado em:\n{script}",
                    "OK");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName         = "cmd.exe",
                Arguments        = $"/k \"title MainBot && python \"{script}\"\"",
                WorkingDirectory = ProjectRoot,
                UseShellExecute  = true,
                CreateNoWindow   = false,
            };

            Process.Start(psi);
            UnityEngine.Debug.Log("<color=green>[Bot Arena]</color> MainBot iniciado numa janela CMD.");
        }

        // ─────────────────────────────────────────────
        //  Menu: Bot Arena > Parar todos os bots
        // ─────────────────────────────────────────────
        [MenuItem("Bot Arena/⏹ Parar todos os bots", priority = 3)]
        public static void StopAllBots()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Bot Arena – Parar bots",
                "Tem certeza que quer encerrar todos os processos de bot?",
                "Sim, parar tudo",
                "Cancelar");

            if (!confirmed) return;

            string killScript = @"
                $ProcessMatch = 'codex_broker\.py|codex_live_agent\.py|codex_report_console\.py|mainbot\.py|http\.server 8765'
                Get-CimInstance Win32_Process |
                    Where-Object { $_.CommandLine -match $ProcessMatch } |
                    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

                Get-NetTCPConnection -LocalPort 8765 -State Listen -ErrorAction SilentlyContinue |
                    Select-Object -ExpandProperty OwningProcess -Unique |
                    Where-Object { $_ -gt 0 } |
                    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
            ";

            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NoProfile -ExecutionPolicy Bypass -Command \"{killScript.Replace("\"", "\\\"")}\"",
                WorkingDirectory       = ProjectRoot,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);

            UnityEngine.Debug.Log("<color=orange>[Bot Arena]</color> Todos os processos de bot foram encerrados.");
            EditorUtility.DisplayDialog("Bot Arena", "Todos os bots foram encerrados.", "OK");
        }

        // ─────────────────────────────────────────────
        //  Menu: Bot Arena > Abrir pasta tools/
        // ─────────────────────────────────────────────
        [MenuItem("Bot Arena/📁 Abrir pasta tools/", priority = 20)]
        public static void OpenToolsFolder()
        {
            Process.Start("explorer.exe", ToolsDir);
        }
    }
}
