using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpWaifu2xSpriteUpgradeTools
    {
        private const string SettingsEditorPrefsKey = "ProjectPVP.Waifu2x.Settings";
        private const string DefaultArgumentsTemplate =
            "-m waifu2x.cli --input {input} --output {output} --method {method} {noise_args} --gpu {gpu} --batch-size {batch_size} --tile-size {tile_size} --style {style}";
        private const string DefaultStyle = "art";
        private const int DefaultBatchSize = 4;
        private const int DefaultTileSize = 256;
        private const string DefaultGpu = "0";

        [Serializable]
        private sealed class Waifu2xSettingsData
        {
            public string executablePath = string.Empty;
            public string workingDirectory = string.Empty;
            public string argumentsTemplate = DefaultArgumentsTemplate;
            public int scale = 2;
            public int noise = 1;
            public string gpu = DefaultGpu;
            public int batchSize = DefaultBatchSize;
            public int tileSize = DefaultTileSize;
            public string style = DefaultStyle;
        }

        [MenuItem("ProjectPVP/Characters/Configure Waifu2x")]
        private static void OpenSettingsWindow()
        {
            ProjectPvpWaifu2xSettingsWindow.ShowWindow();
        }

        [MenuItem("ProjectPVP/Characters/Upscale Selected Character With Waifu2x", true)]
        private static bool ValidateUpscaleSelectedCharacterWithWaifu2x()
        {
            return Selection.activeObject is CharacterDefinition;
        }

        [MenuItem("ProjectPVP/Characters/Upscale Selected Character With Waifu2x")]
        private static void UpscaleSelectedCharacterWithWaifu2x()
        {
            if (Selection.activeObject is not CharacterDefinition definition)
            {
                Debug.LogWarning("ProjectPVP: selecione um CharacterDefinition para passar no waifu2x.");
                return;
            }

            if (TryUpscaleCharacterSprites(definition, out string summary))
            {
                Debug.Log(summary);
                return;
            }

            Debug.LogWarning(summary);
        }

        [MenuItem("ProjectPVP/Characters/Upscale All Character Sprites With Waifu2x")]
        private static void UpscaleAllCharactersWithWaifu2x()
        {
            int successCount = 0;
            int failedCount = 0;

            foreach (CharacterDefinition definition in ProjectPvpCharacterAssetPaths.EnumerateDefinitions())
            {
                if (TryUpscaleCharacterSprites(definition, out string summary))
                {
                    successCount += 1;
                    Debug.Log(summary);
                }
                else
                {
                    failedCount += 1;
                    Debug.LogWarning(summary);
                }
            }

            Debug.Log("ProjectPVP: lote waifu2x concluido. Sucessos: " + successCount + ". Falhas: " + failedCount + ".");
        }

        internal static bool TryUpscaleCharacterSprites(CharacterDefinition definition, out string summary)
        {
            summary = "ProjectPVP: nao foi possivel processar o personagem com waifu2x.";
            if (definition == null)
            {
                summary = "ProjectPVP: CharacterDefinition nulo.";
                return false;
            }

            if (!ProjectPvpCharacterAssetPaths.TryGetCharacterRoot(definition, out string characterRootAssetPath))
            {
                summary = "ProjectPVP: nao foi possivel localizar a pasta raiz do personagem.";
                return false;
            }

            string characterRootFullPath = ProjectPvpCharacterAssetPaths.ToFullPath(characterRootAssetPath);
            if (string.IsNullOrWhiteSpace(characterRootFullPath) || !Directory.Exists(characterRootFullPath))
            {
                summary = "ProjectPVP: pasta fisica do personagem nao encontrada.";
                return false;
            }

            string[] pngFiles = CollectCharacterPngFiles(characterRootFullPath);
            if (pngFiles.Length == 0)
            {
                summary = "ProjectPVP: nenhum PNG encontrado para processar em " + characterRootAssetPath + ".";
                return false;
            }

            if (!TryLoadReadySettings(out Waifu2xSettingsData settings, out string settingsSummary))
            {
                summary = settingsSummary;
                return false;
            }

            string safeCharacterName = BuildSafeFileName(!string.IsNullOrWhiteSpace(definition.id) ? definition.id : definition.displayName);
            string runStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string tempOutputRoot = Path.Combine(Path.GetTempPath(), "ProjectPVP", "Waifu2x", safeCharacterName + "-" + runStamp);
            Directory.CreateDirectory(tempOutputRoot);

            try
            {
                string method;
                try
                {
                    method = ResolveNunifMethod(settings.scale, settings.noise);
                }
                catch (ArgumentException exception)
                {
                    summary = "ProjectPVP: configuracao de metodo invalida. " + exception.Message;
                    return false;
                }

                string noiseArguments = BuildNunifNoiseArguments(settings.noise);
                var stagedOutputs = new List<(string sourceFile, string outputFile)>(pngFiles.Length);

                for (int index = 0; index < pngFiles.Length; index += 1)
                {
                    string sourceFile = pngFiles[index];
                    string relativePath = Path.GetRelativePath(characterRootFullPath, sourceFile);
                    string outputFile = Path.Combine(tempOutputRoot, relativePath);
                    string outputDirectory = Path.GetDirectoryName(outputFile);
                    if (!string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    string arguments;
                    try
                    {
                        arguments = ExpandArgumentTemplate(
                            settings.argumentsTemplate,
                            sourceFile,
                            outputFile,
                            settings.scale,
                            settings.noise,
                            method,
                            noiseArguments,
                            settings.gpu,
                            settings.batchSize,
                            settings.tileSize,
                            settings.style);
                    }
                    catch (ArgumentException exception)
                    {
                        summary = "ProjectPVP: configuracao do waifu2x invalida. " + exception.Message;
                        return false;
                    }

                    if (!TryRunWaifu2x(settings.executablePath, settings.workingDirectory, arguments, out string processSummary))
                    {
                        summary = "ProjectPVP: waifu2x falhou em " + relativePath.Replace("\\", "/") + ". " + processSummary;
                        return false;
                    }

                    if (!File.Exists(outputFile))
                    {
                        summary = "ProjectPVP: waifu2x terminou sem gerar o arquivo esperado para " + relativePath.Replace("\\", "/") + ".";
                        return false;
                    }

                    stagedOutputs.Add((sourceFile, outputFile));
                }

                string backupZipPath = CreateCharacterBackup(characterRootFullPath, safeCharacterName, runStamp);
                for (int index = 0; index < stagedOutputs.Count; index += 1)
                {
                    (string sourceFile, string outputFile) = stagedOutputs[index];
                    File.Copy(outputFile, sourceFile, overwrite: true);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                int optimizedCount = ProjectPvpCharacterSpriteImportTools.OptimizeSpriteImportsInFolders(characterRootAssetPath);
                bool rebuilt = ProjectPvpCharacterAnimationSync.RebuildFromFolders(definition, out string rebuildSummary);

                summary = "ProjectPVP: waifu2x concluido para "
                    + definition.displayName
                    + ". PNGs processados: "
                    + stagedOutputs.Count
                    + ". Metodo: "
                    + method
                    + ". GPU: "
                    + settings.gpu
                    + ". Batch size: "
                    + settings.batchSize
                    + ". Tile size: "
                    + settings.tileSize
                    + ". Backup: "
                    + backupZipPath.Replace("\\", "/")
                    + ". Sprites reimportados: "
                    + optimizedCount
                    + ". "
                    + rebuildSummary;
                return rebuilt;
            }
            finally
            {
                TryDeleteDirectory(tempOutputRoot);
            }
        }

        private static bool TryLoadReadySettings(out Waifu2xSettingsData settings, out string summary)
        {
            settings = LoadSettings();
            ApplyDefaults(settings);
            TryApplyDetectedNunifEnvironment(settings);

            if (!File.Exists(settings.executablePath))
            {
                string selectedExecutablePath = EditorUtility.OpenFilePanel(
                    "Selecione o Python do ambiente do nunif",
                    Path.GetDirectoryName(settings.executablePath) ?? string.Empty,
                    "exe");

                if (string.IsNullOrWhiteSpace(selectedExecutablePath))
                {
                    summary = "ProjectPVP: nenhum Python do ambiente do nunif foi configurado. Abra ProjectPVP/Characters/Configure Waifu2x para salvar isso depois.";
                    ProjectPvpWaifu2xSettingsWindow.ShowWindow();
                    return false;
                }

                settings.executablePath = selectedExecutablePath;
                if (string.IsNullOrWhiteSpace(settings.workingDirectory))
                {
                    settings.workingDirectory = EditorUtility.OpenFolderPanel("Selecione a pasta raiz do repo nunif", string.Empty, string.Empty);
                }

                SaveSettings(settings);
            }

            if (string.IsNullOrWhiteSpace(settings.workingDirectory) || !Directory.Exists(settings.workingDirectory))
            {
                string selectedWorkingDirectory = EditorUtility.OpenFolderPanel("Selecione a pasta raiz do repo nunif", string.Empty, string.Empty);
                if (string.IsNullOrWhiteSpace(selectedWorkingDirectory))
                {
                    summary = "ProjectPVP: a pasta raiz do nunif nao foi configurada.";
                    ProjectPvpWaifu2xSettingsWindow.ShowWindow();
                    return false;
                }

                settings.workingDirectory = selectedWorkingDirectory;
                SaveSettings(settings);
            }

            summary = string.Empty;
            return true;
        }

        private static void ApplyDefaults(Waifu2xSettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.argumentsTemplate))
            {
                settings.argumentsTemplate = DefaultArgumentsTemplate;
            }

            if (string.IsNullOrWhiteSpace(settings.gpu))
            {
                settings.gpu = DefaultGpu;
            }

            if (settings.batchSize <= 0)
            {
                settings.batchSize = DefaultBatchSize;
            }

            if (settings.tileSize <= 0)
            {
                settings.tileSize = DefaultTileSize;
            }

            if (string.IsNullOrWhiteSpace(settings.style))
            {
                settings.style = DefaultStyle;
            }

            if (settings.scale != 1 && settings.scale != 2 && settings.scale != 4)
            {
                settings.scale = settings.scale >= 4 ? 4 : 2;
            }

            if (settings.noise < -1)
            {
                settings.noise = -1;
            }

            if (settings.noise > 3)
            {
                settings.noise = 3;
            }
        }

        private static void TryApplyDetectedNunifEnvironment(Waifu2xSettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            string detectedPythonPath = GetDefaultNunifPythonPath();
            string detectedWorkingDirectory = GetDefaultNunifWorkingDirectory();

            if (File.Exists(detectedPythonPath) && !File.Exists(settings.executablePath))
            {
                settings.executablePath = detectedPythonPath;
            }

            if (Directory.Exists(detectedWorkingDirectory) && !Directory.Exists(settings.workingDirectory))
            {
                settings.workingDirectory = detectedWorkingDirectory;
            }
        }

        private static string CreateCharacterBackup(string characterRootFullPath, string safeCharacterName, string runStamp)
        {
            string backupDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? Path.GetTempPath(), "Temp", "Waifu2xBackups");
            Directory.CreateDirectory(backupDirectory);

            string backupZipPath = Path.Combine(backupDirectory, safeCharacterName + "-" + runStamp + ".zip");
            if (File.Exists(backupZipPath))
            {
                File.Delete(backupZipPath);
            }

            ZipFile.CreateFromDirectory(characterRootFullPath, backupZipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);
            return backupZipPath;
        }

        private static bool TryRunWaifu2x(string executablePath, string workingDirectory, string arguments, out string summary)
        {
            summary = "ProjectPVP: falha ao executar o waifu2x.";

            try
            {
                string resolvedWorkingDirectory = !string.IsNullOrWhiteSpace(workingDirectory)
                    ? workingDirectory
                    : Path.GetDirectoryName(executablePath) ?? string.Empty;

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = resolvedWorkingDirectory,
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    summary = "ProjectPVP: nao foi possivel iniciar o processo do waifu2x.";
                    return false;
                }

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    summary = string.Empty;
                    return true;
                }

                string detail = !string.IsNullOrWhiteSpace(standardError) ? standardError : standardOutput;
                detail = CollapseWhitespace(detail);
                summary = "ProjectPVP: waifu2x saiu com codigo " + process.ExitCode + "." + (string.IsNullOrWhiteSpace(detail) ? string.Empty : " " + detail);
                return false;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is IOException)
            {
                summary = "ProjectPVP: erro ao executar o waifu2x. " + exception.Message;
                return false;
            }
        }

        private static Waifu2xSettingsData LoadSettings()
        {
            string json = EditorPrefs.GetString(SettingsEditorPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Waifu2xSettingsData();
            }

            try
            {
                Waifu2xSettingsData loaded = JsonUtility.FromJson<Waifu2xSettingsData>(json);
                return loaded ?? new Waifu2xSettingsData();
            }
            catch (ArgumentException)
            {
                return new Waifu2xSettingsData();
            }
        }

        private static void SaveSettings(Waifu2xSettingsData settings)
        {
            EditorPrefs.SetString(SettingsEditorPrefsKey, JsonUtility.ToJson(settings ?? new Waifu2xSettingsData()));
        }

        private static string[] CollectCharacterPngFiles(string characterRootFullPath)
        {
            if (string.IsNullOrWhiteSpace(characterRootFullPath) || !Directory.Exists(characterRootFullPath))
            {
                return Array.Empty<string>();
            }

            string[] pngFiles = Directory.GetFiles(characterRootFullPath, "*.png", SearchOption.AllDirectories);
            Array.Sort(pngFiles, StringComparer.OrdinalIgnoreCase);
            return pngFiles;
        }

        private static string ResolveNunifMethod(int scale, int noise)
        {
            if (scale >= 4)
            {
                return noise >= 0 ? "noise_scale4x" : "scale4x";
            }

            if (scale >= 2)
            {
                return noise >= 0 ? "noise_scale" : "scale";
            }

            if (noise >= 0)
            {
                return "noise";
            }

            throw new ArgumentException("Nunif nao suporta scale 1 sem noise. Use scale 2/4 ou habilite denoise.");
        }

        private static string BuildNunifNoiseArguments(int noise)
        {
            return noise >= 0
                ? "--noise-level " + noise.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string ExpandArgumentTemplate(
            string template,
            string inputFilePath,
            string outputFilePath,
            int scale,
            int noise,
            string method,
            string noiseArguments,
            string gpu,
            int batchSize,
            int tileSize,
            string style)
        {
            string normalizedTemplate = string.IsNullOrWhiteSpace(template)
                ? DefaultArgumentsTemplate
                : template.Trim();

            bool containsInput = normalizedTemplate.Contains("{input}", StringComparison.Ordinal);
            bool containsOutput = normalizedTemplate.Contains("{output}", StringComparison.Ordinal);
            if (!containsInput || !containsOutput)
            {
                throw new ArgumentException("O template precisa conter {input} e {output}.");
            }

            return CollapseWhitespace(
                normalizedTemplate
                    .Replace("{input}", QuoteArgument(inputFilePath), StringComparison.Ordinal)
                    .Replace("{output}", QuoteArgument(outputFilePath), StringComparison.Ordinal)
                    .Replace("{scale}", scale.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{noise}", noise.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{method}", method ?? string.Empty, StringComparison.Ordinal)
                    .Replace("{noise_args}", noiseArguments ?? string.Empty, StringComparison.Ordinal)
                    .Replace("{gpu}", gpu ?? string.Empty, StringComparison.Ordinal)
                    .Replace("{batch_size}", batchSize.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{tile_size}", tileSize.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{style}", style ?? string.Empty, StringComparison.Ordinal));
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", string.Empty) + "\"";
        }

        private static string CollapseWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string[] parts = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        private static string BuildSafeFileName(string value)
        {
            string fallback = "character";
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            var builder = new System.Text.StringBuilder(value.Length);

            for (int index = 0; index < value.Length; index += 1)
            {
                char character = value[index];
                if (Array.IndexOf(invalidCharacters, character) >= 0)
                {
                    builder.Append('_');
                    continue;
                }

                builder.Append(char.IsWhiteSpace(character) ? '-' : character);
            }

            string sanitized = builder.ToString().Trim('-', '_', '.');
            return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
        }

        private static string GetDefaultNunifRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectPVP", "nunif-waifu2x");
        }

        private static string GetDefaultNunifPythonPath()
        {
            return Path.Combine(GetDefaultNunifRoot(), "venv", "Scripts", "python.exe");
        }

        private static string GetDefaultNunifWorkingDirectory()
        {
            return Path.Combine(GetDefaultNunifRoot(), "nunif-src");
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        internal sealed class ProjectPvpWaifu2xSettingsWindow : EditorWindow
        {
            private Waifu2xSettingsData _settings;

            internal static void ShowWindow()
            {
                ProjectPvpWaifu2xSettingsWindow window = GetWindow<ProjectPvpWaifu2xSettingsWindow>("ProjectPVP Waifu2x");
                window.minSize = new Vector2(560f, 300f);
                window.Show();
            }

            private void OnEnable()
            {
                _settings = LoadSettings();
                ApplyDefaults(_settings);
                TryApplyDetectedNunifEnvironment(_settings);
            }

            private void OnGUI()
            {
                _settings ??= LoadSettings();
                ApplyDefaults(_settings);

                EditorGUILayout.LabelField("Waifu2x Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "O preset padrao agora usa o nunif via Python. Aponte para o python da venv e para a pasta raiz do repo nunif.",
                    MessageType.Info);

                EditorGUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                _settings.executablePath = EditorGUILayout.TextField("Python", _settings.executablePath);
                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                {
                    string selectedPath = EditorUtility.OpenFilePanel("Selecione o Python do ambiente do nunif", Path.GetDirectoryName(_settings.executablePath) ?? string.Empty, "exe");
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        _settings.executablePath = selectedPath;
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                _settings.workingDirectory = EditorGUILayout.TextField("Nunif Root", _settings.workingDirectory);
                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                {
                    string selectedDirectory = EditorUtility.OpenFolderPanel("Selecione a pasta raiz do repo nunif", _settings.workingDirectory, string.Empty);
                    if (!string.IsNullOrWhiteSpace(selectedDirectory))
                    {
                        _settings.workingDirectory = selectedDirectory;
                    }
                }

                EditorGUILayout.EndHorizontal();

                _settings.argumentsTemplate = EditorGUILayout.TextField("Args Template", _settings.argumentsTemplate);
                _settings.scale = EditorGUILayout.IntPopup("Scale", _settings.scale, new[] { "1x (noise only)", "2x", "4x" }, new[] { 1, 2, 4 });
                _settings.noise = EditorGUILayout.IntSlider("Noise", Mathf.Clamp(_settings.noise, -1, 3), -1, 3);
                _settings.gpu = EditorGUILayout.TextField("GPU", _settings.gpu);
                _settings.batchSize = EditorGUILayout.IntSlider("Batch Size", Mathf.Clamp(_settings.batchSize, 1, 16), 1, 16);
                _settings.tileSize = EditorGUILayout.IntSlider("Tile Size", Mathf.Clamp(_settings.tileSize, 64, 2048), 64, 2048);
                _settings.style = EditorGUILayout.TextField("Style", _settings.style);

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Placeholders", "{input}, {output}, {scale}, {noise}, {method}, {noise_args}, {gpu}, {batch_size}, {tile_size}, {style}");
                EditorGUILayout.LabelField("Template padrao", DefaultArgumentsTemplate, EditorStyles.miniLabel);

                EditorGUILayout.Space(10f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Use Local Nunif"))
                {
                    _settings.executablePath = GetDefaultNunifPythonPath();
                    _settings.workingDirectory = GetDefaultNunifWorkingDirectory();
                    _settings.argumentsTemplate = DefaultArgumentsTemplate;
                    _settings.gpu = DefaultGpu;
                    _settings.style = DefaultStyle;
                    _settings.batchSize = DefaultBatchSize;
                    _settings.tileSize = DefaultTileSize;
                    ShowNotification(new GUIContent("Preset local aplicado"));
                }

                if (GUILayout.Button("Save"))
                {
                    SaveSettings(_settings);
                    ShowNotification(new GUIContent("Configuracao salva"));
                }

                if (GUILayout.Button("Reset"))
                {
                    _settings = new Waifu2xSettingsData();
                    SaveSettings(_settings);
                    ShowNotification(new GUIContent("Configuracao resetada"));
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
