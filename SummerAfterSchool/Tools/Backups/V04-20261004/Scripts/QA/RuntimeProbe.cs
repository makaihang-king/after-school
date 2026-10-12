using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Childhood
{
    /// <summary>仅 --smoke-test 时运行，使用独立测试存档，真正调用 PhysX 与游戏流程。</summary>
    public sealed class RuntimeProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool completed;
            public string engine;
            public int pogScore, marbleScore, ringOutScore;
            public bool restoredScore;
            public bool realMatchSettled;
            public bool marbleScorePersisted;
            public bool polishControlsValid, layoutsGeometryValid, legacySnapshotsRestored, futureSnapshotRejected, layoutTestsPreservedSave;
            public int pogLayoutsVerified, marbleLayoutsVerified, layoutSnapshotRestores, flightCancelChecks;
            public List<string> errors = new List<string>();
            public List<string> observations = new List<string>();
        }
        Report report = new Report();
        string output;
        SummerGame game;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootProbe()
        {
            if (Environment.GetCommandLineArgs().Contains("--smoke-test")) new GameObject("运行验证").AddComponent<RuntimeProbe>();
        }

        void Awake()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA"));
            if (Environment.GetCommandLineArgs().Contains("--marble-smoke")) output = Path.Combine(output, "Marbles");
            else if (Environment.GetCommandLineArgs().Contains("--polish-smoke")) output = Path.Combine(output, "Polish");
            else if (Environment.GetCommandLineArgs().Contains("--visual-smoke")) output = Path.Combine(output, "Visual");
            Directory.CreateDirectory(output); report.engine = Application.unityVersion;
            Application.logMessageReceived += OnLog;
        }
        void OnLog(string value, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(value + "\n" + stack);
        }

        IEnumerator Start()
        {
            yield return new WaitUntil(() => FindFirstObjectByType<SummerGame>() != null);
            game = FindFirstObjectByType<SummerGame>();
            game.Pogs.PhysicsDiagnostic += value => report.observations.Add(value);
            yield return new WaitForSecondsRealtime(2);
            yield return Capture("01-hub");
            ObserveAudio(AudioMood.Explore, "Hub");
            if (Resources.Load<AudioClip>("Audio/summer_collection") == null) report.errors.Add("Collection music resource summer_collection is missing");
            string controlsError = game.UI.VerifyPolishControls();
            report.polishControlsValid = controlsError == null;
            if (controlsError != null) report.errors.Add("Polish controls: " + controlsError);
            game.UI.ShowAudioSettings(); yield return Capture("14-audio-settings", false); game.UI.HandleEscape();
            game.UI.ShowMarbleLobby(); yield return Capture("15-marble-choices", false); game.UI.HandleEscape();
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
                game.GoHub(); game.SelectLocation("school_gate"); game.StartMarbles();
                yield return new WaitForSecondsRealtime(.8f); yield return Capture("07-marbles");
                game.UI.ShowPause(); game.UI.ShowAudioSettings();
                if (!game.UI.HandleEscape() || Time.timeScale != 0) report.errors.Add("Audio settings did not return to paused match");
                if (!game.UI.HandleEscape() || Time.timeScale != 1) report.errors.Add("Escape did not resume paused match");
                game.GoHub(); CompleteReport(); yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--polish-smoke"))
            {
                game.ShowCollection(); ObserveAudio(AudioMood.Collection, "Collection");
                yield return Capture("06-collection"); game.GoHub();
                yield return VerifyGameplayLayouts();
                // 独立挑战不进入协调器结算，直接验证 Result 音景 API，完整模式另验真实 Result 转换。
                game.Audio.SetContext(game.LocationId, AudioMood.Result); ObserveAudio(AudioMood.Result, "Isolated Result API");
                game.GoHub(); ObserveAudio(AudioMood.Explore, "Return Hub");
                CompleteReport(); yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--marble-smoke"))
            {
                if (Environment.GetCommandLineArgs().Contains("--pinwheel-smoke")) game.SelectedMarbleLayout = MarbleLayout.Pinwheel;
                yield return VerifyMarbles(); CompleteReport(); yield break;
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
            yield return VerifyMarbles();
            game.GoHub(); game.SelectLocation("alley"); yield return new WaitForSecondsRealtime(.8f); yield return Capture("09-alley");
            game.SelectLocation("old_tree"); yield return new WaitForSecondsRealtime(.8f); yield return Capture("10-tree");
            game.SelectLocation("home"); yield return new WaitForSecondsRealtime(.8f); yield return Capture("11-home");
            ObserveAudio(AudioMood.Explore, "Home Hub");
            yield return VerifyGameplayLayouts();
            CompleteReport();
        }

        IEnumerator VerifyMarbles()
        {
            int roundsBefore = game.Repository.Current.progress.marbleRounds;
            int bestBefore = game.Repository.Current.progress.marbleBestScore;
            game.GoHub(); game.StartMarbles(); yield return new WaitForSecondsRealtime(1); yield return Capture("07-marbles");
            ObserveAudio(AudioMood.Match, "Marble match");
            int attempts = 0;
            float roundDeadline = Time.realtimeSinceStartup + 160;
            while (game.Screen == AppScreen.Marbles && attempts < 6 && Time.realtimeSinceStartup < roundDeadline)
            {
                if (game.Marbles.Phase != MatchPhase.PlayerAim) { yield return null; continue; }
                attempts++;
                float angle = attempts == 1 ? 0 : attempts % 2 == 0 ? -17 : 17;
                game.Marbles.Launch(Quaternion.Euler(0, angle, 0) * Vector3.forward, .7f);
                float start = Time.realtimeSinceStartup;
                while (game.Marbles.Phase == MatchPhase.Flying && Time.realtimeSinceStartup - start < 20) yield return null;
                report.observations.Add("Marble shot " + attempts + ": score=" + game.Marbles.Score);
                if (game.Marbles.Phase == MatchPhase.Flying) { report.errors.Add("Marbles did not settle within 20 seconds"); break; }
                yield return new WaitForSecondsRealtime(.5f);
            }
            report.marbleScore = game.Marbles.Score;
            float finishDeadline = Time.realtimeSinceStartup + 4;
            while (game.Screen == AppScreen.Marbles && Time.realtimeSinceStartup < finishDeadline) yield return null;
            if (game.Screen != AppScreen.Result) report.errors.Add("Marble round did not reach result screen");
            else ObserveAudio(AudioMood.Result, "Marble result");
            var reload = new SaveRepository(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/save")));
            var progress = reload.LoadOrCreate().progress;
            report.marbleScorePersisted = progress.marbleRounds == roundsBefore + 1 && progress.marbleBestScore == Math.Max(bestBefore, report.marbleScore);
            if (!report.marbleScorePersisted) report.errors.Add("Marble round count or highest score did not survive disk reload");
            report.observations.Add("Marble result screen=" + game.Screen + "; disk reload rounds=" + progress.marbleRounds + ", best=" + progress.marbleBestScore);
            yield return Capture("08-marble-result");
        }

        void ObserveAudio(AudioMood expected, string label)
        {
            report.observations.Add(label + " audio location=" + game.Audio.LocationId + " mood=" + game.Audio.Mood);
            if (game.Audio.LocationId != game.LocationId || game.Audio.Mood != expected)
                report.errors.Add(label + " audio context did not follow location/mood");
        }

        // 独立组件仍使用同一 PhysX 地面，但不订阅协调器 Completed/Changed，挑战验收不会写战绩或转移圆片。
        IEnumerator VerifyGameplayLayouts()
        {
            game.GoHub(); game.SelectLocation("school_gate"); yield return null;
            string saveBefore = JsonUtility.ToJson(game.Repository.Current);
            var geometry = GameplayLayouts.ValidateGeometry();
            report.layoutsGeometryValid = geometry.Count == 0;
            foreach (string error in geometry) report.errors.Add("Layout geometry: " + error);
            report.observations.Add("Eight layout geometry checks: " + (geometry.Count == 0 ? "passed" : "failed"));
            var pogHost = new GameObject("QA · 独立打片挑战");
            var marbleHost = new GameObject("QA · 独立玻璃球挑战");
            var pog = pogHost.AddComponent<PogMatch>(); pog.Configure(game.World, game.Audio);
            var marble = marbleHost.AddComponent<MarbleMatch>(); marble.Configure(game.World, game.Audio);
            pog.PhysicsDiagnostic += value => report.observations.Add("Layout QA " + value);
            marble.PhysicsDiagnostic += value => report.observations.Add("Layout QA " + value);
            int completions = 0; marble.Completed += _ => completions++;
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
                bool compatible = true;
                for (int version = 0; version <= 2; version++)
                {
                    var legacy = JsonUtility.FromJson<PogMatch.BoardState>(classic);
                    legacy.version = version;
                    // 旧 JSON 没有这些字段；版本分支应忽略它们，不能继承 Begin 的新布局。
                    if (version < 2) { legacy.layout = PracticeLayout.EdgeRace; legacy.lastFeedback = ""; legacy.playerMissStreak = legacy.playerScoringStreak = 0; }
                    pog.Begin(GameCatalog.GetCard("pog_00"), GameCatalog.GetNpc("xiaoyu"), RuleKind.Flip, true, 97, PracticeLayout.Fan);
                    bool restored = pog.RestoreBoard(JsonUtility.ToJson(legacy));
                    var actual = JsonUtility.FromJson<PogMatch.BoardState>(pog.CaptureBoard());
                    legacy.layout = PracticeLayout.Classic;
                    if (!restored || pog.Layout != PracticeLayout.Classic || !SamePogBoard(legacy, actual))
                    {
                        compatible = false; report.errors.Add("Pog board version " + version + " compatibility failed");
                        File.WriteAllText(Path.Combine(output, "pog-legacy-" + version + "-before.json"), JsonUtility.ToJson(legacy, true));
                        File.WriteAllText(Path.Combine(output, "pog-legacy-" + version + "-after.json"), JsonUtility.ToJson(actual, true));
                        report.observations.Add("Pog legacy restore version=" + version + " " + DescribePogDifference(legacy, actual));
                        report.observations.AddRange(pog.Targets.Select(disc => DescribePogBodyPose("Pog legacy restore " + version, disc)));
                    }
                    report.observations.Add("Pog legacy version=" + version + " restored=" + restored + " layout=" + pog.Layout);
                    pog.StopMatch(); yield return null;
                }
                report.legacySnapshotsRestored = compatible;
                pog.Begin(GameCatalog.GetCard("pog_00"), GameCatalog.GetNpc("xiaoyu"), RuleKind.Flip, true);
                string untouched = pog.CaptureBoard();
                var future = JsonUtility.FromJson<PogMatch.BoardState>(classic); future.version = 3;
                report.futureSnapshotRejected = !pog.RestoreBoard(JsonUtility.ToJson(future)) && untouched == pog.CaptureBoard();
                if (!report.futureSnapshotRejected) report.errors.Add("Future Pog board version was accepted or mutated the board");
                pog.StopMatch(); yield return null;
            }
            foreach (MarbleLayout layout in Enum.GetValues(typeof(MarbleLayout)))
            {
                int errorsBefore = report.errors.Count;
                marble.Begin(97, layout);
                yield return WaitForState(() => marble.Phase == MatchPhase.PlayerAim, 6, "Marble " + layout + " initial aim");
                if (marble.Phase != MatchPhase.PlayerAim) { marble.StopMatch(); yield return null; continue; }
                var target = marble.TargetBodies.OrderBy(t => t.position.z).FirstOrDefault();
                if (target == null) { report.errors.Add("Marble " + layout + " has no target"); marble.StopMatch(); yield return null; continue; }
                Vector3 direction = target.position - MarbleMatch.LaunchPosition; direction.y = 0;
                marble.Launch(direction.normalized, .7f);
                if (marble.CaptureBoard() != null) report.errors.Add("Marble " + layout + " captured an unstable flying board");
                yield return new WaitForSecondsRealtime(.12f);
                yield return VerifyFlightCancel(marble, marble.CancelAim, () => marble.Phase, () => marble.IsDragging, () => marble.Power, "Marble " + layout);
                yield return WaitForState(() => marble.Phase != MatchPhase.Flying, 20, "Marble " + layout + " settle");
                if (marble.Phase == MatchPhase.Flying) { marble.StopMatch(); yield return null; continue; }
                if (marble.ShotsRemaining != 5) report.errors.Add("Marble " + layout + " did not consume exactly one real shot");
                string json = marble.CaptureBoard();
                if (string.IsNullOrEmpty(json)) { report.errors.Add("Marble " + layout + " stable snapshot is empty"); marble.StopMatch(); yield return null; continue; }
                var before = JsonUtility.FromJson<MarbleMatch.BoardState>(json);
                marble.StopMatch(); yield return null;
                marble.Begin(); int completionsBeforeRestore = completions;
                bool restored = marble.RestoreBoard(json);
                var after = JsonUtility.FromJson<MarbleMatch.BoardState>(marble.CaptureBoard());
                bool same = restored && marble.Layout == layout && SameMarbleBoard(before, after) && completions == completionsBeforeRestore;
                report.observations.Add("Marble restore API=" + restored + " completionDelta=" + (completions - completionsBeforeRestore) + " " + DescribeMarbleDifference(before, after));
                if (!same)
                {
                    File.WriteAllText(Path.Combine(output, "marble-" + layout + "-before.json"), JsonUtility.ToJson(before, true));
                    File.WriteAllText(Path.Combine(output, "marble-" + layout + "-after.json"), JsonUtility.ToJson(after, true));
                    foreach (var body in marble.GetComponentsInChildren<Rigidbody>(true).Where(b => b.name.StartsWith("彩玻璃球 ")))
                        report.observations.Add("Marble restore " + body.name + " active=" + body.gameObject.activeSelf + " kinematic=" + body.isKinematic +
                            " body=" + body.position.ToString("F6") + " transform=" + body.transform.position.ToString("F6") + " bodyTransformDistance=" + Vector3.Distance(body.position, body.transform.position).ToString("G9"));
                }
                if (!same) report.errors.Add("Marble " + layout + " stable snapshot changed mode, score, feedback, poses or fired Completed");
                else report.layoutSnapshotRestores++;
                // Marble 当前只有版本 1，也必须拒绝未来版本且不改变现有稳定盘面。
                string untouched = marble.CaptureBoard(); before.version = 2;
                if (marble.RestoreBoard(JsonUtility.ToJson(before)) || untouched != marble.CaptureBoard()) report.errors.Add("Future Marble board version was accepted or mutated the board");
                report.observations.Add("Marble layout=" + layout + " shot=1 score=" + marble.Score + " remaining=" + marble.ShotsRemaining + " restored=" + same);
                if (report.errors.Count == errorsBefore) report.marbleLayoutsVerified++;
                marble.StopMatch(); yield return null;
            }
            pog.StopMatch(); marble.StopMatch(); Destroy(pogHost); Destroy(marbleHost); yield return null;
            game.GoHub();
            report.layoutTestsPreservedSave = saveBefore == JsonUtility.ToJson(game.Repository.Current);
            if (!report.layoutTestsPreservedSave) report.errors.Add("Isolated layout tests modified collection, pending match or progress");
            if (report.pogLayoutsVerified != 4 || report.marbleLayoutsVerified != 4 || report.layoutSnapshotRestores != 8 || report.flightCancelChecks != 8)
                report.errors.Add("Layout coverage incomplete: pog=" + report.pogLayoutsVerified + " marble=" + report.marbleLayoutsVerified + " snapshots=" + report.layoutSnapshotRestores + " cancel=" + report.flightCancelChecks);
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
            return Vector3.Distance(leftPosition, rightPosition) < .003f && Quaternion.Angle(leftRotation, rightRotation) < .08f;
        }
        static bool SamePogBoard(PogMatch.BoardState a, PogMatch.BoardState b)
        {
            if (a == null || b == null || a.discs == null || b.discs == null || a.discs.Length != 5 || b.discs.Length != 5 || a.nextTurnBase != b.nextTurnBase ||
                a.playerScore != b.playerScore || a.npcScore != b.npcScore || a.shotsUsed != b.shotsUsed || a.randomDrawCount != b.randomDrawCount ||
                a.layout != b.layout || a.practice != b.practice || a.shot != b.shot || (a.lastFeedback ?? "") != (b.lastFeedback ?? "") ||
                a.playerMissStreak != b.playerMissStreak || a.playerScoringStreak != b.playerScoringStreak) return false;
            for (int i = 0; i < a.discs.Length; i++) if (a.discs[i] == null || b.discs[i] == null || a.discs[i].claimed != b.discs[i].claimed || !SamePose(a.discs[i].position, a.discs[i].rotation, b.discs[i].position, b.discs[i].rotation)) return false;
            return true;
        }
        static bool SameMarbleBoard(MarbleMatch.BoardState a, MarbleMatch.BoardState b)
        {
            if (a == null || b == null || a.balls == null || b.balls == null || a.balls.Length != 8 || b.balls.Length != 8 || a.seed != b.seed || a.score != b.score ||
                a.shotsRemaining != b.shotsRemaining || a.layout != b.layout || (a.lastFeedback ?? "") != (b.lastFeedback ?? "") || a.lastShotGain != b.lastShotGain ||
                a.scoringStreak != b.scoringStreak || a.bestScoringStreak != b.bestScoringStreak) return false;
            for (int i = 0; i < a.balls.Length; i++) if (a.balls[i] == null || b.balls[i] == null || a.balls[i].claimed != b.balls[i].claimed || !SamePose(a.balls[i].position, a.balls[i].rotation, b.balls[i].position, b.balls[i].rotation)) return false;
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

        static string DescribeMarbleDifference(MarbleMatch.BoardState a, MarbleMatch.BoardState b)
        {
            if (a == null || b == null) return "board null before=" + (a == null) + " after=" + (b == null);
            var values = new List<string> { "seed=" + a.seed + "/" + b.seed, "layout=" + a.layout + "/" + b.layout,
                "score=" + a.score + "/" + b.score, "shots=" + a.shotsRemaining + "/" + b.shotsRemaining,
                "gain=" + a.lastShotGain + "/" + b.lastShotGain, "streak=" + a.scoringStreak + "/" + b.scoringStreak,
                "bestStreak=" + a.bestScoringStreak + "/" + b.bestScoringStreak, "feedbackSame=" + ((a.lastFeedback ?? "") == (b.lastFeedback ?? "")) };
            if (a.balls == null || b.balls == null || a.balls.Length != b.balls.Length) { values.Add("ball count differs/null"); return string.Join("; ", values); }
            for (int i = 0; i < a.balls.Length; i++)
            {
                var left = a.balls[i]; var right = b.balls[i];
                if (left == null || right == null) { values.Add("ball" + i + " null"); continue; }
                float componentDelta = Mathf.Max(Mathf.Abs(left.rotation.x - right.rotation.x), Mathf.Abs(left.rotation.y - right.rotation.y),
                    Mathf.Abs(left.rotation.z - right.rotation.z), Mathf.Abs(left.rotation.w - right.rotation.w));
                values.Add("ball" + i + " claimed=" + left.claimed + "/" + right.claimed +
                    " posDiff=" + Vector3.Distance(left.position, right.position).ToString("G9") +
                    " rawAngle=" + Quaternion.Angle(left.rotation, right.rotation).ToString("G9") +
                    " normalizedAngle=" + Quaternion.Angle(left.rotation.normalized, right.rotation.normalized).ToString("G9") +
                    " qNorm=" + Quaternion.Dot(left.rotation, left.rotation).ToString("G9") + "/" + Quaternion.Dot(right.rotation, right.rotation).ToString("G9") +
                    " qComponentDiff=" + componentDelta.ToString("G9"));
            }
            return string.Join("; ", values);
        }

        void CompleteReport()
        {
            report.completed = true;
            File.WriteAllText(Path.Combine(output, "runtime-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("SUMMER_SMOKE_COMPLETE errors=" + report.errors.Count + " pog=" + report.pogScore + " marble=" + report.marbleScore);
            Application.Quit(report.errors.Count == 0 ? 0 : 1);
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
