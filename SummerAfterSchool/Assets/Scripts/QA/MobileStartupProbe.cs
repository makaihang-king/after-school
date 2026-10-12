using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>独立手机界面验收：原生启动、资源字体、真实面板截图；不运行长篇物理或故事测试。</summary>
    public sealed class MobileStartupProbe : MonoBehaviour
    {
        const float TimeoutSeconds = 90;

        [Serializable] sealed class Report
        {
            public bool completed, passed, startupReady, isolatedSave, bundledChineseFont;
            public bool hubCompactVerified, settingsVerified, alertVerified, collectionVerified, collectionPaginationVerified;
            public bool practiceVerified, mobileControlsVerified, returnedToHub;
            public bool crowdModelsVerified, storyVerified, tournamentVerified, shopVerified, errandsVerified, lobbyVerified;
            public bool buyPreviewVerified, sellPreviewVerified, errandFlowVerified;
            public bool hubMenuVerified, locationSheetVerified, resultVerified, guidedPracticeVerified, guidedArenaInputVerified;
            public bool realMatchVerified, realMatchHudVerified, realMatchResumeVerified;
            public bool pauseVerified, exitConfirmVerified, arenaInputVerified, layoutQualityVerified;
            public string engine, platform, savePath, startedUtc, finishedUtc;
            public int panelCount, mobileControlsCount, shotsUsed, hubCompactChecks, hubCompactPasses;
            public int layoutQualityChecks, layoutQualityPasses, arenaInputSamples, arenaFreeSamples;
            public int collectionPagesVisited, collectionDefinitionsSeen;
            public int guidedStagesVerified;
            public int crowdActorCount, crowdCapsuleMeshCount, crowdHealthyCapsuleMeshCount, crowdModelChecks, crowdModelPasses;
            public float elapsedSeconds;
            public List<string> errors = new List<string>();
            public List<string> observations = new List<string>();
            public List<CaptureObservation> captures = new List<CaptureObservation>();
            public List<LayoutObservation> layoutQuality = new List<LayoutObservation>();
        }

        [Serializable] sealed class CaptureObservation
        {
            public string name, path, screen;
            public int width, height;
            public bool rendered, nonBlank;
        }

        [Serializable] sealed class LayoutObservation
        {
            public string name, screen, surface;
            public float logicalWidth, logicalHeight, pixelsPerPanelUnit, minButtonHeight, minButtonFontSize;
            public int customButtons, visibleButtons, scrollOffscreenButtons, clippedButtons, clippedCardActions, clippedButtonLabels, undersizedButtons, unreadableButtons, elongatedButtons;
            public bool passed;
        }

        readonly Report report = new Report();
        SummerGame game;
        UIDocument mainDocument, alertDocument;
        string output;
        float startedAt, watchdog;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootProbe()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (arguments.Contains("--smoke-test") && arguments.Contains("--mobile-ui-test") && !arguments.Contains("--shot-feedback-test") &&
                !arguments.Contains("--free-angle-experience-test") && !arguments.Contains("--companion-experience-test") && !arguments.Contains("--qa-manual-inspection"))
                new GameObject("手机启动与布局验收").AddComponent<MobileStartupProbe>();
        }

        void Awake()
        {
            var arguments = Environment.GetCommandLineArgs();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/MobileStartup"));
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == "--qa-output") output = Path.GetFullPath(arguments[i + 1]);
            Directory.CreateDirectory(output);
            report.engine = Application.unityVersion;
            report.platform = Application.platform.ToString();
            report.startedUtc = DateTime.UtcNow.ToString("O");
            Application.runInBackground = true;
            startedAt = Time.realtimeSinceStartup;
            watchdog = startedAt + TimeoutSeconds;
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string value, string stack, LogType type)
        {
            if (!finished && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                report.errors.Add(value + "\n" + stack);
        }

        IEnumerator Start()
        {
            // 展平协程，页面或截图异常也必须生成失败报告并用非零状态退出。
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object current = null;
                bool advanced = false, failed = false;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception exception) { report.errors.Add("Mobile probe exception: " + exception); Finish(false); failed = true; }
                if (failed) yield break;
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
                else yield return current;
            }
            if (!finished) Finish(true);
        }

        IEnumerator Run()
        {
            while ((game = FindFirstObjectByType<SummerGame>()) == null) yield return null;
            report.startupReady = game.enabled && game.Repository != null && game.World != null && game.World.IsBuilt &&
                game.World.MainCamera != null && game.World.FloorCollider != null && game.Audio != null &&
                game.Pogs != null && game.UI != null && game.Display != null && game.GetComponent<CrowdActors>() != null;
            Require(report.startupReady, "Startup did not initialize every required component: " + game.Error);
            if (!report.startupReady) { Finish(false); yield break; }
            Require(string.IsNullOrEmpty(game.Error), "Startup reported an error: " + game.Error);
            VerifySaveIsolation();
            if (!report.isolatedSave || game.Repository.HasPendingMatch) { Finish(false); yield break; }
            yield return SettleLayout();
            VerifyCrowdModels("initial");

            var documents = game.GetComponentsInChildren<UIDocument>(true);
            report.panelCount = documents.Length;
            Require(documents.Length == 2, "Expected the main and priority UIDocument panels");
            mainDocument = documents.FirstOrDefault(item => item.panelSettings != null && item.panelSettings.sortingOrder == 20);
            alertDocument = documents.FirstOrDefault(item => item.panelSettings != null && item.panelSettings.sortingOrder == 200);
            Require(mainDocument != null && alertDocument != null, "Main or priority panel is missing");
            if (mainDocument == null || alertDocument == null) { Finish(false); yield break; }
            Require(game.Screen == AppScreen.Hub, "Initial screen is not the hub");
            yield return Capture("01-mobile-hub");
            // 隐藏窗口只有显式 Repaint/Render 后才可靠地计算子树尺寸。
            VerifyPanels(documents);
            VerifyCompactHub("initial");
            yield return VerifyHubSheets();

            game.UI.ShowAudioSettings();
            yield return SettleLayout();
            yield return Capture("02-mobile-settings");
            VerifySettings();
            Require(game.UI.HandleEscape() && !game.UI.SettingsVisible, "Settings did not close through the existing Escape flow");

            game.UI.ShowNotice("手机启动验收", "中文提示能正常显示。原存档由隔离测试目录保护。");
            yield return SettleLayout();
            report.alertVerified = game.UI.AlertVisible && game.UI.AlertMessage.Contains("中文提示") &&
                AlertRoot.Q<ScrollView>(className: "priority-scroll") != null;
            Require(report.alertVerified, "Priority mobile alert or its scroll container is missing");
            yield return Capture("03-mobile-alert");
            game.UI.CloseAlert();
            Require(!game.UI.AlertVisible, "Priority alert did not close");

            yield return VerifyCollection();

            game.GoHub(); game.ShowStory();
            yield return SettleLayout();
            yield return Capture("07-mobile-story");
            report.storyVerified = VerifyMobilePage(AppScreen.Story, "mobile-story");

            game.GoHub(); game.ShowTournament();
            yield return SettleLayout();
            yield return Capture("08-mobile-tournament");
            report.tournamentVerified = VerifyMobilePage(AppScreen.Tournament, "mobile-tournament");

            game.GoHub(); game.ShowShop();
            yield return SettleLayout();
            yield return Capture("09-mobile-shop");
            report.shopVerified = VerifyMobilePage(AppScreen.Shop, "mobile-shop");
            yield return VerifyTradePreview("buy-", "09-mobile-shop-buy-preview");
            for (int shopTab = 1; shopTab <= 2; shopTab++)
            {
                var tab = MainRoot.Q<Button>("shop-tab-" + shopTab);
                Require(tab != null, "Mobile shop is missing tab " + shopTab);
                if (tab == null) continue;
                Submit(tab); yield return SettleLayout();
                yield return Capture("09-mobile-shop-tab-" + shopTab);
                report.shopVerified &= VerifyMobilePage(AppScreen.Shop, "mobile-shop");
                if (shopTab == 1) yield return VerifyTradePreview("sell-", "09-mobile-shop-sell-preview");
            }

            game.GoHub(); game.ShowErrands();
            yield return SettleLayout();
            yield return Capture("10-mobile-errands");
            report.errandsVerified = VerifyMobilePage(AppScreen.Errands, "mobile-errands");
            var firstErrandActions = MainRoot.Query<Button>().ToList().Where(button => button.name != null && button.name.StartsWith("accept-", StringComparison.Ordinal)).Select(button => button.name).ToArray();
            var errandNext = MainRoot.Q<Button>("mobile-page-next");
            Require(errandNext != null && errandNext.enabledInHierarchy, "Mobile errands must expose an enabled Next action on the first page");
            if (errandNext != null && errandNext.enabledInHierarchy)
            {
                Submit(errandNext); yield return SettleLayout();
                yield return Capture("10-mobile-errands-page-2");
                report.errandsVerified &= VerifyMobilePage(AppScreen.Errands, "mobile-errands");
                var secondErrandActions = MainRoot.Query<Button>().ToList().Where(button => button.name != null && button.name.StartsWith("accept-", StringComparison.Ordinal)).Select(button => button.name).ToArray();
                Require(secondErrandActions.Length > 0 && !secondErrandActions.Any(name => firstErrandActions.Contains(name)),
                    "Mobile errands Next must show different errands on the second page");
                var errandPrev = MainRoot.Q<Button>("mobile-page-prev");
                Require(errandPrev != null && errandPrev.enabledInHierarchy, "Mobile errands must expose an enabled Previous action on the second page");
                if (errandPrev != null && errandPrev.enabledInHierarchy)
                {
                    Submit(errandPrev); yield return SettleLayout();
                    var returnedErrandActions = MainRoot.Query<Button>().ToList().Where(button => button.name != null && button.name.StartsWith("accept-", StringComparison.Ordinal)).Select(button => button.name).ToArray();
                    Require(returnedErrandActions.SequenceEqual(firstErrandActions), "Mobile errands Previous must restore the first page before the errand flow");
                }
            }
            yield return VerifyErrandFlow();

            game.GoHub(); game.ShowLobby();
            yield return SettleLayout();
            yield return Capture("11-mobile-lobby");
            report.lobbyVerified = VerifyMobilePage(AppScreen.Lobby, "mobile-lobby");
            // 约战三步只切换界面，不选择赌注、不成交、不推进故事或杯赛。
            for (int step = 1; step <= 2; step++)
            {
                var tab = MainRoot.Q<Button>("mobile-lobby-step-" + step);
                Require(tab != null, "Mobile lobby wizard is missing step " + step);
                if (tab == null) continue;
                Submit(tab); yield return SettleLayout();
                yield return Capture("11-mobile-lobby-step-" + step);
                VerifyMobilePage(AppScreen.Lobby, "mobile-lobby");
            }
            game.Training = true; game.RealMatch = false; game.SelectedRule = RuleKind.Flip;
            game.StartPogs();
            report.practiceVerified = game.Screen == AppScreen.Pogs && game.Pogs.Practice &&
                game.ActiveMatch != null && !game.ActiveMatch.isReal && game.ActiveMatch.soloPractice;
            Require(report.practiceVerified, "A real solo practice match did not start: " + game.Error);
            if (!report.practiceVerified) { Finish(false); yield break; }
            float aimDeadline = Time.realtimeSinceStartup + 8;
            while (game.Pogs.Phase != MatchPhase.PlayerAim && Time.realtimeSinceStartup < aimDeadline) yield return null;
            Require(game.Pogs.Phase == MatchPhase.PlayerAim, "Practice did not reach the player's aiming phase");
            yield return SettleLayout();
            yield return Capture("12-mobile-practice");
            VerifyMobileControls();
            VerifyArenaInput();

            game.UI.ShowPause();
            yield return SettleLayout();
            yield return Capture("13-mobile-pause");
            report.pauseVerified = game.UI.PauseVisible && Time.timeScale == 0;
            Require(report.pauseVerified, "Mobile pause did not preserve the existing pause flow");
            game.UI.ShowAudioSettings();
            yield return SettleLayout();
            yield return Capture("14-mobile-match-settings");
            VerifySettings();
            Require(game.UI.HandleEscape() && !game.UI.SettingsVisible && game.UI.PauseVisible && Time.timeScale == 0,
                "Closing match settings must restore the still-open pause surface");

            game.UI.ShowExitConfirm();
            yield return SettleLayout();
            yield return Capture("15-mobile-exit-confirm");
            var exit = MainRoot.Q("exit-confirm");
            var stay = exit?.Q<Button>("exit-continue");
            var saveAndExit = exit?.Q<Button>("exit-save");
            report.exitConfirmVerified = exit != null && stay != null && saveAndExit != null && Time.timeScale == 0;
            Require(report.exitConfirmVerified, "Mobile exit confirmation lacks its two safe exit actions or lost pause state");
            if (stay != null) Submit(stay); else game.UI.HandleEscape();
            Require(MainRoot.Q("exit-confirm") == null && game.UI.PauseVisible && Time.timeScale == 0,
                "Cancelling mobile exit must keep the underlying match paused");
            Require(game.UI.HandleEscape() && !game.UI.PauseVisible && Time.timeScale == 1,
                "Mobile pause did not resume through the existing Escape flow");

            // 真实练习认输正常结算至结果页，既验收结果布局也解除下一场教学的 pending 门控。
            Require(game.Pogs.ShotsUsed == 0, "Mobile practice verification launched a shot before its safe result-page transition");
            int cardsBeforeResult = game.Repository.PlayerCards.Count;
            game.Forfeit(); yield return SettleLayout();
            yield return Capture("15-mobile-result");
            report.resultVerified = VerifyMobilePage(AppScreen.Result, "mobile-result") &&
                !game.Repository.HasPendingMatch && game.Repository.PlayerCards.Count == cardsBeforeResult &&
                game.LastSettlement != null && game.LastSettlement.gainedCardIds.Count == 0 && game.LastSettlement.lostCardIds.Count == 0;
            Require(report.resultVerified, "Forfeiting the isolated solo practice must reach the mobile result page without transferring cards");

            // 只测试页面、取消、正常练习结算与教学重摆；所有阶段均不发射圆片。
            game.GoHub();
            yield return SettleLayout();
            report.returnedToHub = game.Screen == AppScreen.Hub && Time.timeScale == 1;
            Require(report.returnedToHub, "Returning from practice left the hub unavailable or time paused");
            yield return Capture("16-mobile-hub-return");
            VerifyCompactHub("return");
            VerifyCrowdModels("return");
            yield return VerifyGuidedPractice();
            yield return VerifyRealMatch();
            report.shotsUsed = Mathf.Max(report.shotsUsed, game.Pogs.ShotsUsed);
            Require(report.shotsUsed == 0, "Mobile layout validation unexpectedly launched a shot");
        }

        VisualElement MainRoot => mainDocument.rootVisualElement;
        VisualElement AlertRoot => alertDocument.rootVisualElement;

        void VerifyCrowdModels(string stage)
        {
            var crowd = game.GetComponent<CrowdActors>();
            var crowdRoot = crowd == null ? null : crowd.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.parent == crowd.transform && item.name == "街区伙伴 · 围观的小朋友" && item.gameObject.activeInHierarchy);
            var actors = crowdRoot == null ? new Transform[0] : crowdRoot.Cast<Transform>()
                .Where(item => item.name.EndsWith(" · 微缩儿童", StringComparison.Ordinal)).ToArray();
            report.crowdActorCount = actors.Length;
            bool passed = crowdRoot != null && actors.Length == 4;
            Require(passed, "Crowd " + stage + " must contain four active child character roots");

            foreach (var actor in actors)
            {
                var filters = actor.GetComponentsInChildren<MeshFilter>(true);
                var capsules = filters.Where(IsCapsuleMesh).ToArray();
                int healthyMeshes = filters.Count(filter => HasRenderableMesh(filter) && filter.gameObject.activeInHierarchy);
                int healthyCapsules = capsules.Count(filter => HasRenderableMesh(filter) && filter.gameObject.activeInHierarchy);
                // 每名儿童：双腿六段、脖子一段、双臂六段、书包提手两段，合计十五个胶囊网格。
                bool actorPassed = actor.gameObject.activeInHierarchy && filters.Length > 15 &&
                    healthyMeshes == filters.Length && capsules.Length == 15 && healthyCapsules == 15;
                passed &= actorPassed;
                report.observations.Add("crowd " + stage + " actor=" + actor.name + " meshes=" + filters.Length +
                    " healthyMeshes=" + healthyMeshes + " capsuleMeshes=" + capsules.Length + " healthyCapsules=" + healthyCapsules);
                Require(actorPassed, "Crowd " + stage + " character has missing or unusable mesh/render components: " + actor.name);
            }

            var crowdFilters = crowdRoot == null ? new MeshFilter[0] : crowdRoot.GetComponentsInChildren<MeshFilter>(true);
            var crowdCapsules = crowdFilters.Where(IsCapsuleMesh).ToArray();
            report.crowdCapsuleMeshCount = crowdCapsules.Length;
            report.crowdHealthyCapsuleMeshCount = crowdCapsules.Count(HasRenderableMesh);
            // 四名儿童六十段，另加默认隐藏的出手手掌六段；隐藏对象也必须有完整的网格和材质。
            bool allCapsules = report.crowdCapsuleMeshCount == 66 && report.crowdHealthyCapsuleMeshCount == 66;
            passed &= allCapsules;
            Require(allCapsules, "Crowd " + stage + " must retain all 66 capsule meshes and render components, including the hidden player hand");
            report.crowdModelChecks++;
            if (passed) report.crowdModelPasses++;
            report.crowdModelsVerified = report.crowdModelChecks == report.crowdModelPasses;
            report.observations.Add("crowd " + stage + " actors=" + report.crowdActorCount +
                " capsuleMeshes=" + report.crowdCapsuleMeshCount + " healthyCapsules=" + report.crowdHealthyCapsuleMeshCount + " passed=" + passed);
        }

        static bool IsCapsuleMesh(MeshFilter filter)
        {
            return filter != null && filter.sharedMesh != null &&
                filter.sharedMesh.name.IndexOf("Capsule", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool HasRenderableMesh(MeshFilter filter)
        {
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) return false;
            var renderer = filter.GetComponent<MeshRenderer>();
            return renderer != null && renderer.enabled && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null;
        }

        void VerifySaveIsolation()
        {
            string expectedDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/save-v04"));
            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == "--qa-save") expectedDirectory = Path.GetFullPath(arguments[i + 1]);
            report.savePath = game.Repository.SavePath;
            string expected = Path.Combine(expectedDirectory, "collection.json");
            string normal = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "SummerAfterSchool-v2", "collection.json"));
            string actual = Path.GetFullPath(report.savePath);
            report.isolatedSave = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(actual, normal, StringComparison.OrdinalIgnoreCase);
            Require(report.isolatedSave, "Mobile probe is not using the expected isolated QA save: " + actual);
            Require(!game.Repository.HasPendingMatch, "Mobile startup validation requires a fresh QA directory without a pending match");
        }

        void VerifyPanels(UIDocument[] documents)
        {
            var chinese = Resources.Load<Font>("Fonts/Chinese");
            report.bundledChineseFont = chinese != null && chinese.HasCharacter('放') && chinese.HasCharacter('夏');
            Require(report.bundledChineseFont, "Bundled font is missing or cannot supply Chinese glyphs");
            foreach (var document in documents)
            {
                var root = document.rootVisualElement;
                Require(root != null && root.ClassListContains("mobile-ui"), "Panel lacks mobile-ui class: " + document.name);
                if (root == null) continue;
                Require(root.style.unityFontDefinition.value.font == chinese && chinese != null,
                    "Panel does not use the bundled Chinese font: " + document.name);
                Require(root.panel != null && root.worldBound.width > 1 && root.worldBound.height > 1,
                    "Panel has no live viewport: " + document.name);
                Require(document.panelSettings.referenceResolution == new Vector2Int(960, 480) &&
                    Mathf.Approximately(document.panelSettings.match, 1),
                    "Mobile panel must use 960x480 reference resolution and match height: " + document.name);
                Require(Mathf.Abs(root.worldBound.height - 480) <= 1,
                    "Mobile panel's logical height is not 480: " + root.worldBound);
                report.observations.Add(document.name + " viewport=" + root.worldBound + " font=" + (chinese == null ? "missing" : chinese.name));
            }
        }

        void VerifySettings()
        {
            var settings = MainRoot.Q("game-settings");
            var content = settings?.Q("mobile-settings-content");
            var allSliders = settings == null ? new List<Slider>() : settings.Query<Slider>().ToList();
            var volumeSliders = allSliders.Where(slider => slider.name != null && slider.name.StartsWith("volume-", StringComparison.Ordinal)).ToList();
            string[] volumeNames = { "volume-0", "volume-1", "volume-2", "volume-3" };
            // 只统计四个命名声音通道；纯手机设置不借用桌面显示控件。
            bool volumeControls = volumeSliders.Count == 4 &&
                volumeNames.All(name => volumeSliders.Count(slider => slider.name == name) == 1);
            bool volumeInContent = content != null && volumeSliders.All(slider => content.Contains(slider));
            var desktopButtons = settings == null ? new List<Button>() : settings.Query<Button>().ToList()
                .Where(button => button.name != null && button.name.StartsWith("display-", StringComparison.Ordinal)).ToList();
            bool liveContent = content != null && content.worldBound.width > 1 && content.worldBound.height > 1;
            bool twoByTwo = volumeControls && Mathf.Abs(volumeSliders.Single(slider => slider.name == "volume-0").worldBound.y -
                volumeSliders.Single(slider => slider.name == "volume-1").worldBound.y) <= PanelPixelTolerance(MainRoot) &&
                Mathf.Abs(volumeSliders.Single(slider => slider.name == "volume-2").worldBound.y -
                volumeSliders.Single(slider => slider.name == "volume-3").worldBound.y) <= PanelPixelTolerance(MainRoot) &&
                volumeSliders.Single(slider => slider.name == "volume-0").worldBound.xMax < volumeSliders.Single(slider => slider.name == "volume-1").worldBound.xMin &&
                volumeSliders.Single(slider => slider.name == "volume-2").worldBound.yMin > volumeSliders.Single(slider => slider.name == "volume-0").worldBound.yMax;
            report.settingsVerified = game.UI.SettingsVisible && settings != null && liveContent &&
                volumeControls && volumeInContent && twoByTwo && desktopButtons.Count == 0;
            report.observations.Add("settings visible=" + game.UI.SettingsVisible + " panel=" + DescribeElement(settings) +
                " content=" + DescribeElement(content) + " allSliders=" + allSliders.Count + " volumeControls=" + volumeControls +
                " volumeInContent=" + volumeInContent + " twoByTwo=" + twoByTwo + " desktopChoices=" + desktopButtons.Count);
            foreach (var slider in allSliders)
                report.observations.Add("settings slider=" + DescribeElement(slider) + " value=" + slider.value);
            Require(report.settingsVerified, "Mobile settings must show exactly four named audio sliders in a bounded 2x2 content area and no desktop display choices");
            foreach (var slider in volumeSliders)
                Require(IsFullyVisible(slider.worldBound, MainRoot.worldBound, PanelPixelTolerance(MainRoot)) &&
                    slider.worldBound.width >= 140 && slider.worldBound.height >= 36,
                    "Mobile volume slider is clipped or too small to drag: " + DescribeElement(slider));
        }

        void VerifyCompactHub(string stage)
        {
            var scroll = MainRoot.Q<ScrollView>(className: "mobile-hub-scroll");
            var hub = MainRoot.Q("mobile-hub");
            var tiles = hub == null ? new List<Button>() : hub.Query<Button>(className: "mobile-hub-tile").ToList();
            float tolerance = PanelPixelTolerance(MainRoot);
            bool sixVisibleTiles = tiles.Count == 6 && tiles.All(tile => IsDisplayed(tile) && tile.enabledInHierarchy &&
                IsFullyVisible(tile.worldBound, MainRoot.worldBound, tolerance) && tile.worldBound.height + tolerance >= 50 &&
                ButtonFontSize(tile) >= 20);
            bool twoColumns = tiles.Count == 6 && Enumerable.Range(0, 3).All(row =>
                Mathf.Abs(tiles[row * 2].worldBound.y - tiles[row * 2 + 1].worldBound.y) <= tolerance &&
                tiles[row * 2].worldBound.xMax <= tiles[row * 2 + 1].worldBound.xMin + tolerance);
            bool passed = hub != null && scroll == null && Mathf.Abs(MainRoot.worldBound.height - 480) <= 1 &&
                sixVisibleTiles && twoColumns;
            report.hubCompactChecks++;
            if (passed) report.hubCompactPasses++;
            report.hubCompactVerified = report.hubCompactChecks == report.hubCompactPasses;
            report.observations.Add("hub " + stage + " viewport=" + MainRoot.worldBound + " oldLongScroll=" + (scroll != null) +
                " tiles=" + tiles.Count + " sixVisibleTiles=" + sixVisibleTiles + " twoColumns=" + twoColumns);
            foreach (var tile in tiles)
                report.observations.Add("hub " + stage + " tile=" + DescribeElement(tile) + " titleFont=" + ButtonFontSize(tile));
            Require(passed, "Hub " + stage + " must show six readable, touch-sized tiles in two columns without the old long ScrollView");
        }

        IEnumerator VerifyHubSheets()
        {
            var menu = MainRoot.Q<Button>("mobile-menu");
            Require(menu != null && menu.enabledInHierarchy, "Mobile hub has no usable menu button");
            if (menu != null)
            {
                Submit(menu); yield return SettleLayout();
                yield return Capture("01-mobile-hub-menu");
                var sheet = MainRoot.Q("mobile-sheet");
                var close = sheet?.Q<Button>("mobile-sheet-close");
                bool opened = sheet != null && close != null && sheet.Q<Button>("mobile-menu-settings") != null &&
                    sheet.Query<Button>().ToList().Count(button => button.ClassListContains("input-block")) == 5;
                Require(opened, "Mobile hub menu must expose four short native actions plus its close button");
                if (close != null) Submit(close); else game.UI.HandleEscape();
                yield return SettleLayout();
                report.hubMenuVerified = opened && MainRoot.Q("mobile-sheet") == null && game.Screen == AppScreen.Hub;
                Require(report.hubMenuVerified, "Mobile menu did not close back to the hub");
            }
            var locations = MainRoot.Q<Button>("open-locations");
            Require(locations != null && locations.enabledInHierarchy, "Mobile hub has no usable location picker");
            if (locations == null) yield break;
            string originalLocation = game.LocationId;
            Submit(locations); yield return SettleLayout();
            yield return Capture("01-mobile-hub-locations");
            var locationSheet = MainRoot.Q("mobile-sheet");
            var locationChoices = locationSheet == null ? new List<Button>() : locationSheet.Query<Button>().ToList()
                .Where(button => button.name != null && button.name.StartsWith("location-", StringComparison.Ordinal)).ToList();
            var locationClose = locationSheet?.Q<Button>("mobile-sheet-close");
            bool locationsOpened = locationSheet != null && locationClose != null && locationChoices.Count == GameCatalog.Locations.Length;
            Require(locationsOpened, "Mobile location picker does not expose the full set of native destination buttons");
            if (locationClose != null) Submit(locationClose); else game.UI.HandleEscape();
            yield return SettleLayout();
            report.locationSheetVerified = locationsOpened && MainRoot.Q("mobile-sheet") == null && game.Screen == AppScreen.Hub &&
                game.LocationId == originalLocation;
            Require(report.locationSheetVerified, "Closing the location sheet changed location or failed to return to the hub");
        }

        IEnumerator VerifyGuidedPractice()
        {
            bool completionBefore = game.Repository.GuidedPracticeCompleted;
            var start = MainRoot.Q<Button>("open-guided-practice");
            Require(start != null && start.enabledInHierarchy, "Mobile hub cannot start the real guided practice flow");
            if (start == null) yield break;
            Submit(start);
            bool started = game.Screen == AppScreen.Pogs && game.GuidedPracticeMode && game.GuidedPractice != null && !game.Repository.HasPendingMatch;
            Require(started, "Native mobile tutorial tile did not enter guided practice: " + game.Error);
            if (!started) yield break;
            foreach (GuidedPracticeStep step in new[] { GuidedPracticeStep.DragAndCancel, GuidedPracticeStep.SlapFlip, GuidedPracticeStep.SkimRingOut })
            {
                if (step != GuidedPracticeStep.DragAndCancel)
                {
                    // 公开的教学重摆 API 发出正常 ResetRequested，构建对应规则与练习盘；不伪造完成、不调用出手。
                    game.GuidedPractice.SetStep(step); game.GuidedPractice.RetryStep();
                }
                float aimDeadline = Time.realtimeSinceStartup + 8;
                while (game.Pogs.Phase != MatchPhase.PlayerAim && Time.realtimeSinceStartup < aimDeadline) yield return null;
                yield return SettleLayout();
                yield return Capture("17-mobile-guided-step-" + ((int)step + 1));
                var prompt = MainRoot.Q<Label>(className: "mobile-guided-message");
                var caption = MainRoot.Q<Label>(className: "mobile-rule-caption");
                var feedback = MainRoot.Q(className: "mobile-match-feedback");
                bool readable = game.Pogs.Phase == MatchPhase.PlayerAim && game.GuidedPractice.Step == step && prompt != null &&
                    caption != null && feedback != null && !string.IsNullOrWhiteSpace(prompt.text) && !string.IsNullOrWhiteSpace(caption.text) &&
                    prompt.resolvedStyle.fontSize >= 20 && caption.resolvedStyle.fontSize >= 20 &&
                    IsFullyVisible(prompt.worldBound, MainRoot.worldBound, PanelPixelTolerance(MainRoot)) &&
                    IsFullyVisible(prompt.worldBound, feedback.worldBound, PanelPixelTolerance(MainRoot)) &&
                    IsFullyVisible(caption.worldBound, MainRoot.worldBound, PanelPixelTolerance(MainRoot));
                Require(readable, "Mobile guided practice must preserve a readable, fully visible stage caption and prompt: " + step +
                    " caption=" + DescribeElement(caption) + " prompt=" + DescribeElement(prompt));
                if (readable) report.guidedStagesVerified++;
                report.observations.Add("guided step=" + step + " caption=" + (caption == null ? "missing" : caption.text) +
                    " prompt=" + (prompt == null ? "missing" : prompt.text) + " phase=" + game.Pogs.Phase);
                VerifyArenaInput("guided-" + step);
                Require(game.Pogs.ShotsUsed == 0, "Guided mobile layout verification unexpectedly launched a shot: " + step);
            }
            report.guidedArenaInputVerified = report.arenaInputSamples == 36 && report.arenaFreeSamples == 36;
            report.guidedPracticeVerified = started && report.guidedStagesVerified == 3 &&
                game.Repository.GuidedPracticeCompleted == completionBefore && game.Pogs.ShotsUsed == 0;
            Require(report.guidedPracticeVerified, "Guided mobile verification did not preserve all three readable stage prompts without completing or launching shots");
            game.GoHub(); yield return SettleLayout();
            yield return Capture("18-mobile-hub-after-guided");
            Require(game.Screen == AppScreen.Hub && !game.GuidedPracticeMode && Time.timeScale == 1,
                "Guided mobile verification failed to restore the hub or clear guided mode");
        }

        IEnumerator VerifyRealMatch()
        {
            var card = game.Repository.PlayerCards.FirstOrDefault(item => game.Repository.CanStake(item.instanceId));
            Require(card != null && !game.Repository.HasPendingMatch, "The isolated mobile save cannot select an eligible real-match stake");
            if (card == null || game.Repository.HasPendingMatch) yield break;
            int playerCards = game.Repository.PlayerCards.Count;
            game.SelectCard(card.instanceId); game.Training = false; game.RealMatch = true;
            game.ShowLobby(); yield return SettleLayout();
            yield return Capture("19-mobile-real-lobby");
            VerifyMobilePage(AppScreen.Lobby, "mobile-lobby");
            game.StartPogs();
            report.realMatchVerified = game.Screen == AppScreen.Pogs && !game.Training && game.RealMatch && game.ActiveMatch != null &&
                game.ActiveMatch.isReal && !game.ActiveMatch.soloPractice && game.Repository.HasPendingMatch;
            Require(report.realMatchVerified, "Normal mobile real-match flow failed to start a real pending match: " + game.Error);
            if (!report.realMatchVerified) yield break;
            string matchId = game.ActiveMatch.matchId;
            float aimDeadline = Time.realtimeSinceStartup + 8;
            while (game.Pogs.Phase != MatchPhase.PlayerAim && Time.realtimeSinceStartup < aimDeadline) yield return null;
            yield return SettleLayout();
            yield return Capture("19-mobile-real-match");
            var board = MainRoot.Q(className: "mobile-scoreboard");
            var score = board?.Q<Label>(className: "mobile-score");
            var rule = board?.Q<Label>(className: "mobile-rule-caption");
            var turn = board?.Q<Label>(className: "mobile-turn-label");
            var labels = new[] { score, rule, turn };
            float tolerance = PanelPixelTolerance(MainRoot);
            report.realMatchHudVerified = board != null && IsFullyVisible(board.worldBound, MainRoot.worldBound, tolerance) &&
                labels.All(label => label != null && IsDisplayed(label) && !string.IsNullOrWhiteSpace(label.text) &&
                    label.resolvedStyle.fontSize >= 20 && IsFullyVisible(label.worldBound, board.worldBound, tolerance));
            Require(report.realMatchHudVerified, "Real mobile score, rule and turn labels must be readable and wholly inside the scoreboard");
            foreach (var label in labels)
                report.observations.Add("real-match HUD=" + DescribeElement(label) + " text=" + (label == null ? "missing" : label.text));
            VerifyArenaInput("real-match");
            Require(game.Pogs.Phase == MatchPhase.PlayerAim && game.Pogs.ShotsUsed == 0,
                "Real mobile layout validation did not reach aiming safely or unexpectedly launched a shot");
            game.GoHub(); yield return SettleLayout();
            yield return Capture("20-mobile-hub-with-pending-match");
            var pending = game.Repository.GetPendingMatch();
            var resume = MainRoot.Q<Button>("resume-match");
            report.realMatchResumeVerified = game.Screen == AppScreen.Hub && Time.timeScale == 1 && pending != null &&
                pending.matchId == matchId && pending.isReal && pending.paused && pending.checkpoint != null &&
                pending.checkpoint.shotsUsed == 0 && game.Repository.PlayerCards.Count == playerCards &&
                resume != null && resume.enabledInHierarchy && ButtonFontSize(resume) >= 20 &&
                IsFullyVisible(resume.worldBound, MainRoot.worldBound, tolerance);
            Require(report.realMatchResumeVerified, "Returning from the real match must retain the untouched stakes and expose a visible continue-match entry");
        }

        IEnumerator VerifyCollection()
        {
            game.ShowCollection(); yield return SettleLayout();
            yield return Capture("04-mobile-collection");
            var collection = MainRoot.Q("mobile-collection");
            var cards = MobileCollectionCards();
            float tolerance = PanelPixelTolerance(MainRoot);
            report.collectionVerified = game.Screen == AppScreen.Collection && collection != null && cards.Count == 6 &&
                cards.All(card => IsFullyVisible(card.worldBound, MainRoot.worldBound, tolerance));
            bool threeByTwo = cards.Count == 6 && Enumerable.Range(0, 2).All(row =>
                Enumerable.Range(0, 3).All(column => Mathf.Abs(cards[row * 3].worldBound.y - cards[row * 3 + column].worldBound.y) <= tolerance));
            var detail = collection?.Q("collection-detail");
            bool rightDetail = detail != null && cards.Count == 6 && detail.worldBound.xMin >= cards[2].worldBound.xMax - tolerance &&
                IsFullyVisible(detail.worldBound, MainRoot.worldBound, tolerance);
            report.collectionVerified &= threeByTwo && rightDetail;
            Require(report.collectionVerified, "Mobile collection must show exactly six cards in a visible 3x2 grid with a visible right detail panel");
            Require(GameCatalog.Cards.Length == 42 && MainRoot.Q<Button>("collection-filter-0") != null,
                "Collection's initial catalog/filter hook does not contain all 42 definitions");
            string[] firstIds = cards.Select(card => card.name).ToArray();
            var next = MainRoot.Q<Button>("mobile-page-next");
            var prev = MainRoot.Q<Button>("mobile-page-prev");
            bool initialPager = next != null && next.enabledInHierarchy && prev != null && !prev.enabledInHierarchy;
            Require(initialPager, "Collection's first page must allow Next and disable Previous");
            if (next == null) yield break;
            Submit(next); yield return SettleLayout();
            yield return Capture("05-mobile-collection-page-2");
            var secondCards = MobileCollectionCards();
            bool changedPage = secondCards.Count == 6 && !secondCards.Any(card => firstIds.Contains(card.name));
            prev = MainRoot.Q<Button>("mobile-page-prev");
            bool previousEnabled = prev != null && prev.enabledInHierarchy;
            Require(changedPage && previousEnabled, "Collection Next did not replace all six cards or enable Previous");
            var seen = new HashSet<string>(firstIds); foreach (var card in secondCards) seen.Add(card.name);
            report.collectionPagesVisited = 2;
            int expectedPages = Mathf.CeilToInt(GameCatalog.Cards.Length / 6f);
            for (int pageIndex = 2; pageIndex < expectedPages; pageIndex++)
            {
                next = MainRoot.Q<Button>("mobile-page-next");
                Require(next != null && next.enabledInHierarchy, "Collection cannot reach catalog page " + (pageIndex + 1));
                if (next == null || !next.enabledInHierarchy) break;
                Submit(next); yield return SettleLayout();
                yield return Capture("05-mobile-collection-page-" + (pageIndex + 1));
                var pageCards = MobileCollectionCards();
                int expectedCards = Mathf.Min(6, GameCatalog.Cards.Length - pageIndex * 6);
                Require(pageCards.Count == expectedCards && pageCards.All(card => !seen.Contains(card.name)),
                    "Collection page " + (pageIndex + 1) + " has missing or repeated catalog cards");
                foreach (var card in pageCards) seen.Add(card.name);
                report.collectionPagesVisited++;
            }
            report.collectionDefinitionsSeen = seen.Count;
            next = MainRoot.Q<Button>("mobile-page-next");
            bool completeCatalog = seen.Count == GameCatalog.Cards.Length && report.collectionPagesVisited == expectedPages &&
                next != null && !next.enabledInHierarchy;
            Require(completeCatalog, "Collection pagination must expose all 42 distinct definitions and disable Next on the last page");
            for (int pageIndex = report.collectionPagesVisited - 1; pageIndex > 0; pageIndex--)
            {
                prev = MainRoot.Q<Button>("mobile-page-prev");
                Require(prev != null && prev.enabledInHierarchy, "Collection cannot return through page " + pageIndex);
                if (prev == null || !prev.enabledInHierarchy) break;
                Submit(prev); yield return SettleLayout();
            }
            bool returnedPage = MobileCollectionCards().Select(card => card.name).SequenceEqual(firstIds);
            report.collectionPaginationVerified = initialPager && changedPage && previousEnabled && completeCatalog && returnedPage;
            Require(returnedPage, "Collection Previous did not restore the initial page's six cards");

            var history = MainRoot.Q<Button>("collection-show-history");
            Require(history != null, "Collection does not expose a detail/history action");
            if (history == null) yield break;
            Submit(history); yield return SettleLayout();
            yield return Capture("06-mobile-collection-detail");
            var sheet = MainRoot.Q("mobile-sheet") ?? MainRoot.Q(className: "mobile-sheet");
            var sheetScroll = sheet?.Q<ScrollView>(className: "mobile-sheet-scroll");
            var close = sheet?.Q<Button>("mobile-sheet-close");
            bool detailOpened = sheet != null && sheetScroll != null && close != null;
            Require(detailOpened, "Collection history did not open the bounded mobile detail sheet");
            if (close != null) Submit(close); else game.UI.HandleEscape();
            yield return SettleLayout();
            bool detailClosed = MainRoot.Q("mobile-sheet") == null && MainRoot.Q(className: "mobile-sheet") == null && game.Screen == AppScreen.Collection;
            Require(detailClosed, "Collection detail sheet did not close back to the current collection page");
            report.collectionVerified &= detailOpened && detailClosed;
        }

        List<Button> MobileCollectionCards()
        {
            return MainRoot.Query<Button>(className: "mobile-collection-card").ToList();
        }

        bool VerifyMobilePage(AppScreen expected, string name)
        {
            var page = MainRoot.Q(name);
            var topbar = MainRoot.Q("mobile-topbar") ?? MainRoot.Q(className: "mobile-topbar");
            bool passed = game.Screen == expected && page != null && topbar != null &&
                IsFullyVisible(page.worldBound, MainRoot.worldBound, PanelPixelTolerance(MainRoot)) &&
                page.Query<Button>().ToList().Any(button => button.ClassListContains("input-block") && IsDisplayed(button));
            report.observations.Add("page " + expected + " page=" + DescribeElement(page) + " topbar=" + DescribeElement(topbar));
            Require(passed, "Mobile " + expected + " must use a bounded page with a visible mobile topbar and native action buttons");
            return passed;
        }

        IEnumerator VerifyTradePreview(string prefix, string captureName)
        {
            int marbles = game.Repository.Current.glassMarbles, transactions = game.Repository.Current.transactions.Count;
            int playerCards = game.Repository.PlayerCards.Count;
            var card = MainRoot.Query<Button>(className: "mobile-shop-card").ToList()
                .FirstOrDefault(button => button.name.StartsWith(prefix, StringComparison.Ordinal) && button.enabledInHierarchy);
            Require(card != null, "Fresh mobile QA save has no enabled " + prefix + " card to verify the trade preview");
            if (card == null) yield break;
            Submit(card); yield return SettleLayout();
            yield return Capture(captureName);
            var sheet = MainRoot.Q("mobile-sheet") ?? MainRoot.Q(className: "mobile-sheet");
            var confirm = sheet?.Q<Button>("mobile-confirm-trade");
            var close = sheet?.Q<Button>("mobile-sheet-close");
            bool opened = sheet != null && confirm != null && close != null && !game.UI.AlertVisible;
            Require(opened, "Mobile trade preview did not open a sheet with explicit confirmation and close actions: " + captureName);
            if (close != null) Submit(close); else game.UI.HandleEscape();
            yield return SettleLayout();
            bool cancelled = game.Screen == AppScreen.Shop && MainRoot.Q("mobile-sheet") == null && MainRoot.Q(className: "mobile-sheet") == null;
            bool unchanged = marbles == game.Repository.Current.glassMarbles && transactions == game.Repository.Current.transactions.Count &&
                playerCards == game.Repository.PlayerCards.Count;
            Require(cancelled && unchanged, "Closing a mobile trade preview changed ownership, balance, transactions or failed to restore the shop");
            if (prefix == "buy-") report.buyPreviewVerified = opened && cancelled && unchanged;
            else report.sellPreviewVerified = opened && cancelled && unchanged;
        }

        IEnumerator VerifyErrandFlow()
        {
            int marbles = game.Repository.Current.glassMarbles, activities = game.Repository.Current.activities.Count;
            var accept = MainRoot.Query<Button>().ToList().FirstOrDefault(button =>
                button.name != null && button.name.StartsWith("accept-", StringComparison.Ordinal) && button.enabledInHierarchy);
            Require(accept != null && game.Repository.Current.activeErrand == null, "Fresh mobile QA save cannot accept a visible errand");
            if (accept == null || game.Repository.Current.activeErrand != null) yield break;
            Submit(accept); yield return SettleLayout();
            yield return Capture("10-mobile-errands-active");
            var pending = game.Repository.Current.activeErrand;
            var go = MainRoot.Q<Button>("errand-go-destination");
            bool accepted = pending != null && go != null && game.Screen == AppScreen.Errands;
            Require(accepted, "Mobile errand acceptance did not expose the active errand and destination action");
            if (!accepted) yield break;
            string destination = pending.destinationLocationId;
            Submit(go); yield return SettleLayout();
            bool arrived = game.Screen == AppScreen.Hub && game.LocationId == destination &&
                game.Repository.Current.activeErrand != null && game.Repository.Current.activeErrand.destinationVisited;
            Require(arrived, "Mobile errand destination action did not visit the required location");
            game.ShowErrands(); yield return SettleLayout();
            yield return Capture("10-mobile-errands-delivery");
            var deliver = MainRoot.Q<Button>("deliver-errand");
            Require(deliver != null && deliver.enabledInHierarchy, "Mobile active errand does not expose a delivery action after arrival");
            if (deliver == null || !deliver.enabledInHierarchy) yield break;
            Submit(deliver); yield return SettleLayout();
            yield return Capture("10-mobile-errands-reward");
            bool rewarded = game.Repository.Current.activeErrand == null && game.Repository.Current.glassMarbles == marbles + 6 &&
                game.Repository.Current.activities.Count == activities + 1 && game.UI.AlertVisible;
            Require(rewarded, "Mobile delivery action did not finish exactly one errand and award six marbles in the isolated QA save");
            game.UI.CloseAlert();
            report.errandFlowVerified = accepted && arrived && rewarded;
        }

        void VerifyMobileControls()
        {
            var row = MainRoot.Q(className: "mobile-match-controls");
            var buttons = row == null ? new List<Button>() : row.Query<Button>(className: "mobile-action-button").ToList();
            report.mobileControlsCount = buttons.Count;
            string[] labels = { "旋转左", "归零", "旋转右", "取消蓄力" };
            bool controlsPresent = buttons.Count == labels.Length && labels.All(label => buttons.Count(button => button.text == label) == 1);
            Require(controlsPresent, "Expected all four named mobile practice controls");
            if (!controlsPresent) return;
            // 新手机布局要求至少 52 个面板单位。像素对齐造成的舍入仍只容许一个物理像素。
            float pixelsPerPanelUnit = UnityEngine.Screen.height > 0 && MainRoot.worldBound.height > 0 ?
                UnityEngine.Screen.height / MainRoot.worldBound.height : 0;
            bool controlsLayout = pixelsPerPanelUnit > 0 && buttons.All(button => button.enabledInHierarchy &&
                button.worldBound.width >= 72 && button.worldBound.height > 0 && button.resolvedStyle.minHeight.value >= 52 &&
                ButtonFontSize(button) >= 22 && IsFullyVisible(button.worldBound, MainRoot.worldBound, PanelPixelTolerance(MainRoot)) &&
                (button.resolvedStyle.minHeight.value - button.worldBound.height) * pixelsPerPanelUnit <= 1f);
            foreach (var button in buttons)
                report.observations.Add("mobile control text=" + button.text + " enabled=" + button.enabledInHierarchy +
                    " layout=" + DescribeElement(button) + " resolvedMinHeight=" + button.resolvedStyle.minHeight +
                    " pixelsPerPanelUnit=" + pixelsPerPanelUnit + " heightRoundingPixels=" +
                    ((button.resolvedStyle.minHeight.value - button.worldBound.height) * pixelsPerPanelUnit) + " maxRoundingPixels=1");
            Require(controlsLayout, "Mobile practice controls must be visible, at least 72 units wide, request a minimum of 52 units and use readable 22-unit text; physical pixel rounding tolerance is one pixel");
            var shotChoices = Enumerable.Range(0, 3).Select(index => MainRoot.Q<Button>("mobile-shot-" + index)).ToArray();
            Require(shotChoices.All(button => button != null && button.enabledInHierarchy && ButtonFontSize(button) >= 22 &&
                button.worldBound.height + PanelPixelTolerance(MainRoot) >= 52 &&
                IsFullyVisible(button.worldBound, MainRoot.worldBound, PanelPixelTolerance(MainRoot))),
                "All three mobile shot-type choices must be visible, readable and touch-sized");
            game.Pogs.Spin = 0;
            Submit(buttons.Single(button => button.text == "旋转左"));
            bool left = Mathf.Approximately(game.Pogs.Spin, -.25f);
            Submit(buttons.Single(button => button.text == "旋转右"));
            bool right = Mathf.Approximately(game.Pogs.Spin, 0);
            game.Pogs.Spin = .75f;
            Submit(buttons.Single(button => button.text == "归零"));
            bool reset = Mathf.Approximately(game.Pogs.Spin, 0);
            int shots = game.Pogs.ShotsUsed;
            Submit(buttons.Single(button => button.text == "取消蓄力"));
            bool cancel = !game.Pogs.IsDragging && Mathf.Approximately(game.Pogs.Power, 0) && game.Pogs.ShotsUsed == shots;
            report.mobileControlsVerified = controlsLayout && left && right && reset && cancel;
            report.observations.Add("mobile callbacks left=" + left + " right=" + right + " reset=" + reset + " cancel=" + cancel);
            Require(left && right && reset && cancel, "Native mobile control callbacks did not update spin/cancel without launching a shot");
        }

        void VerifyArenaInput(string stage = "practice")
        {
            // 覆盖拇指拖动需要的中心九点，不能只留下一个未被遮挡的中心像素。
            float[] xs = { .35f, .5f, .65f }, ys = { .32f, .5f, .68f };
            foreach (float x in xs)
                foreach (float y in ys)
                {
                    Vector2 screenPoint = new Vector2(UnityEngine.Screen.width * x, UnityEngine.Screen.height * (1 - y));
                    bool free = !game.UI.BlocksInput(screenPoint);
                    report.arenaInputSamples++;
                    if (free) report.arenaFreeSamples++;
                    report.observations.Add("arena " + stage + " logicalPoint=" + new Vector2(MainRoot.worldBound.width * x, MainRoot.worldBound.height * y) +
                        " screenPoint=" + screenPoint + " free=" + free);
                    Require(free, "Mobile " + stage + " UI blocks the central drag area at normalized " + x + "," + y);
                }
            report.arenaInputVerified = report.arenaInputSamples >= 9 && report.arenaInputSamples % 9 == 0 &&
                report.arenaFreeSamples == report.arenaInputSamples;
            report.observations.Add("arena " + stage + " testedLogicalRect=" + new Rect(MainRoot.worldBound.width * .35f, MainRoot.worldBound.height * .32f,
                MainRoot.worldBound.width * .30f, MainRoot.worldBound.height * .36f) +
                " freeSamples=" + report.arenaFreeSamples + "/" + report.arenaInputSamples);
        }

        void VerifyLayoutQuality(string captureName)
        {
            var root = game.UI.AlertVisible ? AlertRoot : MainRoot;
            var surface = root;
            if (!game.UI.AlertVisible)
            {
                // 只评价最上层可交互浮层；被暂停/设置挡住的底页按钮不属于当前可点区域。
                surface = MainRoot.Q("exit-confirm") ?? MainRoot.Q("game-settings") ??
                    MainRoot.Q("mobile-sheet") ?? MainRoot.Q(className: "mobile-sheet") ??
                    (game.UI.PauseVisible ? MainRoot.Q("mobile-pause") ?? MainRoot.Query<VisualElement>(className: "overlay").ToList().LastOrDefault() : null) ?? MainRoot;
            }
            var entry = new LayoutObservation { name = captureName, screen = game.Screen.ToString(),
                surface = DescribeIdentity(surface), logicalWidth = root.worldBound.width, logicalHeight = root.worldBound.height,
                pixelsPerPanelUnit = root.worldBound.height > 0 ? UnityEngine.Screen.height / root.worldBound.height : 0,
                minButtonHeight = float.MaxValue, minButtonFontSize = float.MaxValue };
            var buttons = surface.Query<Button>().ToList().Where(button => button.ClassListContains("input-block") && IsDisplayed(button)).ToList();
            entry.customButtons = buttons.Count;
            float tolerance = PanelPixelTolerance(root);
            foreach (var button in buttons)
            {
                var card = ParentMobilePageCard(button);
                if (card != null && !IsFullyVisible(button.worldBound, card.worldBound, tolerance))
                {
                    entry.clippedCardActions++;
                    Require(false, "Mobile action overflows its page card in " + captureName + ": " + DescribeElement(button) +
                        " card=" + DescribeIdentity(card) + " cardBounds=" + card.worldBound);
                }
                var scroll = ParentScroll(button);
                Rect clip = root.worldBound;
                if (scroll != null) clip = Intersection(clip, scroll.contentViewport.worldBound);
                Rect visible = Intersection(button.worldBound, clip);
                if (visible.width <= tolerance || visible.height <= tolerance)
                {
                    if (scroll != null) { entry.scrollOffscreenButtons++; continue; }
                    entry.clippedButtons++;
                    Require(false, "Mobile button is outside the visible page in " + captureName + ": " + DescribeElement(button));
                    continue;
                }
                entry.visibleButtons++;
                float height = button.worldBound.height, fontSize = ButtonFontSize(button);
                entry.minButtonHeight = Mathf.Min(entry.minButtonHeight, height);
                entry.minButtonFontSize = Mathf.Min(entry.minButtonFontSize, fontSize);
                bool fullyVisible = IsFullyVisible(button.worldBound, clip, tolerance);
                if (!fullyVisible)
                {
                    entry.clippedButtons++;
                    Require(false, "Mobile action is partly cut off in " + captureName + ": " + DescribeElement(button) + " visible=" + visible + " viewport=" + clip);
                }
                // 工具栏/图鉴筛选有明确的 46–50 单位规格；其它按钮还须达到自身声明的最小高度。
                bool sized = button.worldBound.width + tolerance >= 64 && height + tolerance >= 46 &&
                    height + tolerance >= button.resolvedStyle.minHeight.value;
                if (!sized)
                {
                    entry.undersizedButtons++;
                    Require(false, "Mobile action is too small or below its own styled minimum in " + captureName + ": " + DescribeElement(button) +
                        " styledMinHeight=" + button.resolvedStyle.minHeight);
                }
                if (fontSize < 20)
                {
                    entry.unreadableButtons++;
                    Require(false, "Mobile action's main text is below 20 units in " + captureName + ": " + DescribeIdentity(button) + " font=" + fontSize);
                }
                foreach (var label in button.Query<Label>().ToList().Where(IsDisplayed))
                {
                    if (string.IsNullOrWhiteSpace(label.text) || IsFullyVisible(label.worldBound, button.worldBound, tolerance)) continue;
                    entry.clippedButtonLabels++;
                    Require(false, "Mobile button text overflows its touch card in " + captureName + ": " + DescribeIdentity(button) +
                        " text=" + label.text + " labelBounds=" + label.worldBound + " buttonBounds=" + button.worldBound);
                }
                bool elongated = button.worldBound.width > root.worldBound.width * .85f && height < 70 ||
                    button.worldBound.width > height * 10 && height < 70;
                if (elongated)
                {
                    entry.elongatedButtons++;
                    Require(false, "Mobile action uses an excessively long, thin strip in " + captureName + ": " + DescribeElement(button));
                }
                report.observations.Add("layout " + captureName + " button=" + DescribeIdentity(button) + " bounds=" + button.worldBound +
                    " font=" + fontSize + " enabled=" + button.enabledInHierarchy + " fullyVisible=" + fullyVisible + " scroll=" + DescribeIdentity(scroll));
            }
            if (entry.visibleButtons == 0) { entry.minButtonHeight = 0; entry.minButtonFontSize = 0; }
            entry.passed = entry.visibleButtons > 0 && entry.clippedButtons == 0 && entry.clippedCardActions == 0 && entry.clippedButtonLabels == 0 && entry.undersizedButtons == 0 &&
                entry.unreadableButtons == 0 && entry.elongatedButtons == 0;
            report.layoutQuality.Add(entry);
            report.layoutQualityChecks++;
            if (entry.passed) report.layoutQualityPasses++;
            report.layoutQualityVerified = report.layoutQualityChecks == report.layoutQualityPasses;
            Require(entry.visibleButtons > 0, "Mobile screen has no visible custom actions: " + captureName);
        }

        static bool IsDisplayed(VisualElement element)
        {
            for (var current = element; current != null; current = current.hierarchy.parent)
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }

        static ScrollView ParentScroll(VisualElement element)
        {
            for (var current = element.hierarchy.parent; current != null; current = current.hierarchy.parent)
                if (current is ScrollView scroll) return scroll;
            return null;
        }

        static VisualElement ParentMobilePageCard(VisualElement element)
        {
            for (var current = element.hierarchy.parent; current != null; current = current.hierarchy.parent)
                if (current.ClassListContains("mobile-page-card")) return current;
            return null;
        }

        static float ButtonFontSize(Button button)
        {
            if (!string.IsNullOrWhiteSpace(button.text)) return button.resolvedStyle.fontSize;
            var title = button.Q<Label>(className: "mobile-tile-title") ?? button.Q<Label>(className: "pog-name");
            if (title != null) return title.resolvedStyle.fontSize;
            return button.Query<Label>().ToList().Where(IsDisplayed).Select(label => label.resolvedStyle.fontSize).DefaultIfEmpty(0).Max();
        }

        static float PanelPixelTolerance(VisualElement root)
        {
            return UnityEngine.Screen.height > 0 ? root.worldBound.height / UnityEngine.Screen.height + .001f : .001f;
        }

        static bool IsFullyVisible(Rect rect, Rect viewport, float tolerance)
        {
            return rect.width > 1 && rect.height > 1 && rect.xMin >= viewport.xMin - tolerance && rect.yMin >= viewport.yMin - tolerance &&
                rect.xMax <= viewport.xMax + tolerance && rect.yMax <= viewport.yMax + tolerance;
        }

        static Rect Intersection(Rect first, Rect second)
        {
            float left = Mathf.Max(first.xMin, second.xMin), top = Mathf.Max(first.yMin, second.yMin);
            return new Rect(left, top, Mathf.Max(0, Mathf.Min(first.xMax, second.xMax) - left), Mathf.Max(0, Mathf.Min(first.yMax, second.yMax) - top));
        }

        static string DescribeElement(VisualElement element)
        {
            return element == null ? "missing" : DescribeIdentity(element) + " layout=" + element.layout +
                " worldBound=" + element.worldBound + " display=" + element.resolvedStyle.display;
        }

        static string DescribeIdentity(VisualElement element)
        {
            return element == null ? "missing" : element.GetType().Name + "#" + element.name +
                " classes=" + string.Join(",", element.GetClasses());
        }

        static void Submit(Button button)
        {
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }

        IEnumerator SettleLayout()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(.25f);
            yield return null;
        }

        IEnumerator Capture(string name)
        {
            // 显式渲染实际相机与运行时 UI，隐藏窗口也能取得真实图像。
            yield return null;
            if (game.Screen == AppScreen.Pogs) report.shotsUsed = Mathf.Max(report.shotsUsed, game.Pogs.ShotsUsed);
            int width = UnityEngine.Screen.width, height = UnityEngine.Screen.height;
            Require(width > 1 && height > 1, "Capture viewport is unavailable: " + name);
            if (width < 2 || height < 2) yield break;
            var entry = new CaptureObservation { name = name, path = Path.Combine(output, name + ".png"),
                screen = game.Screen.ToString(), width = width, height = height };
            report.captures.Add(entry);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            var camera = game.World.MainCamera;
            var previousCameraTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                target.Create(); camera.targetTexture = target; camera.Render();
                string uiError = game.UI.RenderVerificationFrame(target);
                Require(uiError == null, "Mobile UI verification rendering: " + uiError);
                string inputError = game.UI.VerifyInputBlocking();
                Require(inputError == null, "Mobile UI input: " + inputError);
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                var colors = new HashSet<int>();
                for (int i = 0; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 2048))
                {
                    var pixel = pixels[i];
                    if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) colors.Add((pixel.r << 16) | (pixel.g << 8) | pixel.b);
                }
                entry.nonBlank = colors.Count > 16;
                entry.rendered = uiError == null;
                Require(entry.nonBlank, "Mobile screenshot is blank or uniform: " + name);
                File.WriteAllBytes(entry.path, texture.EncodeToPNG());
                // 使用同一次原生 Repaint/Render 计算后的坐标验收，确保隐藏窗口不是未布局的假结果。
                VerifyLayoutQuality(name);
            }
            finally
            {
                camera.targetTexture = previousCameraTarget; RenderTexture.active = previousActive;
                if (texture != null) Destroy(texture);
                target.Release(); Destroy(target);
            }
        }

        void Require(bool condition, string error)
        {
            if (!condition) report.errors.Add(error);
        }

        void Finish(bool completed)
        {
            if (finished) return;
            finished = true;
            report.completed = completed;
            report.passed = completed && report.errors.Count == 0;
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.elapsedSeconds = Time.realtimeSinceStartup - startedAt;
            try { File.WriteAllText(Path.Combine(output, "mobile-startup-report.json"), JsonUtility.ToJson(report, true)); }
            catch (Exception exception)
            {
                report.passed = false;
                Debug.LogError("Mobile QA report could not be written: " + exception);
            }
            finally
            {
                Debug.Log("SUMMER_MOBILE_QA_COMPLETE completed=" + completed + " passed=" + report.passed + " errors=" + report.errors.Count);
                DisplaySettings.Quit(report.passed ? 0 : 1);
            }
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup > watchdog)
            { report.errors.Add("Mobile startup probe exceeded the 90-second real-time watchdog."); Finish(false); }
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
