using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Childhood.Editor
{
    /// <summary>Android 发布入口。使用 Unity 自带调试签名，适合本地安装验收。</summary>
    public static class SummerAndroidBuild
    {
        const string MainScene = "Assets/Scenes/SummerTown.unity";
        const string ApplicationId = "com.summerstreetstudio.afterschool";

        [MenuItem("放学后/导出 Android APK")]
        public static void BuildAndroid()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(root, "Builds", "Android-v0.5", "after-school.apk");
            string log = Path.Combine(root, "Logs", "android-build-summary.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            Directory.CreateDirectory(Path.GetDirectoryName(log));

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("无法切换到 Android 构建目标；请在 Unity Hub 安装 Android Build Support。");

            ConfigureAndroid();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.CompressWithLz4HC
            });

            File.WriteAllText(log, JsonUtility.ToJson(new BuildSummary
            {
                result = report.summary.result.ToString(),
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                seconds = (float)report.summary.totalTime.TotalSeconds,
                bytes = report.summary.totalSize,
                output = output,
                applicationId = ApplicationId,
                version = PlayerSettings.bundleVersion,
                versionCode = PlayerSettings.Android.bundleVersionCode,
                apkBytes = File.Exists(output) ? new FileInfo(output).Length : 0,
                builtUtc = DateTime.UtcNow.ToString("O")
            }, true));
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0 || !File.Exists(output) || new FileInfo(output).Length == 0)
                throw new InvalidOperationException("Android 构建失败：" + report.summary.result);
            Debug.Log("SUMMER_ANDROID_BUILD_OK " + output);
        }

        static void ConfigureAndroid()
        {
            PlayerSettings.companyName = "Summer Street Studio";
            PlayerSettings.productName = "放学后";
            PlayerSettings.bundleVersion = "0.5.3";
            PlayerSettings.Android.bundleVersionCode = 4;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppStacktraceInformation(NamedBuildTarget.Android, Il2CppStacktraceInformation.MethodFileLineNumber);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            AssetDatabase.SaveAssets();
        }

        [Serializable]
        sealed class BuildSummary
        {
            public string result, output, applicationId, version, builtUtc;
            public int errors, warnings, versionCode;
            public long apkBytes;
            public float seconds;
            public ulong bytes;
        }
    }
}
