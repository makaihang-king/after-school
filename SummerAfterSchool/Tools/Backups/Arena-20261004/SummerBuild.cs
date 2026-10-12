using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Childhood.Editor
{
    public static class SummerBuild
    {
        public const string MainScene = "Assets/Scenes/SummerTown.unity";

        [MenuItem("放学后/打开主场景")]
        public static void OpenScene()
        {
            Prepare(); EditorSceneManager.OpenScene(MainScene);
        }

        [MenuItem("放学后/运行游戏")]
        public static void Play() { OpenScene(); EditorApplication.isPlaying = true; }

        [MenuItem("放学后/导出 Windows 试玩版")]
        public static void BuildWindows()
        {
            Prepare();
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, "Builds", "Windows", "放学后.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene }, locationPathName = path,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            File.WriteAllText(Path.Combine(root, "Logs", "build-summary.json"), JsonUtility.ToJson(new BuildSummaryData
            { result = report.summary.result.ToString(), errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                seconds = (float)report.summary.totalTime.TotalSeconds, bytes = report.summary.totalSize, output = path }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows 构建失败：" + report.summary.result);
            Debug.Log("SUMMER_BUILD_OK " + path);
        }

        public static void Prepare()
        {
            PlayerSettings.companyName = "Summer Street Studio";
            PlayerSettings.productName = "放学后";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            // 旧 Input 支持鼠标与单指触摸模拟，首版不依赖网络包。
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = playerSettings.FindProperty("activeInputHandler"); if (input != null) input.intValue = 0;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = null;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "Standard", "Sprites/Default", "Unlit/Color", "Unlit/Texture", "Skybox/Procedural" })
            {
                var shader = Shader.Find(name); if (shader == null) continue;
                bool exists = false; for (int i = 0; i < shaders.arraySize; i++) if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                if (!exists) { int index = shaders.arraySize; shaders.InsertArrayElementAtIndex(index); shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader; }
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            foreach (string asset in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art/Pogs" }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(asset)) as TextureImporter;
                if (importer == null) continue;
                bool change = importer.maxTextureSize != 1024 || importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency;
                importer.textureType = TextureImporterType.Default; importer.maxTextureSize = 1024;
                importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true; if (change) importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SUMMER_PREPARE_OK");
        }

        [Serializable] class BuildSummaryData { public string result, output; public int errors, warnings; public float seconds; public ulong bytes; }
    }
}
