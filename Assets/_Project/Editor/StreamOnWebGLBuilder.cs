using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StreamOn.Editor
{
    public static class StreamOnWebGLBuilder
    {
        private const string OutputDirectory = "WebDeployment/CloudflarePages/public";

        [MenuItem("STREAM ON/Web/Prepare Project")]
        public static void PrepareProject()
        {
            ApplyRecommendedSettings();
            RunnerSceneUiBaker.BakeRoomSceneIfNeeded();
            NewBroadcastFeatureBaker.Bake();
        }

        [MenuItem("STREAM ON/Web/Build Release")]
        public static void BuildRelease()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes are available for the Web build.");

            ApplyRecommendedSettings();

            string output = Path.GetFullPath(OutputDirectory);
            Directory.CreateDirectory(output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"STREAM ON Web build failed: {report.summary.result}");

            WriteCloudflareHeaders(output);
            Debug.Log($"STREAM ON Web release completed: {output} ({report.summary.totalSize:N0} bytes)");
        }

        [MenuItem("STREAM ON/Web/Apply Recommended Settings")]
        public static void ApplyRecommendedSettings()
        {
            PlayerSettings.WebGL.template = "PROJECT:StreamOn";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            AssetDatabase.SaveAssets();
            Debug.Log("STREAM ON recommended WebGL Player Settings applied.");
        }

        private static void WriteCloudflareHeaders(string output)
        {
            const string headers = "/*\n  X-Content-Type-Options: nosniff\n  Referrer-Policy: same-origin\n  Permissions-Policy: camera=(), microphone=(), geolocation=()\n\n/index.html\n  Cache-Control: no-cache\n\n/Build/*\n  Cache-Control: public, max-age=31536000, immutable\n";
            File.WriteAllText(Path.Combine(output, "_headers"), headers);
        }
    }
}
