using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectPvpWaifu2xSpriteUpgradeTests
    {
        private const string EditorAssemblyName = "ProjectPVP.Editor";
        private const string ToolsTypeName = "ProjectPVP.Editor.ProjectPvpWaifu2xSpriteUpgradeTools";

        [Test]
        public void CollectCharacterPngFiles_ReturnsSortedPngPathsAcrossCharacterFolders()
        {
            string tempRoot = CreateTempDirectory();

            try
            {
                string characterRoot = Path.Combine(tempRoot, "StormDragon");
                Directory.CreateDirectory(Path.Combine(characterRoot, "Animations", "dash", "east"));
                Directory.CreateDirectory(Path.Combine(characterRoot, "Rotations"));
                Directory.CreateDirectory(Path.Combine(characterRoot, "Data"));

                File.WriteAllBytes(Path.Combine(characterRoot, "Rotations", "west.png"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(characterRoot, "Animations", "dash", "east", "frame_002.png"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(characterRoot, "Animations", "dash", "east", "frame_001.png"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(characterRoot, "Data", "ignore.asset"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(characterRoot, "Animations", "dash", "east", "frame_001.png.meta"), new byte[] { 1 });

                string[] result = InvokeStringArrayMethod("CollectCharacterPngFiles", characterRoot);

                Assert.That(result, Is.EqualTo(new[]
                {
                    Path.Combine(characterRoot, "Animations", "dash", "east", "frame_001.png"),
                    Path.Combine(characterRoot, "Animations", "dash", "east", "frame_002.png"),
                    Path.Combine(characterRoot, "Rotations", "west.png"),
                }));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [Test]
        public void ExpandArgumentTemplate_ReplacesInputOutputScaleAndNoiseTokens()
        {
            string arguments = InvokeStringMethod(
                "ExpandArgumentTemplate",
                "-i {input} -o {output} -s {scale} -n {noise}",
                @"C:\Sprites\storm dragon.png",
                @"C:\Output\storm dragon-upscaled.png",
                2,
                1);

            Assert.That(arguments, Is.EqualTo("-i \"C:\\Sprites\\storm dragon.png\" -o \"C:\\Output\\storm dragon-upscaled.png\" -s 2 -n 1"));
        }

        [Test]
        public void ResolveNunifMethod_ReturnsNoiseScaleFor2xWhenNoiseIsEnabled()
        {
            string method = InvokeStringMethod("ResolveNunifMethod", 2, 1);

            Assert.That(method, Is.EqualTo("noise_scale"));
        }

        [Test]
        public void ResolveNunifMethod_ReturnsScale4xWhenNoiseIsDisabled()
        {
            string method = InvokeStringMethod("ResolveNunifMethod", 4, -1);

            Assert.That(method, Is.EqualTo("scale4x"));
        }

        [Test]
        public void BuildNunifNoiseArguments_OmitsNoiseLevelWhenNoiseIsDisabled()
        {
            string noiseArguments = InvokeStringMethod("BuildNunifNoiseArguments", -1);

            Assert.That(noiseArguments, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ExpandArgumentTemplate_ReplacesNunifSpecificTokens()
        {
            string arguments = InvokeStringMethod(
                "ExpandArgumentTemplate",
                "-m waifu2x.cli --input {input} --output {output} --method {method} {noise_args} --gpu {gpu} --batch-size {batch_size} --tile-size {tile_size} --style {style}",
                @"C:\Sprites\storm dragon.png",
                @"C:\Output\storm dragon-upscaled.png",
                4,
                3,
                "noise_scale4x",
                "--noise-level 3",
                "0",
                2,
                512,
                "art");

            Assert.That(
                arguments,
                Is.EqualTo("-m waifu2x.cli --input \"C:\\Sprites\\storm dragon.png\" --output \"C:\\Output\\storm dragon-upscaled.png\" --method noise_scale4x --noise-level 3 --gpu 0 --batch-size 2 --tile-size 512 --style art"));
        }

        [Test]
        public void ExpandArgumentTemplate_RejectsTemplatesWithoutInputOrOutputTokens()
        {
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                InvokeStringMethod(
                    "ExpandArgumentTemplate",
                    "-s {scale} -n {noise}",
                    @"C:\Sprites\storm dragon.png",
                    @"C:\Output\storm dragon-upscaled.png",
                    2,
                    1,
                    "noise_scale",
                    "--noise-level 1",
                    "0",
                    4,
                    256,
                    "art"));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(exception.InnerException?.Message, Does.Contain("{input}"));
            Assert.That(exception.InnerException?.Message, Does.Contain("{output}"));
        }

        private static string[] InvokeStringArrayMethod(string methodName, params object[] arguments)
        {
            object value = InvokeToolsMethod(methodName, arguments);
            if (value is string[] directArray)
            {
                return directArray;
            }

            if (value is IEnumerable enumerable)
            {
                return enumerable.Cast<object>().Select(entry => entry?.ToString() ?? string.Empty).ToArray();
            }

            Assert.Fail("Expected method '{0}' to return a string array or enumerable.", methodName);
            return Array.Empty<string>();
        }

        private static string InvokeStringMethod(string methodName, params object[] arguments)
        {
            object value = InvokeToolsMethod(methodName, arguments);
            Assert.That(value, Is.TypeOf<string>(), "Expected method '{0}' to return a string.", methodName);
            return (string)value;
        }

        private static object InvokeToolsMethod(string methodName, params object[] arguments)
        {
            Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, EditorAssemblyName, StringComparison.Ordinal));
            Assert.That(editorAssembly, Is.Not.Null, "Expected editor assembly '{0}' to be loaded.", EditorAssemblyName);

            Type toolsType = editorAssembly.GetType(ToolsTypeName, throwOnError: false);
            Assert.That(toolsType, Is.Not.Null, "Expected editor type '{0}' to exist.", ToolsTypeName);

            MethodInfo method = toolsType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Expected static helper method '{0}' on '{1}'.", methodName, ToolsTypeName);
            return method.Invoke(null, arguments);
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "ProjectPvpWaifu2xSpriteUpgradeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
