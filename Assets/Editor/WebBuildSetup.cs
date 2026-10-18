using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// One-click Web (CrazyGames) build configuration. Safe to re-run, e.g. after
    /// re-importing the CrazySDK, which can reset some settings.
    /// </summary>
    public static class WebBuildSetup
    {
        const string BootScenePath = "Assets/Scenes/Boot.unity";
        // The first playable shop.
        const string GameScenePath = "Assets/Scenes/Bakery.unity";
        const string CafeScenePath = "Assets/Scenes/Cafe.unity";
        const string PizzeriaScenePath = "Assets/Scenes/Pizzeria.unity";
        const string WebQualityLevel = "PC";
        const string SdkLoadingScenePath = "Assets/CrazySDK/Demo/LoadingScene.unity";

        [MenuItem("Merge Resto/Apply Web Build Settings")]
        public static void ApplyWebBuildSettings()
        {
            var web = NamedBuildTarget.WebGL;

            // Download size
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.stripUnusedMeshComponents = true;
            PlayerSettings.SetApiCompatibilityLevel(web, ApiCompatibilityLevel.NET_Standard);
            SetWasmCodeOptimization("DiskSizeLTO");

            // Runtime
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            SetWebGLBool("wasm2023", true);

            // Startup
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // PC is the main target, so Web builds start on the PC quality level.
            SetWebDefaultQuality(WebQualityLevel);

            AssetDatabase.SaveAssets();
            Debug.Log("[Merge Resto] Web build settings applied.");
        }

        [MenuItem("Merge Resto/Set Up Boot Scene")]
        public static void SetUpBootScene()
        {
            // Keep our own copy so re-importing the SDK never overwrites it.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(SdkLoadingScenePath, BootScenePath))
                {
                    Debug.LogError($"[Merge Resto] Could not copy {SdkLoadingScenePath}. Is the CrazySDK imported?");
                    return;
                }
            }

            // The SDK's demo loader is replaced by ours, which also picks the player's last shop.
            var boot = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Additive);
            foreach (var old in boot.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CrazySDKLoadingScene>(true)))
            {
                var host = old.gameObject;
                UnityEngine.Object.DestroyImmediate(old);
                if (host.GetComponent<MergeResto.Game.BootLoader>() == null)
                    host.AddComponent<MergeResto.Game.BootLoader>();
            }
            var loader = boot.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MergeResto.Game.BootLoader>(true)).FirstOrDefault();
            if (loader == null)
            {
                var go = new GameObject("BootLoader");
                EditorSceneManager.MoveGameObjectToScene(go, boot);
                loader = go.AddComponent<MergeResto.Game.BootLoader>();
            }
            loader.shopIds = new[] { "grandmas_bakery", "corner_cafe", "nonnos_pizzeria" };
            loader.sceneNames = new[] { "Bakery", "Cafe", "Pizzeria" };
            EditorUtility.SetDirty(loader);
            EditorSceneManager.MarkSceneDirty(boot);
            EditorSceneManager.SaveScene(boot);
            if (UnityEngine.SceneManagement.SceneManager.loadedSceneCount > 1)
                EditorSceneManager.CloseScene(boot, true);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
                new EditorBuildSettingsScene(CafeScenePath, true),
                new EditorBuildSettingsScene(PizzeriaScenePath, true),
            };
            string gameSceneName = "Bakery, Cafe";
            Debug.Log($"[Merge Resto] Build scenes: Boot -> {gameSceneName}.");
        }

        // UnityEditor.WebGL.UserBuildSettings lives in the Web module's editor extension,
        // so reach it by reflection to keep this script compiling without that module.
        static void SetWasmCodeOptimization(string value)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.WebGL.UserBuildSettings"))
                .FirstOrDefault(t => t != null);
            var prop = type?.GetProperty("codeOptimization", BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                Debug.LogWarning("[Merge Resto] Web code optimization setting not found; set it to 'Disk Size with LTO' in Build Profiles.");
                return;
            }
            prop.SetValue(null, Enum.Parse(prop.PropertyType, value));
        }

        static void SetWebDefaultQuality(string levelName)
        {
            int level = System.Array.IndexOf(QualitySettings.names, levelName);
            if (level < 0)
            {
                Debug.LogWarning($"[Merge Resto] Quality level '{levelName}' not found.");
                return;
            }
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(asset);
            var defaults = so.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var entry = defaults.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == "WebGL")
                    entry.FindPropertyRelative("second").intValue = level;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetWebGLBool(string name, bool value)
        {
            var prop = typeof(PlayerSettings.WebGL).GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (prop == null || prop.PropertyType != typeof(bool))
            {
                Debug.LogWarning($"[Merge Resto] PlayerSettings.WebGL.{name} not found in this Unity version.");
                return;
            }
            prop.SetValue(null, value);
        }
    }
}
