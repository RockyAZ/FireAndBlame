using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlameAndBlame.EditorTools
{
    /// <summary>One-click WebGL build for itch.io. Menu: Tools/Build WebGL. CLI: -executeMethod FlameAndBlame.EditorTools.WebGLBuild.Build</summary>
    public static class WebGLBuild
    {
        const string OutputDir = "Builds/WebGL";

        [MenuItem("Tools/Build WebGL")]
        public static void Build()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; // works on itch.io without custom headers
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[BUILD] WebGL {report.summary.result} size={report.summary.totalSize / (1024 * 1024)} MB errors={report.summary.totalErrors}");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
