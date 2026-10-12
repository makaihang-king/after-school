using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>仅 --smoke-test 时运行，使用独立测试存档，真正调用 PhysX 与游戏流程。</summary>
    public sealed class RuntimeProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool completed;
            public string engine;
            public int pogScore, ringOutScore;
            public bool restoredScore;
            public bool realMatchSettled;
            public bool polishControlsValid, layoutsGeometryValid, legacySnapshotsRestored, futureSnapshotRejected, layoutTestsPreservedSave;
            public int pogLayoutsVerified, layoutSnapshotRestores, flightCancelChecks;
            public int displayModesVerified, displayLifecycleRestores, environmentCombinationsVerified, sceneVariantsVerified;
            public bool displayTimeoutRestored, userDisplayPreferencesUnchanged, storyFlowVerified, tradeFlowVerified, practiceModalVerified, environmentResumeVerified;
            public bool tournamentFlowVerified, errandFlowVerified, swapFlowVerified, sceneResourcesReleased, topDownViewVerified;
            public List<string> errors = new List<string>();
            public List<string> observations = new List<string>();
        }
        Report report = new Report();
        [Serializable] sealed class ShotFeedbackReport
        {
            public bool completed, passed, isolatedSave, mobileUI, launched, physicsSettled, scoreUnchanged;
            public bool silentMissFeedback, highlightCleared, visibleLabelsClean, snapshotMissFeedbackCleared, snapshotBoardPreserved;
            public string engine, platform, savePath, lastFeedback, message, highlight, snapshotFeedback;
            public int playerScoreBefore, playerScoreAfter, npcScoreBefore, npcScoreAfter, shotsUsed, shotSettledEvents, snapshotVersion;
            public float elapsedSeconds;
            public ShotObservation shot;
            public List<string> errors = new List<string>();
            public List<string> observations = new List<string>();
        }
        readonly ShotFeedbackReport feedbackReport = new ShotFeedbackReport();
        bool shotFeedbackMode;
        float feedbackStartedAt;
        string output;
        SummerGame game;
        float watchdog;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootProbe()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Contains("--smoke-test") && !arguments.Contains("--free-angle-experience-test") && !arguments.Contains("--companion-experience-test") && !arguments.Contains("--qa-manual-inspection") && (arguments.Contains("--shot-feedback-test") ||
                (!arguments.Contains("--guided-experience-test") && !arguments.Contains("--mobile-ui-test") && !arguments.Contains("--campaign-ui-test") && !arguments.Contains("--display-write-test") && !arguments.Contains("--display-readback-test"))))
                new GameObject("运行验证").AddComponent<RuntimeProbe>();
        }

        void Awake()
        {
            shotFeedbackMode = Environment.GetCommandLineArgs().Contains("--shot-feedback-test");
            feedbackStartedAt = Time.realtimeSinceStartup;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA"));
            if (shotFeedbackMode) output = Path.Combine(output, "ShotFeedback");
            else if (Environment.GetCommandLineArgs().Contains("--v04-smoke")) output = Path.Combine(output, "V04");
            else if (Environment.GetCommandLineArgs().Contains("--polish-smoke")) output = Path.Combine(output, "Polish");
            else if (Environment.GetCommandLineArgs().Contains("--visual-smoke")) output = Path.Combine(output, "Visual");
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++) if (arguments[i] == "--qa-output") output = Path.GetFullPath(arguments[i + 1]);
            Directory.CreateDirectory(output); report.engine = Application.unityVersion;
            Application.logMessageReceived += OnLog;
            watchdog = Time.realtimeSinceStartup + (shotFeedbackMode ? 60 : 1000);
            if (shotFeedbackMode) Application.runInBackground = true;
        }
        void OnLog(string value, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(value + "\n" + stack);
        }

        IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object current = null; bool advanced = false, failed = false;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception exception) { report.errors.Add("Probe exception: " + exception); CompleteReport(false); failed = true; }
                if (failed) yield break;
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
                else yield return current;
            }
            if (!finished) CompleteReport();
        }
        IEnumerator Run()
        {
            yield return new WaitUntil(() => FindFirstObjectByType<SummerGame>() != null);
            game = FindFirstObjectByType<SummerGame>();
            if (shotFeedbackMode)
            {
                yield return VerifySilentShotFeedback(); CompleteReport(); yield break;
            }
            game.Pogs.PhysicsDiagnostic += value => report.observations.Add(value);
            yield return new WaitForSecondsRealtime(2);
            yield return Capture("01-hub");
            ObserveAudio(AudioMood.Explore, "Hub");
            if (Resources.Load<AudioClip>("Audio/summer_collection") == null) report.errors.Add("Collection music resource summer_collection is missing");
            string controlsError = game.UI.VerifyPolishControls();
            report.polishControlsValid = controlsError == null;
            if (controlsError != null) report.errors.Add("Polish controls: " + controlsError);
            string exitError = game.UI.VerifyExitConfirm();
            if (exitError != null) report.errors.Add("Exit confirm: " + exitError);
            game.UI.ShowExitConfirm(); yield return Capture("17-exit-confirm", false); game.UI.HandleEscape();
            game.UI.ShowAudioSettings(); yield return Capture("14-audio-settings", false); game.UI.HandleEscape();
            if (Environment.GetCommandLineArgs().Contains("--v04-smoke"))
            {
                yield return VerifyV04(); CompleteReport(); yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--visual-smoke"))
            {
                game.ShowLobby(); yield return Capture("02-lobby");
                game.RealMatch = true; game.Training = false; game.RefreshUI(); yield return Capture("16-real-lobby");
                game.RealMatch = false; game.GoHub();
                game.ShowCollection(); yield return Capture("06-collection"); game.GoHub();
                game.SelectedPracticeLayout = PracticeLayout.Fan; game.SelectedRule = RuleKind.Flip; game.Training = true;
                game.StartPogs(); yield return new WaitForSecondsRealtime(.8f); yield return Capture("03-pogs");
                if (game.Pogs.Layout != PracticeLayout.Fan || game.Screen != AppScreen.Pogs) report.errors.Add("Selected pog layout did not reach playable match");
                game.Forfeit(); game.GoHub();
                foreach (var location in GameCatalog.Locations)
                {
                    game.SelectLocation(location.id); yield return new WaitForSecondsRealtime(.7f);
                    yield return Capture("location-" + location.id);
                }
                game.GoHub(); CompleteReport(); yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--polish-smoke"))
            {
                game.ShowCollection(); ObserveAudio(AudioMood.Collection, "Collection");
                yield return Capture("06-collection"); game.GoHub();
                yield return VerifyGameplayLayouts();
                game.Audio.SetContext(game.LocationId, AudioMood.Result); ObserveAudio(AudioMood.Result, "Isolated Result API");
                game.GoHub(); ObserveAudio(AudioMood.Explore, "Return Hub");
                CompleteReport(); yield break;
            }
            game.ShowLobby(); yield return new WaitForSecondsRealtime(.4f); yield return Capture("02-lobby");
            game.Training = true; game.RealMatch = false; game.StartPogs();
            report.observations.Add("StartPogs screen=" + game.Screen + ", error=" + game.Error);
            if (game.Screen != AppScreen.Pogs) report.errors.Add("Pog match could not start: " + game.Error);
            ObserveAudio(AudioMood.Match, "Pog match");
            yield return new WaitForSecondsRealtime(1.5f); yield return Capture("03-pogs");
            int attempts = 0;
            float practiceDeadline = Time.realtimeSinceStartup + 220;
            while (game.Screen == AppScreen.Pogs && attempts < 14 && Time.realtimeSinceStartup < practiceDeadline)
            {
                if (game.Pogs.Phase != MatchPhase.PlayerAim) { yield return null; continue; }
                attempts++;
                var target = game.Pogs.Targets.Where(t => !t.Claimed).OrderBy(t => t.transform.position.z).FirstOrDefault();
                if (target == null) break;
                Vector3 delta = target.transform.position - new Vector3(0, 0, -2.25f); delta.y = 0;
                float power = Mathf.Clamp01((delta.magnitude - 1.25f) / 3.3f);
                game.Pogs.Launch(delta.normalized, power, attempts % 3 == 0 ? .3f : 0, ShotKind.Slap);
                if (attempts == 1) { yield return new WaitForSecondsRealtime(.22f); yield return Capture("04-impact"); }
                float start = Time.realtimeSinceStartup;
                while (game.Pogs.Phase == MatchPhase.Flying && Time.realtimeSinceStartup - start < 18) yield return null;
                report.observations.Add("Pog shot " + attempts + ": score=" + game.Pogs.PlayerScore + ", phase=" + game.Pogs.Phase);
                report.observations.Add(string.Join(";", game.Pogs.Targets.Where(t => !t.Claimed).Select(t => "disc" + t.ArtIndex + " pos=" + t.transform.position + " normal=" + Vector3.Dot(t.transform.up, Vector3.up).ToString("0.00"))));
                if (game.Pogs.Phase == MatchPhase.Flying) { report.errors.Add("Physics did not settle within 18 seconds"); break; }
                if (attempts == 1 && game.Screen == AppScreen.Pogs)
                {
                    int before = game.Pogs.PlayerScore; game.GoHub(); game.ResumePogs();
                    yield return new WaitForSecondsRealtime(1);
                    report.restoredScore = before == game.Pogs.PlayerScore;
                    if (!report.restoredScore) report.errors.Add("Paused game did not restore the same score");
                    if (!game.Pogs.Practice || !(game.Repository.GetPendingMatch()?.soloPractice ?? false)) report.errors.Add("Solo practice became an NPC match after resume");
                }
                yield return new WaitForSecondsRealtime(.7f);
            }
            report.pogScore = game.Pogs.PlayerScore;
            if (report.pogScore == 0) report.errors.Add("Directed slap shots never produced a settled flip");
            yield return Capture("05-pog-result");
            if (game.Repository.GetPendingMatch() != null) game.Forfeit();
            // 完整出圈与真实赌注走同一个公开玩法流程，结算必须清除约战且只转移约定实例。
            game.GoHub(); game.SelectLocation("school_gate"); game.ShowLobby();
            game.Training = false; game.RealMatch = true; game.SelectedRule = RuleKind.RingOut;
            var stake = game.Repository.PlayerCards.First(c => c.CanTrade);
            game.SelectCard(stake.instanceId); game.StartPogs();
            var pending = game.Repository.GetPendingMatch();
            if (pending == null) report.errors.Add("Real ring-out match could not start: " + game.Error);
            else
            {
                string opponentStake = pending.npcStakeIds[0];
                float deadline = Time.realtimeSinceStartup + 200;
                yield return new WaitForSecondsRealtime(1); yield return Capture("12-ring-out");
                while (game.Screen == AppScreen.Pogs && Time.realtimeSinceStartup < deadline)
                {
                    if (game.Pogs.Phase == MatchPhase.PlayerAim)
                    {
                        var chosen = game.Pogs.Targets.Where(t => !t.Claimed).OrderBy(t => t.transform.position.z).FirstOrDefault();
                        if (chosen == null) break;
                        Vector3 direction = chosen.transform.position - new Vector3(0, 0, -2.25f); direction.y = 0;
                        game.Pogs.Launch(direction.normalized, .84f, 0, ShotKind.Skim);
                    }
                    yield return null;
                }
                report.ringOutScore = game.Pogs.PlayerScore;
                if (report.ringOutScore == 0 && game.Pogs.NpcScore == 0) report.errors.Add("Full ring-out match never produced a score");
                report.realMatchSettled = game.Screen == AppScreen.Result && !game.Repository.HasPendingMatch;
                if (!report.realMatchSettled) { report.errors.Add("Real ring-out match did not reach settlement"); game.Forfeit(); }
                else
                {
                    ObserveAudio(AudioMood.Result, "Real pog result");
                    int playerScore = game.Pogs.PlayerScore, npcScore = game.Pogs.NpcScore;
                    string expectedPlayerStakeOwner = playerScore < npcScore ? game.NpcId : GameCatalog.PlayerOwnerId;
                    string expectedNpcStakeOwner = playerScore > npcScore ? GameCatalog.PlayerOwnerId : game.NpcId;
                    if (game.Repository.GetInstance(stake.instanceId).ownerId != expectedPlayerStakeOwner ||
                        game.Repository.GetInstance(opponentStake).ownerId != expectedNpcStakeOwner)
                        report.errors.Add("Real match ownership did not match the actual score");
                    report.observations.Add("Real ring-out result " + playerScore + ":" + npcScore + "; agreed stakes settled and pending cleared");
                }
                yield return Capture("13-real-match-result");
            }
            game.GoHub(); game.ShowCollection(); yield return new WaitForSecondsRealtime(.7f); yield return Capture("06-collection");
            ObserveAudio(AudioMood.Collection, "Collection");
            game.GoHub(); game.SelectLocation("alley"); yield return new WaitForSecondsRealtime(.8f); yield return Capture("09-alley");
            game.SelectLocation("old_tree"); yield return new WaitForSecondsRealtime(.8f); yield return Capture("10-tree");
            game.SelectLocation("home"); yield return new WaitForSecondsRealtime(.8f); yield return Capture("11-home");
            ObserveAudio(AudioMood.Explore, "Home Hub");
            yield return VerifyGameplayLayouts();
            CompleteReport();
        }

        void ObserveAudio(AudioMood expected, string label)
        {
            report.observations.Add(label + " audio location=" + game.Audio.LocationId + " mood=" + game.Audio.Mood);
            if (game.Audio.LocationId != game.LocationId || game.Audio.Mood != expected)
                report.errors.Add(label + " audio context did not follow location/mood");
        }

        IEnumerator VerifySilentShotFeedback()
        {
            feedbackReport.engine = Application.unityVersion;
            feedbackReport.platform = Application.platform.ToString();
            feedbackReport.errors = report.errors; feedbackReport.observations = report.observations;
            if (game == null || !game.enabled || game.Repository == null || game.Pogs == null || game.UI == null || game.World == null)
            { report.errors.Add("Shot-feedback startup is incomplete: " + (game == null ? "game missing" : game.Error)); yield break; }
            string expectedDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/save-v04"));
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++) if (arguments[i] == "--qa-save") expectedDirectory = Path.GetFullPath(arguments[i + 1]);
            feedbackReport.savePath = game.Repository.SavePath;
            string actualSave = Path.GetFullPath(feedbackReport.savePath);
            string normalSave = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "SummerAfterSchool-v2", "collection.json"));
            feedbackReport.isolatedSave = string.Equals(actualSave, Path.Combine(expectedDirectory, "collection.json"), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(actualSave, normalSave, StringComparison.OrdinalIgnoreCase);
            if (!feedbackReport.isolatedSave || game.Repository.HasPendingMatch)
            { report.errors.Add("Shot-feedback mode requires the expected fresh isolated save without a pending match: " + actualSave); yield break; }

            game.SelectLocation("home"); game.ShowLobby(); game.Training = true; game.RealMatch = false;
            game.SelectedRule = RuleKind.Flip; game.SelectedPracticeLayout = PracticeLayout.Classic;
            game.StartPogs();
            yield return WaitForState(() => game.Screen == AppScreen.Pogs && game.Pogs.Phase == MatchPhase.PlayerAim, 6, "Silent-shot initial aim");
            if (game.Screen != AppScreen.Pogs || game.Pogs.Phase != MatchPhase.PlayerAim)
            { report.errors.Add("Silent-shot practice did not start: " + game.Error); yield break; }
            var root = FeedbackMainRoot();
            feedbackReport.mobileUI = root != null && root.ClassListContains("mobile-ui");
            if (feedbackReport.mobileUI != arguments.Contains("--mobile-ui-test"))
                report.errors.Add("Shot-feedback mode did not select the requested desktop/mobile UI branch");

            var pog = game.Pogs;
            int playerBefore = pog.PlayerScore, npcBefore = pog.NpcScore, claimedBefore = pog.Targets.Count(target => target.Claimed);
            feedbackReport.playerScoreBefore = playerBefore; feedbackReport.npcScoreBefore = npcBefore;
            int settled = 0, highlights = 0;
            Action<ShotObservation> onSettled = observation =>
            {
                settled++; feedbackReport.shot = observation;
                feedbackReport.lastFeedback = pog.LastFeedback; feedbackReport.message = pog.Message;
                feedbackReport.playerScoreAfter = pog.PlayerScore; feedbackReport.npcScoreAfter = pog.NpcScore;
            };
            Action<string> onHighlight = value => { highlights++; feedbackReport.highlight = value; };
            pog.ShotSettled += onSettled; pog.Highlight += onHighlight;
            try
            {
                // 朝目标相反方向低力度掌拍，由真正的刚体运动、地面碰撞和静止判定制造未得分回合。
                pog.Launch(Vector3.back, .08f, 0, ShotKind.Slap);
                feedbackReport.launched = pog.Phase == MatchPhase.Flying && pog.ShotsUsed == 1;
                if (!feedbackReport.launched) { report.errors.Add("Silent-shot launch did not start one real PhysX flight"); yield break; }
                yield return WaitForState(() => settled > 0 && pog.Phase != MatchPhase.Flying, 22, "Silent-shot PhysX settle");
                feedbackReport.shotSettledEvents = settled; feedbackReport.shotsUsed = pog.ShotsUsed;
                var shot = feedbackReport.shot;
                feedbackReport.physicsSettled = settled == 1 && pog.ShotsUsed == 1 && shot != null &&
                    shot.playerControlled && shot.shotKind == ShotKind.Slap && shot.maxMovement > .2f;
                feedbackReport.scoreUnchanged = shot != null && !shot.scored && shot.scoredTargetCount == 0 &&
                    playerBefore == pog.PlayerScore && npcBefore == pog.NpcScore && claimedBefore == pog.Targets.Count(target => target.Claimed);
                feedbackReport.silentMissFeedback = shot != null && string.IsNullOrEmpty(feedbackReport.lastFeedback) &&
                    string.IsNullOrEmpty(feedbackReport.message) && string.IsNullOrEmpty(pog.LastFeedback);
                feedbackReport.highlightCleared = highlights == 1 && string.IsNullOrEmpty(feedbackReport.highlight);
                if (!feedbackReport.physicsSettled) report.errors.Add("Miss shot lost its single normal player-controlled PhysX ShotSettled observation");
                if (!feedbackReport.scoreUnchanged) report.errors.Add("The deliberately missed shot unexpectedly changed score or claimed targets");
                if (!feedbackReport.silentMissFeedback || !feedbackReport.highlightCleared)
                    report.errors.Add("Settled miss must leave LastFeedback, Message and the emitted highlight empty");
                yield return null;
                CaptureFeedbackFrame("shot-feedback-settled");
                feedbackReport.visibleLabelsClean = VerifyFeedbackLabels("settled");
                if (shot == null) yield break;

                // 保留真实 v3 盘面的版本、几何、厚度、环境与计数，只注入旧失败文字来验收提示兼容。
                // 直接 RestoreBoard，不重新 Begin，也不触发旧几何迁移或向协调器保存重置后的零次进度。
                var snapshot = JsonUtility.FromJson<PogMatch.BoardState>(pog.CaptureBoard());
                feedbackReport.snapshotVersion = snapshot.version;
                report.observations.Add("Snapshot miss compatibility phase=" + pog.Phase + " turn=" + pog.Turn +
                    " nextTurnBase=" + snapshot.nextTurnBase + " version=" + snapshot.version + " geometryVersion=" + snapshot.geometryVersion);
                if (pog.Phase != MatchPhase.Waiting || snapshot.nextTurnBase != pog.Turn)
                {
                    report.errors.Add("Miss-wording snapshot must be restored in Waiting with its unchanged turn base; phase=" + pog.Phase +
                        " turn=" + pog.Turn + " nextTurnBase=" + snapshot.nextTurnBase);
                    yield break;
                }
                snapshot.playerMissStreak = Mathf.Max(1, snapshot.playerMissStreak);
                snapshot.lastFeedback = "这一手没有取得进展。换个角度，再试一次。";
                bool restored = pog.RestoreBoard(JsonUtility.ToJson(snapshot));
                feedbackReport.snapshotFeedback = pog.LastFeedback;
                feedbackReport.snapshotMissFeedbackCleared = restored && string.IsNullOrEmpty(pog.LastFeedback);
                snapshot.lastFeedback = "";
                var restoredBoard = JsonUtility.FromJson<PogMatch.BoardState>(pog.CaptureBoard());
                feedbackReport.snapshotBoardPreserved = restored && SamePogBoard(snapshot, restoredBoard);
                if (!feedbackReport.snapshotMissFeedbackCleared) report.errors.Add("Restoring a current-format snapshot replayed its old miss wording");
                if (!feedbackReport.snapshotBoardPreserved) report.errors.Add("Clearing snapshot miss wording changed geometry, board, counters or scores: " + DescribePogDifference(snapshot, restoredBoard));
                yield return WaitForState(() => pog.Phase == MatchPhase.PlayerAim, 6, "Silent-shot next player aim");
                CaptureFeedbackFrame("shot-feedback-restored");
                feedbackReport.visibleLabelsClean &= VerifyFeedbackLabels("restored");
                if (ContainsMissText(pog.Message) || ContainsMissText(pog.LastFeedback)) report.errors.Add("The next aiming phase replayed failure wording");
                report.observations.Add("Silent shot UI=" + (feedbackReport.mobileUI ? "mobile" : "desktop") +
                    " score=" + playerBefore + ":" + npcBefore + " -> " + pog.PlayerScore + ":" + pog.NpcScore +
                    " settled=" + settled + " movement=" + shot.maxMovement + " contacts=" + shot.collisionCount +
                    " targetContacts=" + shot.targetCollisionCount + " snapshotVersion=" + feedbackReport.snapshotVersion +
                    " snapshotMissCleared=" + feedbackReport.snapshotMissFeedbackCleared + " snapshotBoardPreserved=" + feedbackReport.snapshotBoardPreserved);
            }
            finally { pog.ShotSettled -= onSettled; pog.Highlight -= onHighlight; }
            // 只留下隔离存档中的稳定暂停练习；不进行后续长回归和额外出手。
            game.GoHub();
        }

        VisualElement FeedbackMainRoot()
        {
            var document = game.GetComponentsInChildren<UIDocument>(true).FirstOrDefault(item => item.panelSettings != null && item.panelSettings.sortingOrder == 20);
            return document == null ? null : document.rootVisualElement;
        }

        bool VerifyFeedbackLabels(string stage)
        {
            var root = FeedbackMainRoot();
            var labels = root == null ? new List<Label>() : root.Query<Label>().ToList().Where(label => label.visible).ToList();
            bool clean = labels.Count > 0 && labels.All(label => !ContainsMissText(label.text));
            foreach (var label in labels) report.observations.Add("Shot-feedback " + stage + " label " + string.Join(",", label.GetClasses()) + "=" + label.text);
            if (!clean) report.errors.Add("The " + stage + " native UI contains miss/failure wording or has no readable labels");
            var highlight = root?.Q<Label>(className: "highlight");
            if (highlight == null || !string.IsNullOrEmpty(highlight.text))
            { clean = false; report.errors.Add("The " + stage + " visible highlight was not cleared"); }
            return clean;
        }

        static bool ContainsMissText(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string[] phrases = { "没有取得进展", "没取得进展", "没有进展", "没有得分", "未得分", "这一手没", "没翻", "没碰", "没打到", "没出圈", "no progress" };
            return phrases.Any(phrase => value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        void CaptureFeedbackFrame(string name)
        {
            // 快速专用模式直接渲染原生面板，不依赖隐藏窗口的 WaitForEndOfFrame。
            int width = UnityEngine.Screen.width, height = UnityEngine.Screen.height;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var camera = game.World.MainCamera; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                target.Create(); camera.targetTexture = target; camera.Render();
                string error = game.UI.RenderVerificationFrame(target);
                if (error != null) report.errors.Add("Shot-feedback native UI render: " + error);
                RenderTexture.active = target; texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                if (texture != null) Destroy(texture); target.Release(); Destroy(target);
            }
        }

        IEnumerator VerifyV04()
        {
            if (!Environment.GetCommandLineArgs().Contains("--smoke-test"))
            { report.errors.Add("--v04-smoke requires --smoke-test save isolation."); yield break; }
            yield return VerifyDisplayModes();
            yield return VerifyEnvironments();
            yield return VerifyGameplayLayouts();
            yield return VerifyProductFlows();
        }

        IEnumerator VerifyProductFlows()
        {
            game.GoHub();
            if (game.Repository.HasPendingMatch) { game.Forfeit(); game.GoHub(); }
            game.ShowStory(); yield return Capture("v04-story-list");
            game.OpenStoryStage("story_01"); yield return null;
            var foundation = game.Repository.PlayerCards.FirstOrDefault(card => card.isFoundation);
            if (foundation == null) { report.errors.Add("Practice-card popup QA has no foundation card."); yield break; }
            string beforeRestriction = JsonUtility.ToJson(game.Repository.Current);
            game.SelectCard(foundation.instanceId); game.UI.ShowAudioSettings(); game.UI.ShowPracticeRestriction(); game.StartPogs();
            yield return Capture("v04-practice-topmost", false);
            report.practiceModalVerified = game.Screen == AppScreen.Lobby && game.UI.AlertVisible && game.UI.SettingsVisible &&
                (game.UI.AlertMessage ?? "").Contains("练习片不能玩真的") && !game.Repository.HasPendingMatch &&
                beforeRestriction == JsonUtility.ToJson(game.Repository.Current) &&
                game.UI.BlocksInput(new Vector2(UnityEngine.Screen.width * .5f, UnityEngine.Screen.height * .5f)) &&
                game.UI.BlocksInput(new Vector2(UnityEngine.Screen.width * .05f, UnityEngine.Screen.height * .05f));
            if (!report.practiceModalVerified) report.errors.Add("Practice restriction was not topmost, blocked, non-mutating, or prevented a real story match.");
            if (!ClickNamedControl("choose-real-card")) report.errors.Add("Practice popup has no enabled choose-real-card action.");
            yield return null;
            if (game.UI.AlertVisible || game.SelectedCard == null || game.SelectedCard.isFoundation)
            { report.errors.Add("Practice popup did not select a real card through its visible action."); yield break; }
            if (game.UI.SettingsVisible) game.UI.HandleEscape();
            game.StartPogs();
            yield return WaitForState(() => game.Screen == AppScreen.Pogs && game.Pogs.Phase == MatchPhase.PlayerAim, 6, "Story real match aim");
            var pending = game.Repository.GetPendingMatch();
            if (pending == null || pending.route != MatchRoute.Story || pending.stageId != "story_01" || !pending.isReal || pending.soloPractice)
            { report.errors.Add("Story did not start a real staged match."); yield break; }
            // 真实对局中验证俯视镜头入口，随后恢复玩家进入对局前的视角。
            bool wasTopDown = game.World.IsTopDownMatchView;
            game.ToggleMatchCamera();
            yield return new WaitForSecondsRealtime(.9f);
            Vector3 topDownPosition = game.World.MainCamera == null ? Vector3.zero : game.World.MainCamera.transform.position;
            report.topDownViewVerified = game.World.IsTopDownMatchView && topDownPosition.y > 10f &&
                Mathf.Abs(topDownPosition.x) < .2f && Mathf.Abs(topDownPosition.z) < .65f;
            report.observations.Add("Top-down match view enabled=" + game.World.IsTopDownMatchView + " camera=" + topDownPosition);
            if (!report.topDownViewVerified) report.errors.Add("Top-down match view did not move the camera to the playable overhead position.");
            yield return Capture("v04-match-topdown", false);
            if (game.World.IsTopDownMatchView != wasTopDown) game.ToggleMatchCamera();
            string frozenEnvironment = JsonUtility.ToJson(pending.environment);
            string stable = game.Pogs.CaptureBoard();
            game.GoHub(); game.SelectLocation("square"); game.ResumePogs();
            yield return WaitForState(() => game.Pogs.Phase == MatchPhase.PlayerAim, 6, "Story resumed aim");
            report.environmentResumeVerified = frozenEnvironment == JsonUtility.ToJson(game.World.Environment) &&
                frozenEnvironment == JsonUtility.ToJson(game.Repository.GetPendingMatch()?.environment) &&
                SamePogBoard(JsonUtility.FromJson<PogMatch.BoardState>(stable), JsonUtility.FromJson<PogMatch.BoardState>(game.Pogs.CaptureBoard()));
            if (!report.environmentResumeVerified) report.errors.Add("Story resume changed the frozen environment, geometry, layout, score or stable poses.");
            yield return Capture("v04-story-resumed");
            float deadline = Time.realtimeSinceStartup + 200;
            int shots = 0;
            while (game.Screen == AppScreen.Pogs && Time.realtimeSinceStartup < deadline)
            {
                if (game.Pogs.Phase == MatchPhase.PlayerAim)
                {
                    var target = game.Pogs.Targets.Where(disc => !disc.Claimed).OrderBy(disc => disc.Body.position.z).FirstOrDefault();
                    if (target == null) break;
                    Vector3 direction = target.Body.position - new Vector3(0, 0, -2.25f); direction.y = 0;
                    float power = Mathf.Clamp01((direction.magnitude - 1.25f) / 3.3f);
                    game.Pogs.Launch(direction.normalized, power, shots++ % 3 == 0 ? .3f : 0, ShotKind.Slap);
                }
                yield return null;
            }
            var settlement = game.LastSettlement;
            report.storyFlowVerified = game.Screen == AppScreen.Result && settlement != null && settlement.matchId == pending.matchId &&
                settlement.route == MatchRoute.Story && !game.Repository.HasPendingMatch && shots > 0;
            if (!report.storyFlowVerified)
            { report.errors.Add("Real story shots did not reach an atomic staged result."); game.Forfeit(); }
            else
            {
                string playerOwner = settlement.outcome == MatchOutcome.NpcWin ? pending.npcId : GameCatalog.PlayerOwnerId;
                string npcOwner = settlement.outcome == MatchOutcome.PlayerWin ? GameCatalog.PlayerOwnerId : pending.npcId;
                if (game.Repository.GetInstance(pending.playerStakeIds[0]).ownerId != playerOwner || game.Repository.GetInstance(pending.npcStakeIds[0]).ownerId != npcOwner)
                    report.errors.Add("Story result did not transfer exactly the agreed stakes.");
                report.observations.Add("Story real result outcome=" + settlement.outcome + " directedShots=" + shots + " firstClear=" + settlement.firstClear);
            }
            yield return Capture("v04-story-result", false); game.GoHub();

            game.ShowShop(); yield return Capture("v04-shop");
            var offer = CampaignCatalog.ShopOffers.FirstOrDefault(item => !item.isFoundation && item.buyPrice <= game.Repository.Current.glassMarbles && CampaignCatalog.IsOfferUnlocked(game.Repository.Current, item));
            if (offer == null) { report.errors.Add("Trade QA cannot afford any unlocked real card."); yield break; }
            int transactionCount = game.Repository.Current.transactions.Count, funds = game.Repository.Current.glassMarbles;
            var buy = game.Repository.GetTradeQuote(TradeKind.Buy, offer.id);
            if (!ClickNamedControl("shop-tab-0")) { report.errors.Add("Shop has no enabled buy tab."); yield break; }
            yield return null;
            if (!ClickNamedControl("buy-" + offer.id)) { report.errors.Add("Shop has no enabled buy button for " + offer.id); yield break; }
            yield return null;
            var purchase = game.Repository.Current.transactions.Skip(transactionCount).SingleOrDefault(item => item.kind == TradeKind.Buy && item.offerId == offer.id);
            if (purchase == null || purchase.gainedCardIds.Count != 1 || game.Repository.Current.glassMarbles != funds + buy.marbleDelta)
            { report.errors.Add("Buy flow did not save its exact balance and gained instance."); yield break; }
            // UI 创建自己的交易编号，后续重放必须使用该次按钮交易的真实编号。
            buy.quoteId = purchase.quoteId;
            string afterPurchase = JsonUtility.ToJson(game.Repository.Current);
            game.UI.CloseAlert(); game.ExecuteTrade(buy, false); game.UI.CloseAlert();
            if (afterPurchase != JsonUtility.ToJson(game.Repository.Current)) report.errors.Add("Repeated buy confirmation duplicated cards or currency.");
            string boughtId = purchase.gainedCardIds[0];
            var sell = game.Repository.GetTradeQuote(TradeKind.Sell, "shop", boughtId);
            if (!ClickNamedControl("shop-tab-1")) { report.errors.Add("Shop has no enabled sell tab."); yield break; }
            yield return null;
            if (!ClickNamedControl("sell-" + boughtId)) { report.errors.Add("Shop has no enabled sell button for purchased instance " + boughtId); yield break; }
            yield return null; game.UI.CloseAlert();
            report.tradeFlowVerified = game.Repository.Current.transactions.Count == transactionCount + 2 &&
                game.Repository.Current.glassMarbles == funds + buy.marbleDelta + sell.marbleDelta &&
                game.Repository.GetInstance(boughtId).ownerId != GameCatalog.PlayerOwnerId;
            string persistedTrade = JsonUtility.ToJson(game.Repository.Current);
            if (persistedTrade != JsonUtility.ToJson(game.Repository.Reload())) report.tradeFlowVerified = false;
            if (!report.tradeFlowVerified) report.errors.Add("Buy/sell balances, owners, receipts or disk persistence did not match.");
            yield return VerifyShopSwap();
            yield return Capture("v04-trade-complete", false); game.GoHub();

            game.ShowErrands();
            var existingErrand = game.Repository.Current.activeErrand;
            if (existingErrand == null) game.AcceptErrand(CampaignCatalog.Errands[0].id);
            var errand = game.Repository.Current.activeErrand;
            if (errand == null) { report.errors.Add("Errand could not be accepted."); yield break; }
            int errandFunds = game.Repository.Current.glassMarbles;
            game.SelectLocation(errand.destinationLocationId); game.ShowErrands(); game.CompleteErrand(errand.completionId); game.UI.CloseAlert();
            report.errandFlowVerified = game.Repository.Current.activeErrand == null && game.Repository.Current.glassMarbles == errandFunds + 6 &&
                game.Repository.Current.activities.Any(item => item.transactionId == errand.completionId);
            if (!report.errandFlowVerified) report.errors.Add("Errand delivery did not save one six-marble reward and clear the active task.");
            yield return Capture("v04-errand-result", false); game.GoHub();

            game.ShowTournament();
            if (game.Repository.Current.tournament == null || !game.Repository.Current.tournament.active) game.EnterTournament();
            var tournament = game.Repository.Current.tournament;
            if (tournament == null || !tournament.active) { report.errors.Add("Eight-player tournament could not be entered."); yield break; }
            yield return Capture("v04-tournament-bracket");
            game.OpenTournamentRound();
            var realCard = game.Repository.PlayerCards.FirstOrDefault(card => game.Repository.CanStake(card.instanceId));
            if (realCard == null) { report.errors.Add("Tournament has no real stake card after trade recovery."); yield break; }
            game.SelectCard(realCard.instanceId); game.StartPogs();
            yield return WaitForState(() => game.Screen == AppScreen.Pogs, 6, "Tournament real match");
            var tournamentMatch = game.Repository.GetPendingMatch(); game.Forfeit();
            report.tournamentFlowVerified = tournament.bracket.Count == 7 && tournamentMatch?.route == MatchRoute.Tournament &&
                tournamentMatch.tournamentRunId == tournament.runId && game.Screen == AppScreen.Result &&
                game.LastSettlement?.route == MatchRoute.Tournament && !game.Repository.Current.tournament.active && !game.Repository.HasPendingMatch;
            if (!report.tournamentFlowVerified) report.errors.Add("Tournament bracket, real contract or explicit elimination settlement failed.");
            yield return Capture("v04-tournament-result", false); game.GoHub();
        }

        bool ClickNamedControl(string name)
        {
            var button = game.GetComponentsInChildren<UIDocument>(true).Select(document => document.rootVisualElement.Q<Button>(name)).FirstOrDefault(item => item != null && item.enabledInHierarchy);
            if (button == null) return false;
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
            return true;
        }

        IEnumerator VerifyShopSwap()
        {
            TradeQuote quote = null;
            foreach (var offer in CampaignCatalog.SwapOffers)
            {
                var outgoing = game.Repository.PlayerCards.FirstOrDefault(card => card.definitionId == offer.wantedDefinitionId && !card.isFoundation && !card.treasured && !game.Repository.IsCardLocked(card.instanceId));
                if (outgoing == null) continue;
                try
                {
                    var candidate = game.Repository.GetTradeQuote(TradeKind.Swap, offer.id, outgoing.instanceId);
                    if (!candidate.requiresTreasuredConfirmation) { quote = candidate; break; }
                }
                catch (InvalidOperationException) { }
            }
            if (quote == null) { report.errors.Add("Swap UI QA has no currently available ordinary one-for-one contract."); yield break; }
            int transactionCount = game.Repository.Current.transactions.Count, funds = game.Repository.Current.glassMarbles;
            if (!ClickNamedControl("shop-tab-2")) { report.errors.Add("Shop has no enabled partner-swap tab."); yield break; }
            yield return null;
            if (!ClickNamedControl("swap-" + quote.offerId)) { report.errors.Add("Shop has no enabled swap button for " + quote.offerId); yield break; }
            yield return null; game.UI.CloseAlert();
            var receipt = game.Repository.Current.transactions.Skip(transactionCount).SingleOrDefault(item => item.kind == TradeKind.Swap && item.offerId == quote.offerId);
            report.swapFlowVerified = receipt != null && game.Repository.Current.transactions.Count == transactionCount + 1 &&
                receipt.lostCardIds.SequenceEqual(new[] { quote.playerCardId }) && receipt.gainedCardIds.SequenceEqual(new[] { quote.npcCardId }) &&
                game.Repository.Current.glassMarbles == funds && game.Repository.GetInstance(quote.playerCardId).ownerId == quote.npcId &&
                game.Repository.GetInstance(quote.npcCardId).ownerId == GameCatalog.PlayerOwnerId;
            string persisted = JsonUtility.ToJson(game.Repository.Current);
            if (persisted != JsonUtility.ToJson(game.Repository.Reload())) report.swapFlowVerified = false;
            if (ClickNamedControl("swap-" + quote.offerId)) report.swapFlowVerified = false;
            if (!report.swapFlowVerified) report.errors.Add("Partner swap UI changed balance, duplicated instances, failed persistence or left the daily-used contract enabled.");
            report.observations.Add("Partner swap UI contract=" + quote.offerId + " outgoing=" + quote.playerCardId + " incoming=" + quote.npcCardId + " verified=" + report.swapFlowVerified);
        }

        IEnumerator VerifyDisplayModes()
        {
            var display = game.GetComponent<DisplaySettings>();
            if (display == null || !display.IsolatedPreferences)
            { report.errors.Add("Display QA requires an isolated DisplaySettings component."); yield break; }
            string[] userKeys = { "SummerAfterSchool.Display.Mode", "SummerAfterSchool.Display.WindowWidth", "SummerAfterSchool.Display.WindowHeight" };
            int[] original = userKeys.Select(key => PlayerPrefs.GetInt(key, int.MinValue)).ToArray();
            game.UI.ShowAudioSettings();
            foreach (FullScreenMode mode in new[] { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed })
            {
                int errorsBefore = report.errors.Count;
                display.Preview(mode);
                yield return WaitForState(() => UnityEngine.Screen.fullScreenMode == mode, 4, "Display mode " + mode);
                if (!display.AwaitingConfirmation || display.ConfirmationSecondsRemaining <= 0 || display.ConfirmationSecondsRemaining > 15)
                    report.errors.Add("Display " + mode + " did not start the real-time confirmation window.");
                yield return Capture("display-" + mode, false);
                display.Confirm();
                if (display.AwaitingConfirmation || display.CurrentMode != mode || PlayerPrefs.GetInt(display.PreferencePrefix + "Mode", -1) != (int)mode)
                    report.errors.Add("Display " + mode + " confirmation did not persist isolated settings.");
                yield return VerifyDisplayLifecycle(display, mode);
                if (report.errors.Count == errorsBefore) report.displayModesVerified++;
            }
            display.Preview(FullScreenMode.FullScreenWindow);
            float before = Time.realtimeSinceStartup;
            float previousScale = Time.timeScale; Time.timeScale = 0;
            try { yield return new WaitForSecondsRealtime(DisplaySettings.ConfirmationDuration + .4f); }
            finally { Time.timeScale = previousScale; }
            yield return WaitForState(() => UnityEngine.Screen.fullScreenMode == FullScreenMode.Windowed, 4, "Display timeout restore");
            report.displayTimeoutRestored = !display.AwaitingConfirmation && display.CurrentMode == FullScreenMode.Windowed && Time.realtimeSinceStartup - before >= 15;
            if (!report.displayTimeoutRestored) report.errors.Add("Display confirmation timeout failed while game time was paused.");
            report.userDisplayPreferencesUnchanged = userKeys.Select((key, index) => PlayerPrefs.GetInt(key, int.MinValue) == original[index]).All(value => value);
            if (!report.userDisplayPreferencesUnchanged) report.errors.Add("Display QA modified normal user preferences.");
            game.UI.HandleEscape();
        }

        IEnumerator VerifyDisplayLifecycle(DisplaySettings original, FullScreenMode expectedMode)
        {
            int errorsBefore = report.errors.Count;
            int expectedWidth = original.WindowWidth, expectedHeight = original.WindowHeight;
            string expectedPrefix = original.PreferencePrefix;
            original.enabled = false;
            var host = new GameObject("QA · 显示组件保存重建");
            try
            {
                var discarded = host.AddComponent<DisplaySettings>();
                yield return WaitForState(() => discarded.StartupApplied && UnityEngine.Screen.fullScreenMode == expectedMode, 4, "Display first saved initialization " + expectedMode);
                Destroy(discarded); yield return null;
                if (discarded != null) report.errors.Add("Display component was not destroyed before saved-mode recreation.");
                var restored = host.AddComponent<DisplaySettings>();
                yield return WaitForState(() => restored.StartupApplied && UnityEngine.Screen.fullScreenMode == expectedMode &&
                    (expectedMode != FullScreenMode.Windowed || UnityEngine.Screen.width == expectedWidth && UnityEngine.Screen.height == expectedHeight),
                    4, "Display recreated saved mode/size " + expectedMode);
                if (restored.CurrentMode != expectedMode || restored.WindowWidth != expectedWidth || restored.WindowHeight != expectedHeight ||
                    !restored.IsolatedPreferences || restored.PreferencePrefix != expectedPrefix || restored.AwaitingConfirmation)
                    report.errors.Add("Display recreated component did not read the saved mode and window dimensions " + expectedMode);
                report.observations.Add("Display component destroyed/recreated saved=" + expectedMode + " window=" + expectedWidth + "x" + expectedHeight +
                    " actual=" + UnityEngine.Screen.fullScreenMode + " " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height);
                if (report.errors.Count == errorsBefore) report.displayLifecycleRestores++;
            }
            finally { Destroy(host); original.enabled = true; }
            yield return null;
        }

        IEnumerator VerifyEnvironments()
        {
            game.GoHub();
            var locationShapes = new HashSet<string>();
            bool resourcesReleased = true;
            foreach (var location in GameCatalog.Locations)
            {
                game.SelectLocation(location.id); yield return null;
                foreach (DayPeriod period in Enum.GetValues(typeof(DayPeriod)))
                foreach (WeatherKind weather in Enum.GetValues(typeof(WeatherKind)))
                {
                    int errorsBefore = report.errors.Count;
                    var context = new EnvironmentContext { locationId = location.id, visitVariant = 1, day = 3, period = period,
                        weather = weather, weatherSeed = 197, geometryVersion = 2,
                        frictionMultiplier = weather == WeatherKind.Rainy && location.id != "shop" && location.id != "corridor" ? .9f : 1f };
                    game.World.BuildWorld(context); game.World.SetCameraMode(WorldCameraMode.Match, true);
                    yield return null; yield return new WaitForEndOfFrame();
                    if (JsonUtility.ToJson(context) != JsonUtility.ToJson(game.World.Environment)) report.errors.Add("Environment did not preserve frozen context " + location.id + "/" + period + "/" + weather);
                    if (game.World.Terrain == null || !game.World.Terrain.IsTerrainEnabled || RenderSettings.sun == null || RenderSettings.skybox == null)
                        report.errors.Add("Environment lacks physical terrain, sunlight or sky " + location.id + "/" + period + "/" + weather);
                    bool wet = weather == WeatherKind.Rainy && location.id != "shop" && location.id != "corridor";
                    if (game.World.SurfaceWet != wet || game.World.RainActive != (weather == WeatherKind.Rainy))
                        report.errors.Add("Rain/shelter state differs from its preset " + location.id + "/" + period + "/" + weather);
                    yield return Capture("env-" + location.id + "-" + period + "-" + weather, false);
                    if (report.errors.Count == errorsBefore) report.environmentCombinationsVerified++;
                }
                var variants = new HashSet<string>();
                for (int variant = 0; variant < 3; variant++)
                {
                    var context = new EnvironmentContext { locationId = location.id, visitVariant = variant, day = 3,
                        period = DayPeriod.Afternoon, weather = WeatherKind.Sunny, weatherSeed = 197, geometryVersion = 2 };
                    var previousRoot = game.World.ActiveScenery;
                    game.World.BuildWorld(context); yield return null; yield return new WaitForEndOfFrame();
                    if (previousRoot != null) { report.errors.Add("Previous scenery was not destroyed after rebuild " + location.id); resourcesReleased = false; }
                    string signature = SceneryGeometrySignature(game.World.ActiveScenery);
                    variants.Add(signature);
                    int resources = game.World.OwnedResourceCount;
                    var fontAssets = new HashSet<Material>(game.World.ActiveScenery.GetComponentsInChildren<TextMesh>(true).Where(text => text.font != null).Select(text => text.font.material));
                    var ownedMaterials = game.World.ActiveScenery.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null && !fontAssets.Contains(material)).Distinct().ToArray();
                    game.World.BuildWorld(context); yield return null; yield return new WaitForEndOfFrame();
                    if (SceneryGeometrySignature(game.World.ActiveScenery) != signature || game.World.OwnedResourceCount != resources)
                        report.errors.Add("Same frozen scene variant rebuilt differently " + location.id + "/" + variant);
                    // 场景创建的所有材质必须销毁；字体资产材质和 Unity 内建 mesh 可以继续复用。
                    if (ownedMaterials.Any(material => material != null))
                    { report.errors.Add("Transient scene materials survived rebuild " + location.id); resourcesReleased = false; }
                    if (variant == 0) locationShapes.Add(signature);
                    report.sceneVariantsVerified++;
                    yield return Capture("variant-" + location.id + "-" + variant, false);
                }
                if (variants.Count != 3) report.errors.Add("Revisit variants did not change actual scenery geometry " + location.id);
            }
            if (GameCatalog.Locations.Length != 8 || report.environmentCombinationsVerified != 96)
                report.errors.Add("Environment matrix incomplete: " + GameCatalog.Locations.Length + " locations and " + report.environmentCombinationsVerified + "/96 combinations.");
            if (report.sceneVariantsVerified != 24 || locationShapes.Count != 8) report.errors.Add("Eight scenes or three revisit variants lack distinct geometry.");
            report.sceneResourcesReleased = resourcesReleased;
            game.SelectLocation("home"); game.GoHub(); yield return null;
        }

        static string SceneryGeometrySignature(Transform scene)
        {
            if (scene == null) return "";
            return string.Join("|", scene.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh != null)
                .Select(filter => filter.sharedMesh.bounds.size.ToString("F3") + ":" + scene.InverseTransformPoint(filter.transform.position).ToString("F3") + ":" + filter.transform.localScale.ToString("F3"))
                .OrderBy(value => value));
        }

        // 独立组件仍使用同一 PhysX 地面，但不订阅协调器 Completed/Changed，挑战验收不会写战绩或转移圆片。
        IEnumerator VerifyGameplayLayouts()
        {
            game.GoHub(); game.SelectLocation("school_gate"); yield return null;
            string saveBefore = JsonUtility.ToJson(game.Repository.Current);
            var geometry = GameplayLayouts.ValidateGeometry();
            report.layoutsGeometryValid = geometry.Count == 0;
            foreach (string error in geometry) report.errors.Add("Layout geometry: " + error);
            report.observations.Add("Layout geometry checks: " + (geometry.Count == 0 ? "passed" : "failed"));
            var pogHost = new GameObject("QA · 独立打片挑战");
            var pog = pogHost.AddComponent<PogMatch>(); pog.Configure(game.World, game.Audio);
            pog.PhysicsDiagnostic += value => report.observations.Add("Layout QA " + value);
            string classic = null;
            foreach (PracticeLayout layout in Enum.GetValues(typeof(PracticeLayout)))
            {
                int errorsBefore = report.errors.Count;
                game.Audio.SetContext(game.LocationId, AudioMood.Match); ObserveAudio(AudioMood.Match, "Isolated " + layout + " pog");
                pog.Begin(GameCatalog.GetCard("pog_00"), GameCatalog.GetNpc("xiaoyu"), RuleKind.Flip, true, 97, layout);
                yield return WaitForState(() => pog.Phase == MatchPhase.PlayerAim, 6, "Pog " + layout + " initial aim");
                if (pog.Phase != MatchPhase.PlayerAim) { pog.StopMatch(); yield return null; continue; }
                var target = pog.Targets.Where(t => !t.Claimed).OrderBy(t => t.transform.position.z).FirstOrDefault();
                if (target == null) { report.errors.Add("Pog " + layout + " has no target"); pog.StopMatch(); yield return null; continue; }
                Vector3 direction = target.transform.position - new Vector3(0, 0, -2.25f); direction.y = 0;
                pog.Launch(direction.normalized, Mathf.Clamp01((direction.magnitude - 1.25f) / 3.3f), 0, ShotKind.Slap);
                yield return new WaitForSecondsRealtime(.12f);
                yield return VerifyFlightCancel(pog, pog.CancelAim, () => pog.Phase, () => pog.IsDragging, () => pog.Power, "Pog " + layout);
                yield return WaitForState(() => pog.Phase != MatchPhase.Flying, 18, "Pog " + layout + " settle");
                if (pog.Phase == MatchPhase.Flying) { pog.StopMatch(); yield return null; continue; }
                if (pog.ShotsUsed != 1) report.errors.Add("Pog " + layout + " did not consume exactly one real shot");
                string json = pog.CaptureBoard();
                var before = JsonUtility.FromJson<PogMatch.BoardState>(json);
                var capturedPoseDiagnostics = pog.Targets.Select(disc => DescribePogBodyPose("Pog capture", disc)).ToArray();
                if (layout == PracticeLayout.Classic) classic = json;
                pog.StopMatch(); yield return null;
                pog.Begin(GameCatalog.GetCard("pog_00"), GameCatalog.GetNpc("xiaoyu"), RuleKind.Flip, true);
                bool restored = pog.RestoreBoard(json);
                var after = JsonUtility.FromJson<PogMatch.BoardState>(pog.CaptureBoard());
                bool same = restored && pog.Layout == layout && pog.Practice && SamePogBoard(before, after);
                report.observations.Add("Pog restore API=" + restored + " " + DescribePogDifference(before, after));
                if (!same)
                {
                    File.WriteAllText(Path.Combine(output, "pog-" + layout + "-before.json"), JsonUtility.ToJson(before, true));
                    File.WriteAllText(Path.Combine(output, "pog-" + layout + "-after.json"), JsonUtility.ToJson(after, true));
                    report.observations.AddRange(capturedPoseDiagnostics);
                    report.observations.AddRange(pog.Targets.Select(disc => DescribePogBodyPose("Pog restore", disc)));
                }
                if (!same) report.errors.Add("Pog " + layout + " stable snapshot changed mode, score, turn, feedback or poses");
                else report.layoutSnapshotRestores++;
                report.observations.Add("Pog layout=" + layout + " shot=1 score=" + before.playerScore + ":" + before.npcScore + " restored=" + same);
                if (report.errors.Count == errorsBefore) report.pogLayoutsVerified++;
                pog.StopMatch(); yield return null;
            }
            if (classic == null) report.errors.Add("No stable Classic snapshot was available for legacy validation");
            else
            {
                var legacyEnvironment = JsonUtility.FromJson<EnvironmentContext>(JsonUtility.ToJson(game.World.Environment));
                legacyEnvironment.geometryVersion = 1;
                game.World.BuildWorld(legacyEnvironment); yield return null;
                bool compatible = true;
                for (int version = 0; version <= 2; version++)
                {
                    var legacy = JsonUtility.FromJson<PogMatch.BoardState>(classic);
                    legacy.version = version;
                    legacy.baseHeight = 0; legacy.geometryVersion = 0; legacy.environment = null;
                    // 构造真正的旧厚片盘面；旧版未保存厚度，不能拿新版坐标冒充兼容验收。
                    for (int i = 0; i < legacy.discs.Length; i++)
                        if (!legacy.discs[i].claimed)
                            legacy.discs[i].position.y += (DiscBody.LegacyBaseHeight - DiscBody.BaseHeight) * .5f *
                                GameCatalog.Cards[GameCatalog.GetNpc("xiaoyu").startingCardIndexes[i % GameCatalog.GetNpc("xiaoyu").startingCardIndexes.Length]].Physics.thicknessMeters / .0027f *
                                Mathf.Abs((legacy.discs[i].rotation * Vector3.up).y);
                    // 旧 JSON 没有这些字段；版本分支应忽略它们，不能继承 Begin 的新布局。
                    if (version < 2) { legacy.layout = PracticeLayout.EdgeRace; legacy.lastFeedback = ""; legacy.playerMissStreak = legacy.playerScoringStreak = 0; }
                    pog.Begin(GameCatalog.GetCard("pog_00"), GameCatalog.GetNpc("xiaoyu"), RuleKind.Flip, true, 97, PracticeLayout.Fan);
                    var expected = JsonUtility.FromJson<PogMatch.BoardState>(JsonUtility.ToJson(legacy));
                    var migrationDiscs = pog.Targets.ToArray();
                    for (int i = 0; i < expected.discs.Length; i++)
                        if (!expected.discs[i].claimed)
                            expected.discs[i].position.y = pog.RestHeight(migrationDiscs[i], expected.discs[i].position, expected.discs[i].rotation, migrationDiscs[i].RestClearance);
                    bool restored = pog.RestoreBoard(JsonUtility.ToJson(legacy));
                    var actual = JsonUtility.FromJson<PogMatch.BoardState>(pog.CaptureBoard());
                    legacy.layout = PracticeLayout.Classic;
                    expected.layout = PracticeLayout.Classic;
                    if (!restored || pog.Layout != PracticeLayout.Classic || !SamePogBoard(expected, actual))
                    {
                        compatible = false; report.errors.Add("Pog board version " + version + " compatibility failed");
                        File.WriteAllText(Path.Combine(output, "pog-legacy-" + version + "-before.json"), JsonUtility.ToJson(legacy, true));
                        File.WriteAllText(Path.Combine(output, "pog-legacy-" + version + "-after.json"), JsonUtility.ToJson(actual, true));
                        report.observations.Add("Pog legacy migration expected version=" + version + " " + DescribePogDifference(expected, actual));
                        report.observations.AddRange(pog.Targets.Select(disc => DescribePogBodyPose("Pog legacy restore " + version, disc)));
                    }
                    report.observations.Add("Pog legacy version=" + version + " restored=" + restored + " layout=" + pog.Layout);
                    pog.StopMatch(); yield return null;
                }
                report.legacySnapshotsRestored = compatible;
                pog.Begin(GameCatalog.GetCard("pog_00"), GameCatalog.GetNpc("xiaoyu"), RuleKind.Flip, true);
                string untouched = pog.CaptureBoard();
                var future = JsonUtility.FromJson<PogMatch.BoardState>(classic); future.version = 4;
                report.futureSnapshotRejected = !pog.RestoreBoard(JsonUtility.ToJson(future)) && untouched == pog.CaptureBoard();
                if (!report.futureSnapshotRejected) report.errors.Add("Future Pog board version was accepted or mutated the board");
                pog.StopMatch(); yield return null;
            }
            pog.StopMatch(); Destroy(pogHost); yield return null;
            game.GoHub();
            report.layoutTestsPreservedSave = saveBefore == JsonUtility.ToJson(game.Repository.Current);
            if (!report.layoutTestsPreservedSave) report.errors.Add("Isolated layout tests modified collection, pending match or progress");
            if (report.pogLayoutsVerified != 4 || report.layoutSnapshotRestores != 4 || report.flightCancelChecks != 4)
                report.errors.Add("Layout coverage incomplete: pog=" + report.pogLayoutsVerified + " snapshots=" + report.layoutSnapshotRestores + " cancel=" + report.flightCancelChecks);
        }

        IEnumerator WaitForState(Func<bool> ready, float timeout, string label)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!ready()) report.errors.Add(label + " timed out after " + timeout + " seconds");
        }

        sealed class BodyState
        {
            public Rigidbody body;
            public Vector3 position, velocity, angularVelocity;
            public Quaternion rotation;
            public bool kinematic;
            public BodyState(Rigidbody value) { body = value; position = value.position; rotation = value.rotation; velocity = value.linearVelocity; angularVelocity = value.angularVelocity; kinematic = value.isKinematic; }
            public bool Unchanged() { return body != null && SamePose(position, rotation, body.position, body.rotation) && Vector3.Distance(velocity, body.linearVelocity) < .001f && Vector3.Distance(angularVelocity, body.angularVelocity) < .001f && kinematic == body.isKinematic; }
        }

        IEnumerator VerifyFlightCancel(Component match, Action cancel, Func<MatchPhase> phase, Func<bool> dragging, Func<float> power, string label)
        {
            int errorsBefore = report.errors.Count;
            if (phase() != MatchPhase.Flying) { report.errors.Add(label + " was not flying during cancel validation"); yield break; }
            var bodies = match.GetComponentsInChildren<Rigidbody>().Select(b => new BodyState(b)).ToArray();
            if (bodies.Length == 0 || !bodies.Any(b => !b.kinematic && b.velocity.sqrMagnitude > .01f)) report.errors.Add(label + " had no moving dynamic body");
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                cancel(); match.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
                if (phase() != MatchPhase.Flying || dragging() || power() != 0 || bodies.Any(b => !b.Unchanged()))
                    report.errors.Add(label + " CancelAim/focus changed flying physics or failed to clear aim");
                yield return new WaitForSecondsRealtime(.08f);
                if (Time.timeScale != 0 || phase() != MatchPhase.Flying || bodies.Any(b => !b.Unchanged()))
                    report.errors.Add(label + " paused flight moved or resumed itself");
            }
            finally { Time.timeScale = previousScale; }
            if (report.errors.Count == errorsBefore) report.flightCancelChecks++;
            report.observations.Add(label + " flying CancelAim/focus/pause unchanged=" + (report.errors.Count == errorsBefore));
        }

        static bool SamePose(Vector3 leftPosition, Quaternion leftRotation, Vector3 rightPosition, Quaternion rightRotation)
        {
            return Vector3.Distance(leftPosition, rightPosition) < .003f && Quaternion.Angle(leftRotation.normalized, rightRotation.normalized) < .08f;
        }
        static bool SamePogBoard(PogMatch.BoardState a, PogMatch.BoardState b)
        {
            if (a == null || b == null || a.discs == null || b.discs == null || a.discs.Length != 5 || b.discs.Length != 5 || a.nextTurnBase != b.nextTurnBase ||
                a.playerScore != b.playerScore || a.npcScore != b.npcScore || a.shotsUsed != b.shotsUsed || a.randomDrawCount != b.randomDrawCount ||
                a.layout != b.layout || a.practice != b.practice || a.shot != b.shot || (a.lastFeedback ?? "") != (b.lastFeedback ?? "") ||
                a.playerMissStreak != b.playerMissStreak || a.playerScoringStreak != b.playerScoringStreak) return false;
            if (a.version >= 3 && b.version >= 3 && (a.geometryVersion != b.geometryVersion || Mathf.Abs(a.baseHeight - b.baseHeight) > .00001f ||
                JsonUtility.ToJson(a.environment) != JsonUtility.ToJson(b.environment))) return false;
            for (int i = 0; i < a.discs.Length; i++) if (a.discs[i] == null || b.discs[i] == null || a.discs[i].claimed != b.discs[i].claimed || !SamePose(a.discs[i].position, a.discs[i].rotation, b.discs[i].position, b.discs[i].rotation)) return false;
            return true;
        }
        static string DescribePogBodyPose(string label, DiscBody disc)
        {
            var body = disc.Body;
            return label + " " + disc.name + " claimed=" + disc.Claimed + " active=" + disc.gameObject.activeSelf +
                " kinematic=" + body.isKinematic + " body=" + body.position.ToString("F6") + " transform=" + disc.transform.position.ToString("F6") +
                " bodyRotation=" + body.rotation.ToString("F6") + " transformRotation=" + disc.transform.rotation.ToString("F6") +
                " bodyTransformDistance=" + Vector3.Distance(body.position, disc.transform.position).ToString("G9") +
                " bodyTransformAngle=" + Quaternion.Angle(body.rotation.normalized, disc.transform.rotation.normalized).ToString("G9");
        }

        static string DescribePogDifference(PogMatch.BoardState a, PogMatch.BoardState b)
        {
            if (a == null || b == null) return "board null before=" + (a == null) + " after=" + (b == null);
            var values = new List<string> { "turn=" + a.nextTurnBase + "/" + b.nextTurnBase, "score=" + a.playerScore + ":" + a.npcScore + "/" + b.playerScore + ":" + b.npcScore,
                "shots=" + a.shotsUsed + "/" + b.shotsUsed, "randomDraws=" + a.randomDrawCount + "/" + b.randomDrawCount,
                "layout=" + a.layout + "/" + b.layout, "practice=" + a.practice + "/" + b.practice, "shot=" + a.shot + "/" + b.shot,
                "missStreak=" + a.playerMissStreak + "/" + b.playerMissStreak, "scoringStreak=" + a.playerScoringStreak + "/" + b.playerScoringStreak,
                "feedbackSame=" + ((a.lastFeedback ?? "") == (b.lastFeedback ?? "")) };
            if (a.discs == null || b.discs == null || a.discs.Length != b.discs.Length) { values.Add("disc count differs/null"); return string.Join("; ", values); }
            for (int i = 0; i < a.discs.Length; i++)
            {
                var left = a.discs[i]; var right = b.discs[i];
                if (left == null || right == null) { values.Add("disc" + i + " null"); continue; }
                float componentDelta = Mathf.Max(Mathf.Abs(left.rotation.x - right.rotation.x), Mathf.Abs(left.rotation.y - right.rotation.y),
                    Mathf.Abs(left.rotation.z - right.rotation.z), Mathf.Abs(left.rotation.w - right.rotation.w));
                values.Add("disc" + i + " claimed=" + left.claimed + "/" + right.claimed +
                    " position=" + left.position.ToString("F6") + "/" + right.position.ToString("F6") +
                    " rotation=" + left.rotation.ToString("F6") + "/" + right.rotation.ToString("F6") +
                    " posDiff=" + Vector3.Distance(left.position, right.position).ToString("G9") +
                    " rawAngle=" + Quaternion.Angle(left.rotation, right.rotation).ToString("G9") +
                    " normalizedAngle=" + Quaternion.Angle(left.rotation.normalized, right.rotation.normalized).ToString("G9") +
                    " qNorm=" + Quaternion.Dot(left.rotation, left.rotation).ToString("G9") + "/" + Quaternion.Dot(right.rotation, right.rotation).ToString("G9") +
                    " qComponentDiff=" + componentDelta.ToString("G9"));
            }
            return string.Join("; ", values);
        }

        void CompleteReport(bool completed = true)
        {
            if (finished) return; finished = true;
            report.completed = completed;
            if (shotFeedbackMode)
            {
                feedbackReport.completed = completed; feedbackReport.passed = completed && report.errors.Count == 0;
                feedbackReport.elapsedSeconds = Time.realtimeSinceStartup - feedbackStartedAt;
                feedbackReport.errors = report.errors; feedbackReport.observations = report.observations;
                File.WriteAllText(Path.Combine(output, "shot-feedback-report.json"), JsonUtility.ToJson(feedbackReport, true));
                Debug.Log("SUMMER_SHOT_FEEDBACK_COMPLETE completed=" + completed + " passed=" + feedbackReport.passed +
                    " ui=" + (feedbackReport.mobileUI ? "mobile" : "desktop") + " errors=" + report.errors.Count +
                    " settled=" + feedbackReport.shotSettledEvents);
            }
            else File.WriteAllText(Path.Combine(output, "runtime-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("SUMMER_SMOKE_COMPLETE completed=" + completed + " errors=" + report.errors.Count + " pog=" + report.pogScore + " environmentCases=" + report.environmentCombinationsVerified);
            DisplaySettings.Quit(report.errors.Count == 0 ? 0 : 1);
        }
        void Update()
        {
            if (!finished && Time.realtimeSinceStartup > watchdog)
            { report.errors.Add("Runtime probe exceeded the real-time watchdog."); CompleteReport(false); }
        }

        IEnumerator Capture(string name, bool verifyInput = true)
        {
            yield return new WaitForEndOfFrame();
            // 与实际窗口同尺寸，避免改变 targetTexture 时 ScrollView 的裁剪仍保留旧像素尺度。
            int width = UnityEngine.Screen.width, height = UnityEngine.Screen.height;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create();
            var previousCameraTarget = game.World.MainCamera.targetTexture;
            var previousActive = RenderTexture.active;
            game.World.MainCamera.targetTexture = target; game.World.MainCamera.Render();
            string uiError = game.UI.RenderVerificationFrame(target);
            if (uiError != null) report.errors.Add("UI verification rendering: " + uiError);
            string inputError = verifyInput ? game.UI.VerifyInputBlocking() : null;
            if (inputError != null) report.errors.Add("UI input: " + inputError);
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            game.World.MainCamera.targetTexture = previousCameraTarget; RenderTexture.active = previousActive;
            target.Release(); Destroy(target);
            var samples = texture.GetPixels32();
            bool varied = false;
            for (int i = 200; i < samples.Length; i += width) if (samples[i].r > 8 || samples[i].g > 8 || samples[i].b > 8) { varied = true; break; }
            if (!varied) report.errors.Add("Captured frame is black: " + name);
            report.observations.Add(name + " camera=" + game.World.MainCamera.enabled + " position=" + game.World.MainCamera.transform.position);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
