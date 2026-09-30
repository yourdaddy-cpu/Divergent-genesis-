using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DivergentGenesis.Editor
{
    /// <summary>
    /// The method GitHub Actions calls. Builds an Android APK into ./build and
    /// exits with a non zero code on failure so the workflow fails loudly.
    /// </summary>
    public static class DGBuildScript
    {
        public static void BuildAndroid()
        {
            try
            {
                DGProjectSetup.ApplySettings();
                DGProjectSetup.EnsureScene();
                DGProjectSetup.ApplyBuildSceneList();
                AssetDatabase.SaveAssets();

                string outputDir = ResolveArg("-DG_OUTPUT", Path.Combine("build", "divergent-genesis"));
                Directory.CreateDirectory(outputDir);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { DGProjectSetup.ScenePath },
                    locationPathName = Path.Combine(outputDir, "divergent-genesis.apk"),
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };

                var version = ReadVersion();
                PlayerSettings.Android.bundleVersionCode = version;

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                Debug.Log("[DivergentGenesis] build " + summary.result +
                          "  size " + (summary.totalSize / (1024f * 1024f)).ToString("0.00") + " MB" +
                          "  time " + summary.totalTime);

                if (summary.result == BuildResult.Succeeded)
                {
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError("[DivergentGenesis] build FAILED: " + summary.result);
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[DivergentGenesis] build threw: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static int ReadVersion()
        {
            int v;
            return int.TryParse(System.Environment.GetEnvironmentVariable("DG_VERSION_CODE"), out v) && v > 0 ? v : 1;
        }

        private static string ResolveArg(string key, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return fallback;
        }
    }
}
