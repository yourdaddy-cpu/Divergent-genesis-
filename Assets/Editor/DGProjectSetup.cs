using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DivergentGenesis.Editor
{
    /// <summary>
    /// Creates the scene and applies every project setting from code.
    ///
    /// This is what lets the repository stay 100% text: no hand authored .unity
    /// scene, no .asset files, nothing that can break on a fresh clone. Run it
    /// automatically on import and again from the CI build method.
    /// </summary>
    public static class DGProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string PackageName = "com.divergentgenesis.game";
        public const string ProductName = "Divergent Genesis";

        [MenuItem("Divergent Genesis/Rebuild Scene + Settings")]
        public static void RunFromMenu()
        {
            ApplySettings();
            EnsureScene();
            ApplyBuildSceneList();
            AssetDatabase.SaveAssets();
            Debug.Log("[DivergentGenesis] project setup complete");
        }

        public static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject("GameBootstrap");
            boot.AddComponent<GameBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.ImportAsset(ScenePath);
        }

        public static void ApplyBuildSceneList()
        {
            var list = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = list;
        }

        public static void ApplySettings()
        {
            PlayerSettings.companyName = "DivergentGenesis";
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageName);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            // ARM64 only: 32-bit ARM is dead on the 4 GB+ devices we target, and
            // dropping it roughly halves IL2CPP build time.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.androidRenderOutsideSafeArea = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // Landscape only. The joysticks and hotbar are laid out for a wide
            // screen and a 3D view benefits from horizontal field of view.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = false;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCodeGeneration(BuildTargetGroup.Android, Il2CppCodeGeneration.OptimizeSpeed);
            PlayerSettings.MTRendering = true;
            PlayerSettings.graphicsJobs = false;

            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.realtimeGI = false;
            QualitySettings.billboardsFaceCameraPosition = false;
            QualitySettings.antiAliasing = 0;
            QualitySettings.vSyncCount = 0;
            QualitySettings.pixelLightCount = 0;

            Physics.gravity = new Vector3(0f, -28f, 0f);
            Physics.defaultSolverIterations = 4;
            Physics.defaultSolverVelocityIterations = 1;
            Physics.autoSyncTransforms = false;
            Physics.reuseCollisionCallbacks = true;
            Physics.bounceThreshold = 1.4f;

            Time.fixedDeltaTime = 0.02f;
        }

        /// <summary>Called automatically the first time the project is opened.</summary>
        [InitializeOnLoadMethod]
        private static void AutoSetup()
        {
            if (Application.isBatchMode) return;   // CI calls RunFromMenu itself
            if (!File.Exists(ScenePath)) EnsureScene();
            if (!File.Exists(ScenePath)) return;
            if (EditorBuildSettings.scenes == null || EditorBuildSettings.scenes.Length == 0)
                ApplyBuildSceneList();
        }
    }
}
