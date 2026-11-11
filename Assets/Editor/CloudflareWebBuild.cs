using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AzureDreamsReimagined.Editor
{
    internal static class CloudflareWebBuild
    {
        private const string MenuPath = "Azure Dreams/Build for Cloudflare";
        private static bool _building;

        [MenuItem(MenuPath, true)]
        private static bool CanBuild() =>
            !_building && !BuildPipeline.isBuildingPlayer && !EditorApplication.isCompiling
            && !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(MenuPath)]
        private static async void Build()
        {
            _building = true;
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                    throw new InvalidOperationException("Install Web Build Support for this Unity Editor in Unity Hub.");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    throw new InvalidOperationException("Switch to a Web build profile in File > Build Profiles, then run this command again.");

                var project = Path.GetDirectoryName(Application.dataPath);
                var script = Path.Combine(project, "Tools", "package_webgl.py");
                if (!File.Exists(script))
                    throw new FileNotFoundException("The Cloudflare packaging script is missing.", script);
                var python = await Task.Run(FindPython);
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                    || BuildPipeline.isBuildingPlayer || EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    throw new InvalidOperationException("Wait until the Web profile is active and the Editor is idle, then try again.");

                var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                if (scenes.Length == 0)
                    throw new InvalidOperationException("Enable the game scenes in the Web build profile first.");

                string buildName = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                string rawBuild = Path.Combine(project, "Library", "CloudflareBuild", buildName);
                string publish = Path.Combine(project, "Builds", "Cloudflare", buildName);
                Directory.CreateDirectory(rawBuild);
                BuildWebPlayer(scenes, rawBuild);
                Debug.Log($"WebGL build complete. Packaging for Cloudflare from {rawBuild}");

                string summary = await Task.Run(() => Package(python, project, script, rawBuild, publish));
                Debug.Log($"Cloudflare build ready at {publish}\n{summary}");
                try { Directory.Delete(rawBuild, recursive: true); }
                catch (IOException error) { Debug.LogWarning($"Temporary WebGL build kept at {rawBuild}. {error.Message}"); }
                catch (UnauthorizedAccessException error) { Debug.LogWarning($"Temporary WebGL build kept at {rawBuild}. {error.Message}"); }
                EditorUtility.RevealInFinder(Path.Combine(publish, "index.html"));
            }
            catch (Exception error)
            {
                Debug.LogError($"Cloudflare build failed. {error.Message}");
                EditorUtility.DisplayDialog("Cloudflare build failed", error.Message, "OK");
            }
            finally
            {
                _building = false;
            }
        }

        private static void BuildWebPlayer(string[] scenes, string destination)
        {
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            var template = PlayerSettings.WebGL.template;
            var hashedNames = PlayerSettings.WebGL.nameFilesAsHashes;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.template = "APPLICATION:Default";
                PlayerSettings.WebGL.nameFilesAsHashes = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = destination,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Unity returned {report.summary.result} with {report.summary.totalErrors} errors. See the Console for build details.");
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.template = template;
                PlayerSettings.WebGL.nameFilesAsHashes = hashedNames;
            }
        }

        private static (string Command, string[] Arguments) FindPython()
        {
            var candidates = new[]
            {
                (Command: "py", Arguments: new[] { "-3" }),
                (Command: "python3", Arguments: Array.Empty<string>()),
                (Command: "python", Arguments: Array.Empty<string>())
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    var start = PythonProcess(candidate, "-c", "import sys; sys.exit(sys.version_info < (3, 11))");
                    using var process = Process.Start(start);
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                        continue;
                    }
                    if (process.ExitCode == 0) return candidate;
                }
                catch (System.ComponentModel.Win32Exception) { /* Try the next launcher. */ }
            }
            throw new InvalidOperationException("Python 3.11 or newer is required. Install it and make py, python3, or python available on PATH.");
        }

        private static string Package((string Command, string[] Arguments) python, string project,
            string script, string source, string destination)
        {
            var start = PythonProcess(python, script, source, destination);
            start.WorkingDirectory = project;
            using var process = Process.Start(start);
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Packaging failed. The Unity build is at {source}.\n{errors.GetAwaiter().GetResult()}");
            return output.GetAwaiter().GetResult().Trim();
        }

        private static ProcessStartInfo PythonProcess((string Command, string[] Arguments) python, params string[] args)
        {
            var start = new ProcessStartInfo(python.Command)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in python.Arguments.Concat(args)) start.ArgumentList.Add(argument);
            return start;
        }
    }
}
