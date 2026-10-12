using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Childhood
{
    /// <summary>显示偏好独立于收藏存档；预览使用真实时间，失焦或暂停也会按时恢复。</summary>
    public sealed class DisplaySettings : MonoBehaviour
    {
        public const float ConfirmationDuration = 15f;
        public FullScreenMode CurrentMode { get; private set; }
        public int WindowWidth { get; private set; } = 1280;
        public int WindowHeight { get; private set; } = 720;
        public bool AwaitingConfirmation { get; private set; }
        public float ConfirmationSecondsRemaining => AwaitingConfirmation ? Mathf.Max(0, confirmationDeadline - Time.realtimeSinceStartup) : 0;
        public string PreferencePrefix { get; private set; }
        public bool IsolatedPreferences { get; private set; }
        public bool StartupApplied { get; private set; }
        public event Action Changed;
        FullScreenMode previousMode;
        int previousWidth, previousHeight, previousWindowWidth, previousWindowHeight;
        int desktopWidth, desktopHeight, lastCountdown = -1;
        float confirmationDeadline;
        float saveWindowAt = -1;
        bool explicitOverride;
        bool startupPending = true;
        bool mobileDisplay;
        int startupWidth, startupHeight;
        static bool startupArgumentsApplied, processHasDisplayOverrides;
        static bool quitting, quitPrepared;
        static int requestedExitCode;
        // 返回 false 表示退出尚未确认；必须在产生任何副作用之前拦截，取消时不得启动退窗协程。
        public static Func<bool> ExitGate;
        const float QuitPreparationTimeout = 8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStartupState()
        {
            startupArgumentsApplied = false; processHasDisplayOverrides = false;
            quitting = false; quitPrepared = false; requestedExitCode = 0;
        }

        public static void Quit(int exitCode = 0)
        {
            if (!quitting || exitCode != 0) requestedExitCode = exitCode;
            Application.Quit(requestedExitCode);
        }

        void Awake()
        {
            if (Application.isMobilePlatform)
            {
                // Android/iOS own the surface and have no meaningful Windowed/Exclusive modes.
                // Mark the display ready immediately so quitting never waits for a desktop resize.
                mobileDisplay = true;
                CurrentMode = FullScreenMode.FullScreenWindow;
                WindowWidth = Mathf.Max(1, Screen.width);
                WindowHeight = Mathf.Max(1, Screen.height);
                startupPending = false;
                StartupApplied = true;
                return;
            }
            string[] processArguments = Environment.GetCommandLineArgs();
            IsolatedPreferences = processArguments.Contains("--smoke-test") || processArguments.Contains("--arena-test");
            // 命令行只覆盖进程启动；组件重建需读取上次已确认的保存配置。
            string[] arguments = startupArgumentsApplied ? Array.Empty<string>() : processArguments;
            startupArgumentsApplied = true;
            PreferencePrefix = IsolatedPreferences ? "SummerAfterSchool.QA.Display." : "SummerAfterSchool.Display.";
            desktopWidth = Mathf.Max(640, Display.main.systemWidth);
            desktopHeight = Mathf.Max(360, Display.main.systemHeight);
            if (desktopWidth <= 640 || desktopHeight <= 360)
            { desktopWidth = Mathf.Max(640, Screen.currentResolution.width); desktopHeight = Mathf.Max(360, Screen.currentResolution.height); }
            WindowWidth = ClampWidth(PlayerPrefs.GetInt(PreferencePrefix + "WindowWidth", 1280));
            WindowHeight = ClampHeight(PlayerPrefs.GetInt(PreferencePrefix + "WindowHeight", 720));
            int savedMode = PlayerPrefs.GetInt(PreferencePrefix + "Mode", (int)FullScreenMode.Windowed);
            CurrentMode = IsSupported((FullScreenMode)savedMode) ? (FullScreenMode)savedMode : FullScreenMode.Windowed;
            int requestedWidth = WindowWidth, requestedHeight = WindowHeight;
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                string argument = arguments[i], value = arguments[i + 1];
                if (argument == "-screen-width" && int.TryParse(value, out int width)) { requestedWidth = ClampWidth(width); explicitOverride = true; }
                else if (argument == "-screen-height" && int.TryParse(value, out int height)) { requestedHeight = ClampHeight(height); explicitOverride = true; }
                else if (argument == "-screen-fullscreen" && (value == "0" || value == "1"))
                { CurrentMode = value == "0" ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow; explicitOverride = true; }
                else if (argument == "-window-mode" && (value == "exclusive" || value == "borderless"))
                { CurrentMode = value == "exclusive" ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow; explicitOverride = true; }
            }
            // -screen-fullscreen 0 始终代表普通窗口，不能被参数排列顺序误改成全屏。
            int fullscreenArgument = Array.IndexOf(arguments, "-screen-fullscreen");
            if (fullscreenArgument >= 0 && fullscreenArgument + 1 < arguments.Length && arguments[fullscreenArgument + 1] == "0") CurrentMode = FullScreenMode.Windowed;
            if (CurrentMode == FullScreenMode.Windowed) { WindowWidth = requestedWidth; WindowHeight = requestedHeight; }
            startupWidth = explicitOverride ? requestedWidth : 0;
            startupHeight = explicitOverride ? requestedHeight : 0;
            processHasDisplayOverrides |= explicitOverride;
            explicitOverride = processHasDisplayOverrides;
            Debug.Log("Display startup loaded=" + CurrentMode + " engine=" + Screen.fullScreenMode + " " + Screen.width + "x" + Screen.height + " refresh=" + Screen.currentResolution.refreshRateRatio);
            if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen && !Application.isEditor)
            {
                // 引擎继承的独占窗口在首帧失焦时会按全屏尺寸转普通窗口，DX11 可能卡在 Present。
                // 先用记忆中的窗口尺寸建立交换链；保留用户模式，Start 再恢复已确认的独占全屏。
                var refresh = PreferredRefreshRate();
                Debug.Log("Display engine-exclusive bootstrap Windowed " + WindowWidth + "x" + WindowHeight + " refresh=" + refresh + "; saved mode remains " + CurrentMode);
                Screen.SetResolution(WindowWidth, WindowHeight, FullScreenMode.Windowed, refresh);
            }
        }

        IEnumerator Start()
        {
            if (mobileDisplay) yield break;
            // 先让原生窗口和交换链完成首帧；Awake 的帧末切换会与 Unity 的启动独占全屏请求重叠。
            yield return null; yield return null;
            if (!startupPending) yield break;
            startupPending = false;
            Apply(CurrentMode, startupWidth, startupHeight);
            yield return null;
            StartupApplied = true;
            Debug.Log("Display startup applied=" + CurrentMode + " actual=" + Screen.fullScreenMode + " " + Screen.width + "x" + Screen.height);
            Changed?.Invoke();
        }

        public static bool IsSupported(FullScreenMode mode) => mode == FullScreenMode.Windowed || mode == FullScreenMode.FullScreenWindow || mode == FullScreenMode.ExclusiveFullScreen;
        public static string ModeName(FullScreenMode mode) => mode == FullScreenMode.ExclusiveFullScreen ? "全屏" : mode == FullScreenMode.FullScreenWindow ? "全屏窗口化" : "窗口化";

        public void Preview(FullScreenMode mode)
        {
            if (mobileDisplay) return;
            if (!IsSupported(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            startupPending = false; StartupApplied = true;
            if (!AwaitingConfirmation)
            {
                previousMode = CurrentMode;
                previousWidth = Screen.width; previousHeight = Screen.height;
                if (CurrentMode == FullScreenMode.Windowed) { WindowWidth = ClampWidth(Screen.width); WindowHeight = ClampHeight(Screen.height); }
                previousWindowWidth = WindowWidth; previousWindowHeight = WindowHeight;
            }
            CurrentMode = mode;
            AwaitingConfirmation = true;
            confirmationDeadline = Time.realtimeSinceStartup + ConfirmationDuration;
            lastCountdown = -1;
            Apply(mode);
            Changed?.Invoke();
        }

        public void Confirm()
        {
            if (!AwaitingConfirmation) return;
            AwaitingConfirmation = false;
            if (CurrentMode == FullScreenMode.Windowed) { WindowWidth = ClampWidth(Screen.width); WindowHeight = ClampHeight(Screen.height); }
            // 测试与显式命令行配置均不覆盖正常用户的显示偏好。
            SavePreferences();
            Changed?.Invoke();
        }

        public void Revert()
        {
            if (!AwaitingConfirmation) return;
            AwaitingConfirmation = false;
            CurrentMode = previousMode;
            WindowWidth = previousWindowWidth; WindowHeight = previousWindowHeight;
            Apply(previousMode, previousWidth, previousHeight);
            Changed?.Invoke();
        }

        void Update()
        {
            if (AwaitingConfirmation)
            {
                if (ConfirmationSecondsRemaining <= 0) { Revert(); return; }
                int countdown = Mathf.CeilToInt(ConfirmationSecondsRemaining);
                if (countdown != lastCountdown) { lastCountdown = countdown; Changed?.Invoke(); }
            }
            else if (StartupApplied && CurrentMode == FullScreenMode.Windowed && Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                int width = ClampWidth(Screen.width), height = ClampHeight(Screen.height);
                if (width != WindowWidth || height != WindowHeight) { WindowWidth = width; WindowHeight = height; saveWindowAt = Time.realtimeSinceStartup + .5f; }
                if (saveWindowAt >= 0 && Time.realtimeSinceStartup >= saveWindowAt) SavePreferences();
            }
        }

        int ClampWidth(int value) => Mathf.Clamp(value, Mathf.Min(960, desktopWidth), desktopWidth);
        int ClampHeight(int value) => Mathf.Clamp(value, Mathf.Min(540, desktopHeight), desktopHeight);
        void Apply(FullScreenMode mode, int width = 0, int height = 0)
        {
            int requestedWidth = width > 0 ? width : mode == FullScreenMode.Windowed ? WindowWidth : desktopWidth;
            int requestedHeight = height > 0 ? height : mode == FullScreenMode.Windowed ? WindowHeight : desktopHeight;
            if (Screen.fullScreenMode == mode && Screen.width == requestedWidth && Screen.height == requestedHeight) return;
            var refresh = PreferredRefreshRate();
            if (mode == FullScreenMode.ExclusiveFullScreen)
            {
                var supported = Screen.resolutions.Where(resolution => resolution.refreshRateRatio.numerator > 0 && resolution.refreshRateRatio.denominator > 0)
                    .OrderBy(resolution => Mathf.Abs(resolution.width - requestedWidth) + Mathf.Abs(resolution.height - requestedHeight))
                    .ThenBy(resolution => Math.Abs(resolution.refreshRateRatio.value - refresh.value)).ToArray();
                if (supported.Length > 0)
                {
                    requestedWidth = supported[0].width; requestedHeight = supported[0].height;
                    refresh = supported[0].refreshRateRatio;
                }
            }
            Debug.Log("Display resolution request=" + mode + " " + requestedWidth + "x" + requestedHeight + " refresh=" + refresh);
            Screen.SetResolution(requestedWidth, requestedHeight, mode, refresh);
        }
        static RefreshRate PreferredRefreshRate()
        {
            var current = Screen.currentResolution.refreshRateRatio;
            if (current.numerator > 0 && current.denominator > 0) return current;
            var supported = Screen.resolutions.Where(resolution => resolution.refreshRateRatio.numerator > 0 && resolution.refreshRateRatio.denominator > 0)
                .OrderByDescending(resolution => resolution.refreshRateRatio.value).ToArray();
            return supported.Length > 0 ? supported[0].refreshRateRatio : new RefreshRate { numerator = 60, denominator = 1 };
        }
        void SavePreferences()
        {
            saveWindowAt = -1;
            if (explicitOverride && !IsolatedPreferences) return;
            PlayerPrefs.SetInt(PreferencePrefix + "Mode", (int)CurrentMode);
            PlayerPrefs.SetInt(PreferencePrefix + "WindowWidth", WindowWidth);
            PlayerPrefs.SetInt(PreferencePrefix + "WindowHeight", WindowHeight);
            PlayerPrefs.Save();
        }
        bool PrepareQuit()
        {
            if (ExitGate != null && !ExitGate()) return false;
            if (mobileDisplay) return true;
            if (quitPrepared) return true;
            if (quitting) return false;
            if (!isActiveAndEnabled) return true;
            // 未确认的预览始终回到最后确认配置，退出不会替用户确认一次预览。
            if (AwaitingConfirmation) Revert();
            if (saveWindowAt >= 0) SavePreferences();
            if (CurrentMode == FullScreenMode.Windowed && Screen.fullScreenMode == FullScreenMode.Windowed) return true;
            quitting = true;
            startupPending = false;
            StartCoroutine(QuitFromWindow());
            return false;
        }
        IEnumerator QuitFromWindow()
        {
            // Unity 会按退出时的实际窗口保存 Screenmanager。保留我们自己的已确认模式，
            // 让下一次进程先建立普通窗口，再由 Start 恢复用户的全屏模式。
            Debug.Log("Display preparing quit confirmed=" + CurrentMode + " native=" + Screen.fullScreenMode + " window=" + WindowWidth + "x" + WindowHeight + " exitCode=" + requestedExitCode);
            Apply(FullScreenMode.Windowed, WindowWidth, WindowHeight);
            float deadline = Time.realtimeSinceStartup + QuitPreparationTimeout;
            int stableFrames = 0;
            while (stableFrames < 3 && Time.realtimeSinceStartup < deadline)
            {
                bool stable = Screen.fullScreenMode == FullScreenMode.Windowed && Screen.width == WindowWidth && Screen.height == WindowHeight;
                stableFrames = stable ? stableFrames + 1 : 0;
                yield return null;
            }
            if (stableFrames < 3)
            {
                Debug.LogError("Display could not prepare a stable ordinary window before quitting within eight seconds.");
                if (requestedExitCode == 0) requestedExitCode = 1;
            }
            quitPrepared = true;
            Debug.Log("Display quit ready native=" + Screen.fullScreenMode + " " + Screen.width + "x" + Screen.height + " confirmed=" + CurrentMode + " exitCode=" + requestedExitCode);
            Application.Quit(requestedExitCode);
        }
        void OnEnable() { Application.wantsToQuit += PrepareQuit; }
        void OnApplicationQuit() { if (!AwaitingConfirmation && saveWindowAt >= 0) SavePreferences(); }
        void OnDisable() { Application.wantsToQuit -= PrepareQuit; Revert(); }
        void OnDestroy() { Application.wantsToQuit -= PrepareQuit; }
    }
}
