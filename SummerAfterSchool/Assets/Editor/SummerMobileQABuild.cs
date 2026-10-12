using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Childhood.Editor
{
    /// <summary>只导出独立 Windows 手机界面验收程序；不调用修改正式设置的 SummerBuild.Prepare。</summary>
    public static class SummerMobileQABuild
    {
        [MenuItem("放学后/导出手机界面验收程序")]
        public static void BuildMobileQA()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(root, "Builds", "Mobile-QA", "after-school-mobile-qa.exe");
            string summaryPath = Path.Combine(root, "Logs", "mobile-qa-build-summary.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath));
            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone);
            bool changeBackend = backend != ScriptingImplementation.Mono2x;
            bool changeStripping = stripping != ManagedStrippingLevel.Disabled;
            try
            {
                // 验收截图使用原生运行时面板的反射渲染，独立 Mono 构建保留这些入口。
                if (changeBackend) PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                if (changeStripping) PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { SummerBuild.MainScene },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.CompressWithLz4HC
                });
                File.WriteAllText(summaryPath, JsonUtility.ToJson(new Summary
                {
                    result = report.summary.result.ToString(), output = output,
                    errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                    seconds = (float)report.summary.totalTime.TotalSeconds, bytes = report.summary.totalSize
                }, true));
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0 ||
                    !File.Exists(output) || new FileInfo(output).Length == 0)
                    throw new InvalidOperationException("手机界面验收程序构建失败：" + report.summary.result);
                Debug.Log("SUMMER_MOBILE_QA_BUILD_OK " + output);
            }
            finally
            {
                if (changeBackend) PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
                if (changeStripping) PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, stripping);
                if (changeBackend || changeStripping) AssetDatabase.SaveAssets();
            }
        }

        [Serializable] sealed class Summary
        {
            public string result, output;
            public int errors, warnings;
            public float seconds;
            public ulong bytes;
        }
    }
}
