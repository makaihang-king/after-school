using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Childhood
{
    /// <summary>两个独立原生进程分别写入、回读 QA 显示偏好；不读取或修改正常玩家存档。</summary>
    public sealed class StartupDisplayProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool completed, passed, writeTest, normalKeysUnchanged, commandLineOverridesAbsent;
            public string engine, expectedMode, loadedMode, engineBootMode, actualMode, preferencePrefix;
            public int processId, savedMode, windowWidth, windowHeight, savedWidth, savedHeight, actualWidth, actualHeight;
            public List<string> errors = new List<string>();
            public List<string> observations = new List<string>();
        }

        static readonly string[] normalKeys = { "SummerAfterSchool.Display.Mode", "SummerAfterSchool.Display.WindowWidth", "SummerAfterSchool.Display.WindowHeight" };
        static bool[] initialHasKey;
        static int[] initialValues;
        readonly Report report = new Report();
        DisplaySettings display;
        FullScreenMode expected;
        string output;
        bool finished, argumentsValid;
        float watchdog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CaptureNormalKeys()
        {
            initialHasKey = normalKeys.Select(PlayerPrefs.HasKey).ToArray();
            initialValues = normalKeys.Select(key => PlayerPrefs.GetInt(key, int.MinValue)).ToArray();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootProbe()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Contains("--smoke-test") && (arguments.Contains("--display-write-test") || arguments.Contains("--display-readback-test")))
                new GameObject("QA · 新进程显示偏好").AddComponent<StartupDisplayProbe>();
        }

        void Awake()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/DisplayStartup"));
            string expectedName = null;
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (arguments[i] == "--qa-output") output = Path.GetFullPath(arguments[i + 1]);
                if (arguments[i] == "--expected-display") expectedName = arguments[i + 1];
            }
            Directory.CreateDirectory(output);
            report.engine = Application.unityVersion;
            report.processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            report.writeTest = arguments.Contains("--display-write-test");
            report.expectedMode = expectedName ?? "missing";
            report.commandLineOverridesAbsent = !new[] { "-screen-width", "-screen-height", "-screen-fullscreen", "-window-mode" }.Any(arguments.Contains);
            argumentsValid = Enum.TryParse(expectedName, out expected) && DisplaySettings.IsSupported(expected);
            if (!argumentsValid) report.errors.Add("--expected-display must name Windowed, FullScreenWindow or ExclusiveFullScreen.");
            if (report.writeTest == arguments.Contains("--display-readback-test")) { report.errors.Add("Choose exactly one display write or readback flag."); argumentsValid = false; }
            if (!report.writeTest && !report.commandLineOverridesAbsent) { report.errors.Add("Display readback requires a fresh process without -screen-* or -window-mode overrides."); argumentsValid = false; }
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            watchdog = Time.realtimeSinceStartup + 60;
            report.engineBootMode = Screen.fullScreenMode.ToString();
            report.observations.Add("Probe Awake process=" + report.processId + " engineBoot=" + Screen.fullScreenMode + " " + Screen.width + "x" + Screen.height);
            FlushProgress();
        }

        IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                bool advanced = false, failed = false; object current = null;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception exception) { report.errors.Add("Startup display probe exception: " + exception); Finish(false); failed = true; }
                if (failed) yield break;
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
                else yield return current;
            }
            if (!finished) Finish(true);
        }

        IEnumerator Run()
        {
            if (!argumentsValid) yield break;
            float readyDeadline = Time.realtimeSinceStartup + 10;
            while (display == null && Time.realtimeSinceStartup < readyDeadline)
            {
                var game = FindFirstObjectByType<SummerGame>();
                if (game != null) display = game.Display;
                yield return null;
            }
            if (display == null || !display.IsolatedPreferences) { report.errors.Add("Isolated game DisplaySettings did not initialize."); yield break; }
            report.loadedMode = display.CurrentMode.ToString();
            report.preferencePrefix = display.PreferencePrefix;
            report.observations.Add("Loaded component before startup apply=" + display.CurrentMode + " engine=" + Screen.fullScreenMode + " startupApplied=" + display.StartupApplied);
            FlushProgress();
            float startupDeadline = Time.realtimeSinceStartup + 10;
            while (!display.StartupApplied && Time.realtimeSinceStartup < startupDeadline) yield return null;
            if (!display.StartupApplied) { report.errors.Add("Display component startup did not finish its deferred native application."); yield break; }
            if (report.writeTest)
            {
                display.Preview(expected);
                yield return WaitForNativeMode();
                if (Screen.fullScreenMode != expected) yield break;
                display.Confirm();
                if (display.AwaitingConfirmation) report.errors.Add("Display write did not confirm its preview.");
            }
            else
            {
                if (!new[] { "Mode", "WindowWidth", "WindowHeight" }.All(key => PlayerPrefs.HasKey(display.PreferencePrefix + key)))
                    report.errors.Add("Display readback has no complete prior write in QA preferences.");
                if (display.CurrentMode != expected) report.errors.Add("New process loaded " + display.CurrentMode + " instead of " + expected);
                yield return WaitForNativeMode();
            }
            report.savedMode = PlayerPrefs.GetInt(display.PreferencePrefix + "Mode", -1);
            report.savedWidth = PlayerPrefs.GetInt(display.PreferencePrefix + "WindowWidth", -1);
            report.savedHeight = PlayerPrefs.GetInt(display.PreferencePrefix + "WindowHeight", -1);
            if (report.savedMode != (int)expected || display.CurrentMode != expected || Screen.fullScreenMode != expected)
                report.errors.Add("Actual mode, loaded mode and confirmed QA preference differ.");
            if (display.WindowWidth != report.savedWidth || display.WindowHeight != report.savedHeight)
                report.errors.Add("New display component window dimensions differ from saved QA preferences.");
            if (expected == FullScreenMode.Windowed && (Screen.width != report.savedWidth || Screen.height != report.savedHeight))
                report.errors.Add("Actual window dimensions differ from the saved dimensions.");
            report.observations.Add((report.writeTest ? "Confirmed in writer process " : "Read in fresh process ") + report.processId +
                " expected=" + expected + " actual=" + Screen.fullScreenMode + " resolution=" + Screen.width + "x" + Screen.height +
                " rememberedWindow=" + display.WindowWidth + "x" + display.WindowHeight);
        }

        IEnumerator WaitForNativeMode()
        {
            float deadline = Time.realtimeSinceStartup + 8;
            int stableFrames = 0;
            while (stableFrames < 3 && Time.realtimeSinceStartup < deadline)
            {
                bool matches = Screen.fullScreenMode == expected && (expected != FullScreenMode.Windowed || Screen.width == display.WindowWidth && Screen.height == display.WindowHeight);
                stableFrames = matches ? stableFrames + 1 : 0;
                yield return null;
            }
            if (stableFrames < 3) report.errors.Add("Native screen did not settle to " + expected + " and its remembered window size within eight seconds.");
        }

        void OnLog(string value, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(value + "\n" + stack);
        }
        void Update()
        {
            if (!finished && Time.realtimeSinceStartup >= watchdog) { report.errors.Add("Startup display probe exceeded its sixty-second watchdog."); Finish(false); }
        }
        void Finish(bool completed)
        {
            if (finished) return;
            finished = true;
            report.normalKeysUnchanged = initialHasKey != null && normalKeys.Select((key, index) => PlayerPrefs.HasKey(key) == initialHasKey[index] && PlayerPrefs.GetInt(key, int.MinValue) == initialValues[index]).All(value => value);
            if (!report.normalKeysUnchanged) report.errors.Add("Startup display QA modified normal user preference keys.");
            report.actualMode = Screen.fullScreenMode.ToString(); report.actualWidth = Screen.width; report.actualHeight = Screen.height;
            if (display != null) { report.windowWidth = display.WindowWidth; report.windowHeight = display.WindowHeight; }
            report.completed = completed; report.passed = completed && report.errors.Count == 0;
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(Path.Combine(output, "display-startup-report.json"), JsonUtility.ToJson(report, true));
            DisplaySettings.Quit(report.passed ? 0 : 1);
        }
        void FlushProgress() { File.WriteAllText(Path.Combine(output, "display-startup-report.json"), JsonUtility.ToJson(report, true)); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
