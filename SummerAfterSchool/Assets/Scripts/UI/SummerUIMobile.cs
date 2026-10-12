using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>Android/触屏 UI 适配。桌面端不添加任何运行时样式或控件。</summary>
    public sealed partial class SummerUI
    {
        bool mobileUI;

        bool TryInitializeMobileFont()
        {
            var arguments = Environment.GetCommandLineArgs();
            // 桌面验收仅在独立存档模式下运行同一手机界面分支。
            mobileUI = Application.isMobilePlatform ||
                (arguments.Contains("--smoke-test") && arguments.Contains("--mobile-ui-test"));
            if (!mobileUI) return false;
            var mobileFont = Resources.Load<Font>("Fonts/Chinese");
            if (mobileFont == null)
            {
                Debug.LogWarning("Mobile UI font missing: Resources/Fonts/Chinese");
                return false;
            }
            font = mobileFont;
            return true;
        }

        void InitializeMobilePanel()
        {
            if (!mobileUI) return;
            settings.referenceResolution = new Vector2Int(960, 480);
            settings.match = 1;
            root.AddToClassList("mobile-ui");
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplyMobileLayout());
        }

        // InitializeAlerts runs after this panel is initialized, so the priority panel gets the same font and inset.
        void InitializeMobileAlertPanel()
        {
            if (!mobileUI || alertRoot == null) return;
            alertRoot.AddToClassList("mobile-ui");
            alertRoot.RegisterCallback<GeometryChangedEvent>(_ => ApplyMobileLayout());
        }

        void ApplyMobileLayout()
        {
            if (!mobileUI || root == null) return;
            bool portrait = Screen.height > Screen.width;
            root.EnableInClassList("mobile-portrait", portrait);
            root.EnableInClassList("mobile-landscape", !portrait);
            alertRoot?.EnableInClassList("mobile-portrait", portrait);
            alertRoot?.EnableInClassList("mobile-landscape", !portrait);

            // 手机横屏以 480 逻辑高度布局，让文字和触控区占据真实可用的屏幕比例。
            if (settings != null) { settings.referenceResolution = new Vector2Int(960, 480); settings.match = 1; }
            if (alertSettings != null)
            {
                alertSettings.match = 1;
                alertSettings.referenceResolution = new Vector2Int(960, 480);
            }
            ApplySafeInset(screen, false);
            ApplySafeInset(alertRoot, true);
        }

        void ApplySafeInset(VisualElement host, bool padding)
        {
            if (host == null || Screen.width < 1 || Screen.height < 1) return;
            Rect safe = Screen.safeArea;
            float left = Mathf.Clamp01(safe.xMin / Screen.width) * 100f;
            float right = Mathf.Clamp01((Screen.width - safe.xMax) / Screen.width) * 100f;
            float top = Mathf.Clamp01((Screen.height - safe.yMax) / Screen.height) * 100f;
            float bottom = Mathf.Clamp01(safe.yMin / Screen.height) * 100f;
            if (padding)
            {
                host.style.paddingLeft = Length.Percent(left);
                host.style.paddingRight = Length.Percent(right);
                host.style.paddingTop = Length.Percent(top);
                host.style.paddingBottom = Length.Percent(bottom);
            }
            else
            {
                host.style.left = Length.Percent(left);
                host.style.right = Length.Percent(right);
                host.style.top = Length.Percent(top);
                host.style.bottom = Length.Percent(bottom);
            }
        }

        VisualElement MobilePage(string title)
        {
            BuildMobileTopbar(title);
            var page = Box("mobile-page"); screen.Add(page);
            return page;
        }

        void BuildMobileTopbar(string title)
        {
            var bar = Box("mobile-topbar"); bar.name = "mobile-topbar"; screen.Add(bar);
            if (game.Screen != AppScreen.Hub)
            {
                var back = Click("‹ 街区", game.GoHub, "mobile-secondary"); back.name = "mobile-back";
                back.style.width = 110; back.style.marginRight = 10; bar.Add(back);
            }
            var titleFrame = Box("mobile-nav-title-frame"); bar.Add(titleFrame);
            titleFrame.Add(Text(title, "mobile-nav-title"));
            var spacer = Box("mobile-nav-space"); spacer.style.flexGrow = 1; bar.Add(spacer);
            bar.Add(Text("玻璃珠  " + game.Repository.Current.glassMarbles, "mobile-wallet"));
            var menu = Click("菜单", ShowMobileMenu, "mobile-secondary"); menu.name = "mobile-menu";
            menu.style.width = 92; menu.style.marginLeft = 10; bar.Add(menu);
        }

        void ShowMobileMenu()
        {
            ShowMobileSheet("歇一会儿", body =>
            {
                var row = Box("mobile-menu-row"); body.Add(row);
                var settingsButton = Click("游戏设置", () => { CloseDismissOverlay(); ShowAudioSettings(); }, "mobile-secondary");
                settingsButton.name = "mobile-menu-settings"; row.Add(settingsButton);
                row.Add(Click(game.Audio.Muted ? "打开声音" : "暂时静音", () => { game.Audio.ToggleMute(); CloseDismissOverlay(); }, "mobile-secondary"));
                var second = Box("mobile-menu-row"); body.Add(second);
                second.Add(Click("夏天的相片", () => { CloseDismissOverlay(); ShowMobileMemory(); }, "mobile-secondary"));
                second.Add(ToyClick("保存并退出", () => { CloseDismissOverlay(); game.RequestQuit(); }, "danger", "mobile-secondary"));
                body.Add(Text("未完的约战会保留，下次接着玩。", "mobile-caption"));
            });
        }

        void ShowMobileMemory()
        {
            ShowMobileSheet("属于我们的夏天", body =>
            {
                var art = Box("mobile-memory-art");
                art.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("Art/summer_keyart")); body.Add(art);
                body.Add(Text("那时候，最舍不得的宝贝能装进一个小铁盒，最开心的下午可以一直玩到天黑。", "mobile-detail-copy"));
            });
        }

        void ShowMobileSheet(string title, Action<VisualElement> build)
        {
            CloseDismissOverlay();
            var overlay = Box("overlay", true); dismissOverlay = overlay; screen.Add(overlay);
            var panel = Box("mobile-sheet", true); panel.name = "mobile-sheet"; overlay.Add(panel);
            var header = Box("mobile-dialog-header"); panel.Add(header); header.Add(Text(title, "mobile-section-title"));
            var close = Click("收起", CloseDismissOverlay, "mobile-secondary"); close.name = "mobile-sheet-close";
            close.style.width = 96; header.Add(close);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("mobile-sheet-scroll"); panel.Add(scroll);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            build(scroll.contentContainer);
            var actions = scroll.contentContainer.Q(className: "mobile-fixed-sheet-actions");
            if (actions != null) { actions.RemoveFromHierarchy(); panel.Add(actions); }
        }

        void MobilePager(VisualElement parent, int page, int pages, Action<int> select)
        {
            var pager = Box("mobile-pager"); parent.Add(pager);
            var previous = Click("‹ 上一页", () => select(page - 1), "mobile-secondary"); previous.name = "mobile-page-prev";
            previous.SetEnabled(page > 0); pager.Add(previous);
            pager.Add(Text((page + 1) + " / " + pages, "mobile-page-number"));
            var next = Click("下一页 ›", () => select(page + 1), "mobile-secondary"); next.name = "mobile-page-next";
            next.SetEnabled(page + 1 < pages); pager.Add(next);
        }

        void BuildMobileMatch()
        {
            screen.name = "mobile-match";
            var top = Box("mobile-match-top"); screen.Add(top);
            var pause = Click("歇一会儿", ShowPause, "mobile-secondary"); pause.name = "mobile-match-pause"; top.Add(pause);
            var board = Box("mobile-scoreboard", true); top.Add(board);
            var scoreLine = Box("mobile-score-line"); board.Add(scoreLine);
            score = Text("", "mobile-score"); scoreLine.Add(score);
            ruleCaption = Text("", "mobile-rule-caption"); scoreLine.Add(ruleCaption);
            turnLabel = Text("", "mobile-turn-label"); board.Add(turnLabel);
            viewToggle = Click("俯视视角", game.ToggleMatchCamera, "mobile-secondary");
            viewToggle.name = "match-view-toggle"; top.Add(viewToggle);
            highlight = Text("", "highlight"); screen.Add(highlight);

            var left = Box("mobile-match-left", true); screen.Add(left);
            left.Add(Text("出手方式", "mobile-kicker"));
            shotButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var kind = (ShotKind)i;
                shotButtons[i] = Click(GameCatalog.ShotName(kind), () => { game.Pogs.Shot = kind; UpdateMatch(); }, "mobile-shot-button");
                shotButtons[i].AddToClassList("shot-button"); shotButtons[i].name = "mobile-shot-" + i; left.Add(shotButtons[i]);
            }
            BuildMobileShotSetup(left);
            var right = Box("mobile-match-right", true); screen.Add(right);
            BuildMobileMatchControls(right);
            right.Add(Text("出手力度", "mobile-kicker"));
            var bar = Box("mobile-power-bar"); right.Add(bar); powerFill = Box("mobile-power-fill"); bar.Add(powerFill);
            if (game.GuidedPracticeMode) BuildGuidedPower(bar, right);
            else right.Add(Text("向后拖动蓄力\n松手打片", "mobile-aim-hint"));

            var feedback = Box("mobile-match-feedback"); screen.Add(feedback);
            if (game.GuidedPracticeMode)
            {
                feedback.AddToClassList("mobile-guided-feedback");
                guidedMessage = Text("", "mobile-guided-message"); feedback.Add(guidedMessage);
                BuildGuidedAction(feedback);
            }
            message = Text("", "mobile-match-message"); feedback.Add(message);
            if (game.GuidedPracticeMode) message.style.display = DisplayStyle.None;
            UpdateMatch();
        }

        void ShowMobileAudioSettings()
        {
            game.Pogs.CancelAim(); audioPreviousScale = Time.timeScale;
            if (game.Screen == AppScreen.Pogs) Time.timeScale = 0;
            audioOverlay = Box("overlay", true); audioOverlay.name = "game-settings"; screen.Add(audioOverlay);
            var panel = Box("mobile-settings-panel", true); audioOverlay.Add(panel);
            var header = Box("mobile-dialog-header"); panel.Add(header); header.Add(Text("游戏设置", "mobile-section-title"));
            var close = Click("收起", CloseAudioSettings, "mobile-secondary"); close.name = "mobile-settings-close";
            close.style.width = 96; header.Add(close);
            panel.Add(Text("跟着夏天的声音，调到舒服的大小。", "mobile-caption"));
            var content = Box("mobile-settings-content"); content.name = "mobile-settings-content"; panel.Add(content);
            var first = Box("mobile-volume-pair"); content.Add(first);
            AddVolumeSlider(first, "总音量", game.Audio.MasterVolume, 0);
            AddVolumeSlider(first, "音乐", game.Audio.MusicVolume, 1);
            var second = Box("mobile-volume-pair"); content.Add(second);
            AddVolumeSlider(second, "音效", game.Audio.EffectsVolume, 2);
            AddVolumeSlider(second, "环境", game.Audio.AmbienceVolume, 3);
            var actions = Box("mobile-menu-row"); panel.Add(actions);
            audioMuteButton = Click(game.Audio.Muted ? "打开声音" : "暂时静音", () => { game.Audio.ToggleMute(); UpdateMute(); }, "mobile-secondary"); actions.Add(audioMuteButton);
            actions.Add(ToyClick("保存并退出", game.RequestQuit, "danger", "mobile-secondary"));
        }

        void ShowMobilePause()
        {
            game.Pogs.CancelAim(); Time.timeScale = 0;
            pauseOverlay = Box("overlay", true); pauseOverlay.name = "mobile-pause"; screen.Add(pauseOverlay);
            var panel = Box("mobile-pause-panel", true); pauseOverlay.Add(panel);
            panel.Add(Text("喝口汽水，歇一会儿", "mobile-dialog-title"));
            panel.Add(Text("这场约战会保留，回街区后还能继续。", "mobile-caption"));
            var first = Box("mobile-menu-row"); panel.Add(first);
            var resume = Click("继续玩", ResumePause, "mobile-primary"); resume.name = "mobile-pause-resume"; first.Add(resume);
            var audio = Click("游戏设置", ShowAudioSettings, "mobile-secondary"); audio.name = "mobile-pause-settings"; first.Add(audio);
            var second = Box("mobile-menu-row"); panel.Add(second);
            second.Add(Click("回到街区", game.GoHub, "mobile-secondary")); second.Add(Click("这局认输", ConfirmForfeit, "mobile-secondary"));
            var third = Box("mobile-menu-row"); panel.Add(third);
            third.Add(Click("操作与规则", () => ShowMobileSheet("拖动一下，就能出手", body =>
            {
                body.Add(Text("按住出手片，向后拖动蓄力，松手打片。\n左侧选出手方式，点“站位 / 片角”绕场换边、调整片儿前后与左右的倾斜；右侧调旋转或取消蓄力。\n\n翻面要等圆片停稳；出圈要整张越过边界。\n玩真的，双方各押一张约定的真片。", "mobile-detail-copy"));
            }), "mobile-secondary"));
            third.Add(ToyClick("保存并退出", game.RequestExit, "danger", "mobile-secondary"));
        }

        void BuildMobileResult()
        {
            var page = MobilePage("这一场夏日约定"); page.name = "mobile-result";
            page.AddToClassList("mobile-result-page");
            var details = Box("mobile-result-copy", true); page.Add(details);
            details.Add(Text(game.ResultTitle, "mobile-dialog-title"));
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("mobile-result-scroll"); details.Add(scroll);
            scroll.Add(Text(game.ResultText, "mobile-detail-copy"));
            var actions = Box("mobile-result-actions", true); page.Add(actions);
            actions.Add(Click(game.LastSettlement?.route == MatchRoute.Story ? "继续故事" : game.LastSettlement?.route == MatchRoute.Tournament ? "查看锦标赛" : "再来一局", game.Retry, "mobile-primary"));
            actions.Add(Click("去小卖部补给", game.ShowShop, "mobile-secondary"));
            actions.Add(Click("打开铁盒看看", game.ShowCollection, "mobile-secondary"));
            actions.Add(Click("回到街区", game.GoHub, "mobile-secondary"));
        }

        void BuildMobileMatchControls(VisualElement parent)
        {
            if (!mobileUI) return;
            var controls = Box("mobile-match-controls"); parent.Add(controls);
            var row = Box("mobile-control-pair"); controls.Add(row);
            row.Add(Click("旋转左", () => NudgeMobileSpin(-.25f), "mobile-action-button"));
            row.Add(Click("旋转右", () => NudgeMobileSpin(.25f), "mobile-action-button"));
            var second = Box("mobile-control-pair"); controls.Add(second);
            second.Add(Click("归零", ResetMobileSpin, "mobile-action-button"));
            var cancel = Click("取消蓄力", CancelMobileAim, "mobile-action-button"); cancel.AddToClassList("mobile-action-cancel"); second.Add(cancel);
        }

        void NudgeMobileSpin(float amount)
        {
            if (game == null || game.Pogs == null || game.Pogs.Phase != MatchPhase.PlayerAim) return;
            game.Pogs.Spin = Mathf.Clamp(game.Pogs.Spin + amount, -1f, 1f);
            UpdateMatch();
        }

        void ResetMobileSpin()
        {
            if (game == null || game.Pogs == null || game.Pogs.Phase != MatchPhase.PlayerAim) return;
            game.Pogs.Spin = 0;
            UpdateMatch();
        }

        void CancelMobileAim()
        {
            if (game == null || game.Pogs == null) return;
            game.Pogs.CancelAim();
            UpdateMatch();
        }
    }
}
