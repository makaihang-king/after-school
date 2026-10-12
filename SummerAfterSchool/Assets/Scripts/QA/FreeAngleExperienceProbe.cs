using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>自由站位和片角原生验收；屏幕输入、PhysX、计分与结算均走实际游戏路径。</summary>
    public sealed class FreeAngleExperienceProbe : MonoBehaviour
    {
        [Serializable] sealed class CheckResult { public string name, detail; public bool passed; }
        [Serializable] sealed class CaptureRecord
        {
            public string name, path, screen, location; public int width, height;
            public float cameraAzimuth, cameraElevation, cameraDistance, playerAzimuth, tiltPitch, tiltRoll;
            public Vector3 cameraPosition, launchPosition; public Quaternion cameraRotation, readyRotation;
            public bool nonBlank, decorationsChecked, decorationsClear;
        }
        [Serializable] sealed class LaunchRecord
        {
            public int turn, shotsUsed; public float azimuth, pitch, roll, power;
            public ShotKind kind; public Vector2 startScreen, endScreen;
            public Vector3 direction, position, velocity; public Quaternion previewRotation, releasedRotation;
        }
        [Serializable] sealed class ShotRecord { public ShotObservation physics; public string message, feedback; }
        [Serializable] sealed class PreviewCollisionStep
        {
            public float pitch, roll, maxPositionComponentDifference, normalizedQuaternionComponentDifference;
            public Vector3 targetPosition; public Quaternion targetRotation;
            public bool targetDynamic, previewCollidersDisabled; public int playerScore, npcScore, shotsUsed, targetPenetrationRecoveries;
        }
        [Serializable] sealed class PreviewCollisionFixture
        {
            public string purpose = "Separate collision regression fixture after the natural match result; target pose is arranged only to test preview overlap, with no predetermined score or settlement.";
            public bool passed, saveUnchanged, targetDynamicAtBaseline, targetRestingAtBaseline, otherTargetCollidersIsolated;
            public float baselineLinearSpeed, baselineAngularSpeed, baselineStablePositionDelta;
            public Vector3 targetBaselinePosition; public Quaternion targetBaselineRotation;
            public int baselinePlayerScore, baselineNpcScore, baselineShotsUsed, baselinePenetrationRecoveries;
            public List<PreviewCollisionStep> steps = new List<PreviewCollisionStep>();
        }
        [Serializable] sealed class Report
        {
            public bool completed, passed, isolatedSave, mobileUI, uiOnly, fullMatchValidated;
            public bool fourDirectionsValidated, realTiltValidated, pauseFrozen, resumedBoard, diskReadback, legacySetupDefaulted, futureSnapshotRejected;
            public bool resumedMetadataIdentical;
            public bool aimControlsRestoredAfterOverlays;
            public bool previewCollisionFixtureValidated;
            public string version, engine, platform, savePath, startedUtc, finishedUtc, outcome;
            public int matchSeed, playerScore, npcScore, cameraLocationsVerified, decorationViewsVerified;
            public float elapsedSeconds, resumedMaxPositionComponentDifference, resumedMaxNormalizedQuaternionComponentDifference;
            public List<CheckResult> checks = new List<CheckResult>();
            public List<CaptureRecord> captures = new List<CaptureRecord>();
            public List<LaunchRecord> launches = new List<LaunchRecord>();
            public List<ShotRecord> shots = new List<ShotRecord>();
            public PreviewCollisionFixture previewCollisionFixture;
            public List<string> errors = new List<string>();
        }
        readonly Report report = new Report();
        SummerGame game; string output; float started, deadline; bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--smoke-test") && args.Contains("--free-angle-experience-test") && !args.Contains("--qa-manual-inspection"))
                new GameObject("自由站位与片角 · 原生验收").AddComponent<FreeAngleExperienceProbe>();
        }
        void Awake()
        {
            started = Time.realtimeSinceStartup; deadline = started + 480;
            output = Path.GetFullPath(Arg("--qa-output") ?? Path.Combine(Application.dataPath, "../Logs/QA/FreeAngle"));
            Directory.CreateDirectory(output); report.startedUtc = DateTime.UtcNow.ToString("o");
            report.version = Application.version; report.engine = Application.unityVersion; report.platform = Application.platform.ToString();
            report.mobileUI = Environment.GetCommandLineArgs().Contains("--mobile-ui-test");
            report.uiOnly = Environment.GetCommandLineArgs().Contains("--free-angle-ui-only");
            Application.runInBackground = true; Application.logMessageReceived += OnLog;
        }
        IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                bool advanced = false; object current = null;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception e) { report.errors.Add(e.ToString()); Complete(false); }
                if (finished) yield break;
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
                else yield return current;
            }
            if (!finished) Complete(true);
        }
        IEnumerator Run()
        {
            yield return Wait(() => FindFirstObjectByType<SummerGame>()?.Pogs != null, 12, "startup");
            game = FindFirstObjectByType<SummerGame>(); yield return Tick();
            report.savePath = game.Repository.SavePath;
            report.isolatedSave = Arg("--qa-save") != null && string.Equals(Path.GetFullPath(Path.Combine(Arg("--qa-save"), "collection.json")), Path.GetFullPath(report.savePath), StringComparison.OrdinalIgnoreCase);
            Check("fresh-isolated-save", report.isolatedSave && !game.Repository.HasPendingMatch && game.Repository.Current.settlements.Count == 0, report.savePath);
            VerifyOcclusionMath();
            game.Pogs.ShotSettled += OnShot;
            game.GoHub(); game.SelectLocation("school_gate"); game.ShowLobby(); game.SelectNpc("ayue");
            game.SelectPlayMode(false, false); game.SelectedRule = RuleKind.Flip; game.SelectedPracticeLayout = PracticeLayout.Classic;
            game.RefreshUI(); yield return Tick(); ClickName("start-pogs");
            yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 8, "first-aim"); yield return Tick();
            Check("initial-preview-colliders-disabled", game.Pogs.Striker.PhysicsColliders.Count > 0 && game.Pogs.Striker.PhysicsColliders.All(c => !c.enabled), "Ready striker is visual and cannot push any target before release");
            report.matchSeed = game.ActiveMatch.randomSeed; string matchId = game.ActiveMatch.matchId;
            yield return SetupUI();
            yield return VerifyAimOverlayRecovery();
            if (report.uiOnly)
            {
                yield return Capture("mobile-free-angle-controls");
                game.Pogs.CancelAim(); game.UI.ShowPause(); yield return Capture("mobile-free-angle-pause");
                ClickText("继续玩"); yield return Tick();
                game.GoHub();
                if (game.Repository.HasPendingMatch) game.Repository.ForfeitPendingMatch();
                game.SelectLocation("corridor"); game.StartGuidedPractice(); yield return Tick();
                Check("mobile-teaching-entry", game.Screen == AppScreen.Pogs && game.GuidedPracticeMode,
                    "Teaching is entered after the isolated ordinary practice is normally closed");
                yield return EnsureSetupVisible();
                Check("teaching-angle-controls-disabled", !Root.Q<Slider>("shot-azimuth").enabledInHierarchy && !Root.Q<Slider>("shot-tilt-pitch").enabledInHierarchy && !Root.Q<Slider>("shot-tilt-roll").enabledInHierarchy, "Guided teaching keeps its demonstrated setup");
                yield return Capture("mobile-teaching-fixed-setup"); yield break;
            }
            // 避免改变比分或构造终局：先用实际四方向的低力贴地手法，再自然完成余下回合。
            foreach (float azimuth in new[] { 0f, 90f, 180f, 270f })
            {
                if (game.Screen != AppScreen.Result && game.Pogs.Phase != MatchPhase.Finished && game.Pogs.Phase != MatchPhase.PlayerAim)
                    yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim || game.Screen == AppScreen.Result || game.Pogs.Phase == MatchPhase.Finished,
                        30, "next-direction-aim-or-result-" + azimuth);
                // NPC 可能在上一手自然收够三张而提前结束；方向验收不能把这种合法自然结果当作缺少 PlayerAim。
                // 重新开启一场普通练习局，仍走真实入口与随机种子，不预设比分或结果。
                if (game.Screen == AppScreen.Result || game.Pogs.Phase == MatchPhase.Finished)
                {
                    game.GoHub(); game.SelectLocation("school_gate"); game.ShowLobby(); game.SelectNpc("ayue");
                    game.SelectPlayMode(false, false); game.SelectedRule = RuleKind.Flip; game.SelectedPracticeLayout = PracticeLayout.Classic;
                    game.RefreshUI(); yield return Tick(); ClickName("start-pogs");
                    yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 8, "natural-direction-restart-" + azimuth);
                    bool restartedReady = game.Pogs.Phase == MatchPhase.PlayerAim && game.ActiveMatch != null && game.Pogs.ShotsUsed == 0;
                    Check("natural-direction-restart-" + azimuth, restartedReady,
                        "A natural early result starts a fresh ordinary practice match for the remaining camera direction");
                    if (!restartedReady) yield break;
                    matchId = game.ActiveMatch.matchId;
                }
                yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 30, "azimuth-" + azimuth + "-aim");
                Check("set-azimuth-" + azimuth, game.Pogs.SetPlayerAzimuth(azimuth), "Player setup accepted before charging");
                game.World.SetMatchOrbit(azimuth, 42, 12, true); yield return Tick();
                Check("launch-on-chosen-side-" + azimuth, Vector3.Dot(Flat(game.Pogs.LaunchPosition - new Vector3(0, 0, .55f)).normalized, Quaternion.Euler(0, azimuth, 0) * Vector3.back) > .98f, game.Pogs.LaunchPosition.ToString("F5"));
                Check("set-tilt-" + azimuth, game.Pogs.SetDiscAngles(12 + azimuth / 30, -18 + azimuth / 30), "Physical preview is tilted");
                yield return Tick();
                Check("tilt-preview-body-" + azimuth, Quaternion.Angle(game.Pogs.Striker.Body.rotation, game.Pogs.ReadyRotation) < .3f && Vector3.Dot(game.Pogs.Striker.transform.up, Vector3.up) < .99f, "Actual preview Body.rotation=" + game.Pogs.Striker.Body.rotation);
                yield return Capture("school-ready-azimuth-" + azimuth, true);
                Vector3 direction = Quaternion.Euler(0, azimuth, 0) * Vector3.forward;
                int settledBefore = report.shots.Count;
                yield return PointerShot(direction, .14f, ShotKind.Skim, azimuth == 0);
                if (azimuth == 0)
                {
                    yield return new WaitForSeconds(.1f); game.UI.ShowPause(); yield return Tick();
                    string frozenBodies = Bodies(); float frozenAzimuth = game.Pogs.PlayerAzimuth; Quaternion frozenRotation = game.Pogs.Striker.Body.rotation;
                    yield return new WaitForSecondsRealtime(.4f);
                    report.pauseFrozen = frozenBodies == Bodies() && game.UI.PauseVisible && game.Pogs.Phase == MatchPhase.Flying;
                    Check("pause-freezes-real-bodies", report.pauseFrozen, "Body positions, rotations, velocities and angular velocities remain exact");
                    Check("pause-rejects-setup", !game.Pogs.SetPlayerAzimuth(180) && !game.Pogs.SetDiscAngles(30, 30) && game.Pogs.PlayerAzimuth == frozenAzimuth && game.Pogs.Striker.Body.rotation == frozenRotation, "Paused flight cannot alter shot setup");
                    yield return Capture("school-flight-paused"); ClickText("继续玩");
                }
                yield return Wait(() => report.shots.Count > settledBefore, 24, "real-physics-settle-" + azimuth);
                var shot = report.shots.Skip(settledBefore).First(x => x.physics.playerControlled);
                Check("real-movement-" + azimuth, shot.physics.maxMovement > .01f && shot.physics.normalizedPower > .06f, JsonUtility.ToJson(shot.physics));
                if (!shot.physics.scored) Check("quiet-miss-" + azimuth, string.IsNullOrEmpty(shot.message) && string.IsNullOrEmpty(shot.feedback), "Ordinary miss remains quiet");
            }
            report.fourDirectionsValidated = report.launches.Take(4).Select(x => x.azimuth).SequenceEqual(new[] { 0f, 90f, 180f, 270f });
            Check("four-directions-real-release", report.fourDirectionsValidated, "Each cardinal direction released through projected screen drag and settled in PhysX");
            report.realTiltValidated = report.launches.Take(4).All(x => Quaternion.Angle(x.previewRotation, x.releasedRotation) < .3f);
            Check("tilt-used-on-release", report.realTiltValidated, "Released Rigidbody keeps preview orientation at all four directions");
            if (game.Screen == AppScreen.Result || game.Pogs.Phase == MatchPhase.Finished)
            {
                game.GoHub();
                if (game.Repository.HasPendingMatch) game.Repository.ForfeitPendingMatch();
                game.SelectLocation("school_gate"); game.ShowLobby(); game.SelectNpc("ayue");
                game.SelectPlayMode(false, false); game.SelectedRule = RuleKind.Flip; game.SelectedPracticeLayout = PracticeLayout.Classic;
                game.RefreshUI(); yield return Tick(); ClickName("start-pogs");
                yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 8, "checkpoint-restart-aim");
                Check("checkpoint-restart", game.Pogs.Phase == MatchPhase.PlayerAim && game.ActiveMatch != null && game.Pogs.ShotsUsed == 0,
                    "A natural early result starts a fresh ordinary practice match before save/resume verification");
                if (game.Pogs.Phase != MatchPhase.PlayerAim || game.ActiveMatch == null) yield break;
                matchId = game.ActiveMatch.matchId;
            }
            yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 30, "checkpoint-aim");
            Check("checkpoint-setup", game.Pogs.SetPlayerAzimuth(235) && game.Pogs.SetDiscAngles(-17, 23), "Non-default setup for save and resume");
            yield return Tick(); string board = game.Pogs.CaptureBoard(); File.WriteAllText(Path.Combine(output, "board-before-resume.json"), board);
            game.UI.ShowPause(); yield return Tick(); ClickText("回到街区"); yield return Tick();
            Check("same-pending-match", game.Repository.HasPendingMatch && game.Repository.Current.pendingMatch.matchId == matchId, "Pending match preserved");
            var resume = Root.Query<Button>().ToList().FirstOrDefault(x => x.text.Contains("继续") && x.text.Contains("约战"));
            Check("resume-button", resume != null && resume.enabledInHierarchy, "Native hub provides resume"); Submit(resume);
            yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 8, "restored-aim"); yield return Tick();
            string restored = game.Pogs.CaptureBoard(); File.WriteAllText(Path.Combine(output, "board-after-resume.json"), restored);
            report.resumedBoard = SameRestoredBoard(board, restored);
            Check("exact-board-and-setup-resumed", report.resumedBoard && Mathf.Abs(game.Pogs.PlayerAzimuth - 235) < .001f && game.Pogs.TiltPitch == -17 && game.Pogs.TiltRoll == 23 && game.ActiveMatch.matchId == matchId, "snapshot3 restores geometry, random progress, score and shot setup");
            yield return Capture("school-resumed-setup");
            while (game.Screen != AppScreen.Result)
            {
                yield return Wait(() => game.Screen == AppScreen.Result || game.Pogs.Phase == MatchPhase.PlayerAim, 30, "next-natural-turn");
                if (game.Screen == AppScreen.Result) break;
                float azimuth = (game.Pogs.ShotsUsed * 47) % 360;
                Check("natural-next-setup", game.Pogs.SetPlayerAzimuth(azimuth) && game.Pogs.SetDiscAngles(8, -8), "Remaining actual player turn");
                game.World.SetMatchOrbit(azimuth, 42, 12, true); yield return Tick();
                var target = game.Pogs.Targets.Where(x => !x.Claimed).OrderBy(x => Vector3.Distance(x.Body.position, game.Pogs.LaunchPosition)).First();
                Vector3 delta = Flat(target.Body.position - game.Pogs.LaunchPosition);
                float power = Mathf.Clamp((delta.magnitude - 1.25f) / 3.3f, .18f, .85f); int settledBefore = report.shots.Count;
                yield return PointerShot(delta.normalized, power, ShotKind.Slap, false);
                yield return Wait(() => report.shots.Count > settledBefore || game.Screen == AppScreen.Result, 24, "natural-shot-settled");
            }
            report.fullMatchValidated = game.LastSettlement != null && game.LastSettlement.matchId == matchId && !game.Repository.HasPendingMatch && report.shots.Count >= report.launches.Count && report.launches.Count >= 4 && report.shots.Any(x => !x.physics.playerControlled);
            Check("natural-match-result", report.fullMatchValidated, JsonUtility.ToJson(game.LastSettlement));
            Check("natural-real-target-contact", report.shots.Any(x => x.physics.targetCollisionCount > 0), "Real natural-match observations still record striker/target contact");
            report.outcome = game.LastSettlement.outcome.ToString(); report.playerScore = game.Pogs.PlayerScore; report.npcScore = game.Pogs.NpcScore;
            yield return Capture("school-natural-result");
            var current = game.Repository.Current; var disk = game.Repository.Reload();
            report.diskReadback = JsonUtility.ToJson(current) == JsonUtility.ToJson(disk);
            Check("result-disk-readback", report.diskReadback && disk.settlements.Any(x => x.matchId == matchId), "Natural settlement reads back unchanged");
            yield return SnapshotCompatibility();
            yield return PreviewCollisionRegression();
            // 此处只验收不同场景环绕画面与待出手姿态，不重复全量场景物理矩阵。
            foreach (string location in new[] { "school_gate", "corridor" })
            {
                game.GoHub();
                // 环绕验收每个地点都使用独立的练习局；离开上一地点后清理其暂停练习，避免真实入口的 pending gate 阻断下一地点。
                if (game.Repository.HasPendingMatch)
                {
                    var discarded = game.Repository.ForfeitPendingMatch();
                    Check("camera-practice-cleanup-" + location, discarded != null && !game.Repository.HasPendingMatch,
                        "Previous isolated camera practice ended before entering the next location");
                }
                game.SelectLocation(location); game.ShowLobby(); game.SelectPlayMode(true, false); game.StartPogs();
                yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 8, location + "-camera-aim");
                foreach (float azimuth in new[] { 0f, 90f, 180f, 270f })
                {
                    Check(location + "-camera-setup-" + azimuth, game.Pogs.SetPlayerAzimuth(azimuth), "Player launch anchor follows orbit");
                    game.World.SetMatchOrbit(azimuth, 42, 12, true); yield return Tick();
                    Check(location + "-camera-orbit-" + azimuth, Mathf.Abs(Mathf.DeltaAngle(game.World.MatchAzimuth, azimuth)) < .001f && Mathf.Abs(game.World.MatchElevation - 42) < .001f, "Actual orbit angle=" + game.World.MatchAzimuth);
                    Vector3 projected = game.World.MainCamera.WorldToViewportPoint(new Vector3(0, 0, .55f));
                    Check(location + "-center-visible-" + azimuth, projected.z > 0 && projected.x > .05f && projected.x < .95f && projected.y > .05f && projected.y < .95f, projected.ToString("F4"));
                    VerifyLaunchAnchor(location + "-default-" + azimuth);
                    yield return Capture(location + "-orbit-" + azimuth, true);
                }
                int edge = 0;
                foreach (float elevation in new[] { 32f, 72f })
                foreach (float distance in new[] { 10f, 15f })
                {
                    float azimuth = edge++ * 90;
                    Check(location + "-edge-setup-" + edge, game.Pogs.SetPlayerAzimuth(azimuth), "Camera boundary setup");
                    game.World.SetMatchOrbit(azimuth, elevation, distance, true); yield return Tick();
                    Check(location + "-edge-orbit-" + edge, game.World.MatchElevation == elevation && game.World.MatchDistance == distance, "Actual camera boundary=" + elevation + "/" + distance);
                    VerifyLaunchAnchor(location + "-edge-" + edge);
                    yield return Capture(location + "-orbit-e" + elevation + "-d" + distance, true);
                }
                Check(location + "-diagonal-edge-setup", game.Pogs.SetPlayerAzimuth(45), "Diagonal view tests the widest required field of view");
                game.World.SetMatchOrbit(45, 72, 10, true); yield return Tick();
                Check(location + "-diagonal-edge-orbit", game.World.MatchAzimuth == 45 && game.World.MatchElevation == 72 && game.World.MatchDistance == 10, "Actual requested orbit=45/72/10");
                VerifyLaunchAnchor(location + "-diagonal-e72-d10");
                yield return Capture(location + "-diagonal-az45-e72-d10", true);
                game.World.SetTopDownMatchView(true, true); yield return Tick();
                VerifyLaunchAnchor(location + "-topdown"); yield return Capture(location + "-topdown-free-angle", true);
                game.World.SetTopDownMatchView(false, true); yield return Tick();
                report.cameraLocationsVerified++;
            }
            game.GoHub();
        }
        IEnumerator SetupUI()
        {
            yield return EnsureSetupVisible();
            var panel = Root.Q<VisualElement>("shot-setup-panel");
            Check("setup-panel-contained", panel != null && IsDisplayed(panel) && Inside(panel.worldBound, Root.worldBound), panel?.worldBound.ToString() ?? "missing");
            foreach (string name in report.mobileUI ? new[] { "shot-setup-reset", "shot-setup-close" } : new[] { "shot-setup-reset" })
            {
                var button = Root.Q<Button>(name);
                Check("setup-button-contained-" + name, button != null && IsDisplayed(button) && Inside(button.worldBound, panel.worldBound) && button.worldBound.height >= (report.mobileUI ? 40 : 28), button?.worldBound.ToString() ?? "missing");
            }
            foreach (string name in new[] { "shot-azimuth", "shot-tilt-pitch", "shot-tilt-roll" })
            {
                var slider = Root.Q<Slider>(name);
                Check("visible-control-" + name, slider != null && slider.enabledInHierarchy && IsDisplayed(slider) && Inside(slider.worldBound, Root.worldBound) && slider.worldBound.width > 100 && slider.worldBound.height >= 20, slider?.worldBound.ToString() ?? "missing");
                Vector2 p = PanelToScreen(slider.worldBound.center);
                Check("control-blocks-shot-" + name, game.UI.BlocksInput(p), "Setup control blocks the underlying floor pointer");
            }
            Root.Q<Slider>("shot-azimuth").value = 90; Root.Q<Slider>("shot-tilt-pitch").value = 16; Root.Q<Slider>("shot-tilt-roll").value = -21;
            yield return Tick();
            Check("setup-ui-updates-real-state", Mathf.Abs(game.Pogs.PlayerAzimuth - 90) < .001f && game.Pogs.TiltPitch == 16 && game.Pogs.TiltRoll == -21, "Slider callbacks changed production setup");
            if (report.mobileUI)
            {
                var feedback = Root.Query<VisualElement>().ToList().FirstOrDefault(x => x.ClassListContains("mobile-match-feedback") && IsDisplayed(x));
                Check("setup-feedback-not-overlapping", feedback != null && !feedback.worldBound.Overlaps(panel.worldBound), feedback?.worldBound.ToString() ?? "missing");
                Vector3 projected = game.World.MainCamera.WorldToScreenPoint(game.Pogs.LaunchPosition);
                Check("mobile-launch-anchor-unblocked", projected.z > 0 && OnScreen(projected) && !game.UI.BlocksInput(projected), "Actual launch anchor=" + projected);
            }
            yield return Capture("free-angle-ui-adjusted");
            int used = game.Pogs.ShotsUsed; Vector2 blocked = PanelToScreen(Root.Q<Slider>("shot-tilt-pitch").worldBound.center);
            game.Pogs.ProcessPointer(blocked, true, true, false, false); game.Pogs.ProcessPointer(blocked + Vector2.down * 80, false, false, true, false);
            Check("ui-pointer-cannot-fire", !game.Pogs.IsDragging && game.Pogs.ShotsUsed == used, "A slider gesture never starts a shot");
            Check("tilt-safe-range", game.Pogs.SetDiscAngles(100, -100) && game.Pogs.TiltPitch == 35 && game.Pogs.TiltRoll == -35, "Tilt input clamps to ±35 degrees");
            string before = game.Pogs.CaptureBoard();
            Check("invalid-setup-rejected", !game.Pogs.SetDiscAngles(float.NaN, 0) && !game.Pogs.SetPlayerAzimuth(float.PositiveInfinity) && before == game.Pogs.CaptureBoard(), "Non-finite setup cannot corrupt board");
            ClickName("shot-setup-reset"); yield return Tick();
            Check("setup-reset", game.Pogs.PlayerAzimuth == 0 && game.Pogs.TiltPitch == 0 && game.Pogs.TiltRoll == 0, "Reset returns to familiar default setup");
            yield return Capture("free-angle-ui-reset");
            yield return EnsureSetupClosed();
        }
        IEnumerator EnsureSetupVisible()
        {
            var slider = Root.Q<Slider>("shot-azimuth");
            if (report.mobileUI && (slider == null || !IsDisplayed(slider))) { ClickName("shot-setup-toggle"); yield return Tick(); }
        }
        IEnumerator VerifyAimOverlayRecovery()
        {
            int shots = game.Pogs.ShotsUsed, playerScore = game.Pogs.PlayerScore, npcScore = game.Pogs.NpcScore;
            float azimuth = game.Pogs.PlayerAzimuth, pitch = game.Pogs.TiltPitch, roll = game.Pogs.TiltRoll;
            foreach (string overlay in new[] { "pause", "settings", "exit", "alert" })
            {
                switch (overlay)
                {
                    case "pause": game.UI.ShowPause(); break;
                    case "settings": game.UI.ShowAudioSettings(); break;
                    case "exit": game.UI.ShowExitConfirm(); break;
                    case "alert": game.UI.ShowNotice("操作验收", "关闭提示后，站位与片角仍应可调。"); break;
                }
                yield return Tick();
                // 与实际失焦回调同一生产路径；报告只宣称回调恢复，OS Alt-Tab另由人工验收。
                game.Pogs.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver); yield return Tick();
                Check(overlay + "-focus-paused-controls-disabled", Time.timeScale == 0 && game.Pogs.Phase == MatchPhase.PlayerAim && SetupControlsEnabled(false), "Paused overlay plus production focus callback leaves all three sliders and reset disabled");
                if (overlay == "pause")
                {
                    game.UI.ShowAudioSettings(); yield return Tick();
                    Check("settings-over-pause-visible", game.UI.PauseVisible && game.UI.SettingsVisible && Time.timeScale == 0 && SetupControlsEnabled(false), "Settings opened over pause keep the board paused");
                    Check("settings-over-pause-close", game.UI.HandleEscape(), "Escape closes the top settings layer"); yield return Tick();
                    Check("settings-over-pause-retains-disabled", game.UI.PauseVisible && !game.UI.SettingsVisible && Time.timeScale == 0 && SetupControlsEnabled(false), "Closing settings restores the underlying pause and disabled angle controls");
                }
                Check(overlay + "-close-handled", game.UI.HandleEscape(), "The actual top overlay handles Escape"); yield return Tick();
                game.Pogs.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver); yield return Tick();
                Check(overlay + "-aim-controls-restored", Time.timeScale == 1 && !game.UI.PauseVisible && !game.UI.SettingsVisible && !game.UI.ExitVisible && !game.UI.AlertVisible && SetupControlsEnabled(true), "Closing the overlay restores all three sliders and reset without another shot or rerender");
                Check(overlay + "-setup-and-score-preserved", game.Pogs.ShotsUsed == shots && game.Pogs.PlayerScore == playerScore && game.Pogs.NpcScore == npcScore && game.Pogs.PlayerAzimuth == azimuth && game.Pogs.TiltPitch == pitch && game.Pogs.TiltRoll == roll && !game.Pogs.IsDragging, "Focus cancellation and modal close consume no turn and retain chosen setup");
            }
            report.aimControlsRestoredAfterOverlays = true;
            yield return Capture("aim-controls-after-modal-focus");
        }
        bool SetupControlsEnabled(bool expected)
        {
            foreach (string name in new[] { "shot-azimuth", "shot-tilt-pitch", "shot-tilt-roll", "shot-setup-reset" })
            {
                var control = Root.Q<VisualElement>(name);
                if (control == null || control.enabledInHierarchy != expected) return false;
            }
            return true;
        }
        IEnumerator EnsureSetupClosed()
        {
            var close = Root.Q<Button>("shot-setup-close");
            if (close != null && IsDisplayed(close)) { Submit(close); yield return Tick(); }
        }
        void VerifyLaunchAnchor(string name)
        {
            Vector3 projected = game.World.MainCamera.WorldToScreenPoint(game.Pogs.Striker.Body.position);
            Check(name + "-striker-visible-unblocked", projected.z > 0 && OnScreen(projected) && !game.UI.BlocksInput(projected), "Actual striker screen=" + projected.ToString("F4"));
            bool fullRim = true;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                Vector3 edge = game.Pogs.Striker.Body.position + game.Pogs.Striker.Body.rotation *
                    new Vector3(Mathf.Cos(angle) * game.Pogs.Striker.Radius, 0, Mathf.Sin(angle) * game.Pogs.Striker.Radius);
                Vector3 pixel = game.World.MainCamera.WorldToScreenPoint(edge);
                fullRim &= pixel.z > 0 && OnScreen(pixel) && !game.UI.BlocksInput(pixel);
            }
            Check(name + "-whole-disc-visible-unblocked", fullRim, "Sixteen points of the actual tilted paper rim remain clear of UI");
            int used = game.Pogs.ShotsUsed;
            game.Pogs.ProcessPointer(projected, true, true, false, false);
            Check(name + "-striker-can-start-drag", game.Pogs.IsDragging, "A pointer at the actual rendered striker begins charging");
            game.Pogs.ProcessPointer(projected, false, false, false, true);
            Check(name + "-striker-drag-cancel", !game.Pogs.IsDragging && game.Pogs.ShotsUsed == used && game.Pogs.Phase == MatchPhase.PlayerAim, "Visual launch point stays playable at this camera angle");
        }
        bool VerifyDecorationVisibility(string name)
        {
            var renderers = game.World.GetComponentsInChildren<MeshRenderer>(true);
            var candidates = new List<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                if (game.World.FloorCollider != null && renderer.transform == game.World.FloorCollider.transform) continue;
                if (renderer.GetComponentInParent<DiscBody>() != null) continue;
                bool excluded = false;
                for (var parent = renderer.transform; parent != null && parent != game.World.transform; parent = parent.parent)
                {
                    // 实体地形/围边承载并碰撞圆片；它不是可临时让开的装饰。片缘是否完整仍由
                    // VerifyLaunchAnchor 的真实屏幕投影检查，建筑、门、树和电线才走网格 LOS。
                    if (parent.name == "打片 · 实体地形与路沿边界" || parent.name.StartsWith("路人", StringComparison.Ordinal) ||
                        parent.name.StartsWith("街区伙伴", StringComparison.Ordinal) || parent.name.Contains("微缩儿童")) { excluded = true; break; }
                }
                if (!excluded) candidates.Add(renderer);
            }
            var discs = game.Pogs.Targets.Where(d => !d.Claimed && d.gameObject.activeInHierarchy).ToList();
            if (game.Pogs.Striker != null && game.Pogs.Striker.gameObject.activeInHierarchy) discs.Add(game.Pogs.Striker);
            var blocking = new HashSet<string>(); int rays = 0, broadphaseHits = 0, triangleTests = 0; Vector3 camera = game.World.MainCamera.transform.position;
            var geometry = new Dictionary<MeshRenderer, OccluderGeometry>();
            foreach (var disc in discs)
            {
                for (int sample = -1; sample < 16; sample++)
                {
                    Vector3 point = disc.Body.position;
                    if (sample >= 0)
                    {
                        float angle = sample * Mathf.PI * 2 / 16;
                        point += disc.Body.rotation * new Vector3(Mathf.Cos(angle) * disc.Radius, 0, Mathf.Sin(angle) * disc.Radius);
                    }
                    Vector3 delta = point - camera; float distance = delta.magnitude; rays++;
                    var ray = new Ray(camera, delta / distance);
                    foreach (var renderer in candidates)
                    {
                        if (!renderer.bounds.IntersectRay(ray, out float hitDistance) || hitDistance >= distance - .03f) continue;
                        broadphaseHits++;
                        if (!geometry.TryGetValue(renderer, out var mesh)) { mesh = ReadOccluderGeometry(renderer); geometry.Add(renderer, mesh); }
                        bool intersects = mesh.error != null;
                        if (!intersects)
                        {
                            for (int triangle = 0; triangle + 2 < mesh.triangles.Length; triangle += 3)
                            {
                                triangleTests++;
                                if (!SegmentIntersectsTriangle(ray, distance - .03f, mesh.vertices[mesh.triangles[triangle]], mesh.vertices[mesh.triangles[triangle + 1]], mesh.vertices[mesh.triangles[triangle + 2]])) continue;
                                intersects = true; break;
                            }
                        }
                        if (!intersects) continue;
                        var path = new List<string>();
                        for (var parent = renderer.transform; parent != null && parent != game.World.transform; parent = parent.parent) path.Add(parent.name);
                        path.Reverse(); blocking.Add(string.Join("/", path) + (mesh.error == null ? " [actual triangle hit]" : " [geometry could not be inspected: " + mesh.error + "]"));
                    }
                }
            }
            string detail = "Live world MeshRenderers=" + renderers.Length + ", enabled active eligible=" + candidates.Count + ", real disc center/rim rays=" + rays + ", AABB broadphase hits=" + broadphaseHits + ", world-space double-sided triangle tests=" + triangleTests + ", front clearance=0.03m; blockers=" +
                (blocking.Count == 0 ? "none" : string.Join(" | ", blocking.OrderBy(x => x)));
            Check(name + "-visible-decoration-line-of-sight", renderers.Length > 0 && discs.Count > 0 && blocking.Count == 0, detail);
            report.decorationViewsVerified++; return true;
        }
        sealed class OccluderGeometry { public Vector3[] vertices; public int[] triangles; public string error; }
        static OccluderGeometry ReadOccluderGeometry(MeshRenderer renderer)
        {
            var result = new OccluderGeometry();
            try
            {
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter == null ? null : filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) { result.error = mesh == null ? "MeshFilter.sharedMesh is missing" : "Mesh is not readable"; return result; }
                result.vertices = mesh.vertices;
                Matrix4x4 matrix = renderer.transform.localToWorldMatrix;
                for (int i = 0; i < result.vertices.Length; i++) result.vertices[i] = matrix.MultiplyPoint3x4(result.vertices[i]);
                var triangles = new List<int>();
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) { result.error = "Visible submesh uses a non-triangle topology"; return result; }
                    triangles.AddRange(mesh.GetTriangles(submesh));
                }
                result.triangles = triangles.ToArray();
            }
            catch (Exception e) { result.error = e.GetType().Name + ": " + e.Message; }
            return result;
        }
        static bool SegmentIntersectsTriangle(Ray ray, float maximumDistance, Vector3 a, Vector3 b, Vector3 c)
        {
            // Moller-Trumbore双面交点：AABB只用于筛选，圆环/窗洞内部不再被当作实心遮挡。
            Vector3 edge1 = b - a, edge2 = c - a, cross = Vector3.Cross(ray.direction, edge2);
            float determinant = Vector3.Dot(edge1, cross);
            if (Mathf.Abs(determinant) < 1e-8f) return false;
            float inverse = 1f / determinant; Vector3 offset = ray.origin - a;
            float u = Vector3.Dot(offset, cross) * inverse;
            if (u < -1e-6f || u > 1f + 1e-6f) return false;
            Vector3 q = Vector3.Cross(offset, edge1);
            float v = Vector3.Dot(ray.direction, q) * inverse;
            if (v < -1e-6f || u + v > 1f + 1e-6f) return false;
            float distance = Vector3.Dot(edge2, q) * inverse;
            return distance >= 0f && distance < maximumDistance;
        }
        void VerifyOcclusionMath()
        {
            Vector3 a = new Vector3(-1, 1, -1), b = new Vector3(1, 1, -1), c = new Vector3(0, 1, 1);
            var front = new Ray(new Vector3(0, 3, 0), Vector3.down);
            Check("occlusion-math-real-front-triangle", SegmentIntersectsTriangle(front, 2.5f, a, b, c), "A real foreground triangle must obstruct the segment");
            Check("occlusion-math-double-sided", SegmentIntersectsTriangle(front, 2.5f, a, c, b) && SegmentIntersectsTriangle(new Ray(new Vector3(0, -1, 0), Vector3.up), 2.5f, a, b, c), "Reversed winding and viewing from the back must both intersect");
            Check("occlusion-math-behind-target", !SegmentIntersectsTriangle(front, 1.5f, a, b, c), "A triangle beyond the target endpoint is clear");
            Vector3 nearA = new Vector3(-1, .02f, -1), nearB = new Vector3(1, .02f, -1), nearC = new Vector3(0, .02f, 1);
            Check("occlusion-math-clearance-boundary", !SegmentIntersectsTriangle(front, 3f - .03f, nearA, nearB, nearC) &&
                SegmentIntersectsTriangle(front, 3f - .03f, nearA + Vector3.up * .02f, nearB + Vector3.up * .02f, nearC + Vector3.up * .02f), "The unchanged 0.03m clearance distinguishes 0.02m and 0.04m foreground offsets");
            // 真正空心环的三角网格，中心射线穿过AABB，却不穿过任何表面。
            bool centerHit = false, rimHit = false; var rimRay = new Ray(new Vector3(1.8f, 3, 0), Vector3.down);
            for (int segment = 0; segment < 32; segment++)
            {
                float angle1 = segment * Mathf.PI * 2 / 32, angle2 = (segment + 1) * Mathf.PI * 2 / 32;
                Vector3 p0 = new Vector3(Mathf.Cos(angle1) * 1.65f, 1, Mathf.Sin(angle1) * 1.65f);
                Vector3 p1 = new Vector3(Mathf.Cos(angle1) * 1.95f, 1, Mathf.Sin(angle1) * 1.95f);
                Vector3 p2 = new Vector3(Mathf.Cos(angle2) * 1.65f, 1, Mathf.Sin(angle2) * 1.65f);
                Vector3 p3 = new Vector3(Mathf.Cos(angle2) * 1.95f, 1, Mathf.Sin(angle2) * 1.95f);
                centerHit |= SegmentIntersectsTriangle(front, 2.5f, p0, p2, p1) || SegmentIntersectsTriangle(front, 2.5f, p1, p2, p3);
                rimHit |= SegmentIntersectsTriangle(rimRay, 2.5f, p0, p2, p1) || SegmentIntersectsTriangle(rimRay, 2.5f, p1, p2, p3);
            }
            Check("occlusion-math-hollow-ring", !centerHit && rimHit, "The ring's open center is clear while its real rim surface still obstructs");
        }
        IEnumerator PointerShot(Vector3 direction, float power, ShotKind kind, bool cancelFirst)
        {
            Check("ready-colliders-disabled-" + game.Pogs.ShotsUsed, game.Pogs.Striker.PhysicsColliders.Count > 0 && game.Pogs.Striker.PhysicsColliders.All(c => !c.enabled), "Prepared striker is excluded from real target collision");
            game.Pogs.Shot = kind; game.Pogs.Spin = 0; direction = Flat(direction).normalized;
            bool planned = false; Vector2 a = default, b = default;
            foreach (float z in new[] { .55f, .2f, .9f, -.15f, 1.25f })
            foreach (float x in new[] { 0f, -.45f, .45f, -.9f, .9f })
            {
                if (planned) continue;
                Vector3 start = new Vector3(x, 0, z), end = start - direction * (power * 2.25f);
                Vector3 ap = game.World.MainCamera.WorldToScreenPoint(start), bp = game.World.MainCamera.WorldToScreenPoint(end);
                if (ap.z <= 0 || bp.z <= 0 || !OnScreen(ap) || !OnScreen(bp) || game.UI.BlocksInput(ap) || game.UI.BlocksInput(bp)) continue;
                a = ap; b = bp; planned = true;
            }
            Check("screen-drag-plan-" + game.Pogs.ShotsUsed, planned, "Unobstructed projected floor gesture exists for direction=" + direction);
            int used = game.Pogs.ShotsUsed;
            game.Pogs.ProcessPointer(a, true, true, false, false); yield return null;
            game.Pogs.ProcessPointer(b, false, true, false, false); yield return null;
            Check("screen-drag-power-" + used, game.Pogs.IsDragging && Mathf.Abs(game.Pogs.Power - power) < .025f, "Power from actual screen drag=" + game.Pogs.Power);
            if (cancelFirst)
            {
                float azimuth = game.Pogs.PlayerAzimuth;
                Check("charging-rejects-standing-change", !game.Pogs.SetPlayerAzimuth(90) && game.Pogs.PlayerAzimuth == azimuth, "Standing position stays fixed during charging");
                Check("charging-allows-tilt", game.Pogs.SetDiscAngles(19, -13), "Tilt can be refined during charging");
                Check("charging-tilt-preview", Quaternion.Angle(game.Pogs.Striker.Body.rotation, game.Pogs.ReadyRotation) < .3f, "Ready tilt is real Body pose during drag");
                game.Pogs.ProcessPointer(b, false, false, false, true); yield return Tick();
                Check("pointer-cancel-preserves-turn", !game.Pogs.IsDragging && game.Pogs.Power == 0 && game.Pogs.ShotsUsed == used && game.Pogs.Phase == MatchPhase.PlayerAim && game.Pogs.PlayerAzimuth == azimuth, "Cancellation consumes no shot and retains setup");
                game.Pogs.ProcessPointer(a, true, true, false, false); yield return null;
                game.Pogs.ProcessPointer(b, false, true, false, false); yield return null;
            }
            var launch = new LaunchRecord { turn = game.Pogs.Turn, shotsUsed = used + 1, azimuth = game.Pogs.PlayerAzimuth, pitch = game.Pogs.TiltPitch, roll = game.Pogs.TiltRoll, power = game.Pogs.Power, kind = kind,
                startScreen = a, endScreen = b, direction = direction, previewRotation = game.Pogs.ReadyRotation };
            game.Pogs.ProcessPointer(b, false, false, true, false);
            launch.position = game.Pogs.Striker.Body.position; launch.releasedRotation = game.Pogs.Striker.Body.rotation; launch.velocity = game.Pogs.Striker.Body.linearVelocity;
            report.launches.Add(launch); Flush();
            Check("pointer-real-flight-" + used, game.Pogs.Phase == MatchPhase.Flying && game.Pogs.ShotsUsed == used + 1 && !game.Pogs.Striker.Body.isKinematic, "Release starts a real Rigidbody flight");
            Check("launch-colliders-enabled-" + used, game.Pogs.Striker.PhysicsColliders.Count > 0 && game.Pogs.Striker.PhysicsColliders.All(c => c.enabled), "All actual striker collision shapes are enabled on release");
            Check("unclamped-direction-" + used, Vector3.Dot(Flat(launch.velocity).normalized, direction) > .999f, JsonUtility.ToJson(launch));
            Check("release-uses-preview-angle-" + used, Quaternion.Angle(launch.previewRotation, launch.releasedRotation) < .3f, "Body release matches physical preview");
        }
        IEnumerator SnapshotCompatibility()
        {
            game.GoHub(); yield return null;
            string saveBefore = JsonUtility.ToJson(game.Repository.Current);
            var host = new GameObject("自由角度 · 旧快照隔离验收"); var pog = host.AddComponent<PogMatch>(); pog.Configure(game.World, game.Audio);
            try
            {
                pog.Begin(GameCatalog.GetCard("pog_00"), GameCatalog.GetNpc("xiaoyu"), RuleKind.Flip, true, 391, PracticeLayout.Classic, game.World.Environment);
                yield return Wait(() => pog.Phase == MatchPhase.PlayerAim, 8, "isolated-snapshot-aim");
                string old = pog.CaptureBoard();
                foreach (string field in new[] { "playerAzimuth", "tiltPitch", "tiltRoll" }) old = Regex.Replace(old, "\"" + field + "\"\\s*:\\s*[-+0-9.eE]+\\s*,?", "");
                old = old.Replace(",}", "}"); File.WriteAllText(Path.Combine(output, "legacy-v3-no-setup-fields.json"), old);
                Check("legacy-has-no-setup-fields", !old.Contains("playerAzimuth") && !old.Contains("tiltPitch") && !old.Contains("tiltRoll"), "Fixture is a real version3 shape without optional fields");
                Check("isolated-nondefault-setup", pog.SetPlayerAzimuth(117) && pog.SetDiscAngles(24, -28), "Existing setup cannot leak into old snapshot restore");
                report.legacySetupDefaulted = pog.RestoreBoard(old) && pog.PlayerAzimuth == 0 && pog.TiltPitch == 0 && pog.TiltRoll == 0;
                var legacyBoard = JsonUtility.FromJson<PogMatch.BoardState>(old); var actual = JsonUtility.FromJson<PogMatch.BoardState>(pog.CaptureBoard());
                Check("legacy-v3-setup-defaults", report.legacySetupDefaulted && legacyBoard.version == 3 && legacyBoard.playerScore == actual.playerScore && legacyBoard.npcScore == actual.npcScore && legacyBoard.shotsUsed == actual.shotsUsed && legacyBoard.discs.Length == actual.discs.Length, "Old snapshot defaults new setup to zero and keeps board progress");
                for (int i = 0; i < legacyBoard.discs.Length; i++) Check("legacy-v3-disc-" + i, legacyBoard.discs[i].position == actual.discs[i].position && Quaternion.Angle(legacyBoard.discs[i].rotation, actual.discs[i].rotation) < .001f && legacyBoard.discs[i].claimed == actual.discs[i].claimed, "Old snapshot disc pose and claimed state preserved");
                string stable = pog.CaptureBoard(); var future = JsonUtility.FromJson<PogMatch.BoardState>(stable); future.version = 4;
                report.futureSnapshotRejected = !pog.RestoreBoard(JsonUtility.ToJson(future)) && stable == pog.CaptureBoard();
                Check("future-snapshot-rejected", report.futureSnapshotRejected, "Future snapshot is rejected without changing current board");
            }
            finally { pog.StopMatch(); Destroy(host); }
            yield return null; Check("compatibility-does-not-write-save", saveBefore == JsonUtility.ToJson(game.Repository.Current), "Isolated compatibility host leaves collection and natural result unchanged");
        }
        IEnumerator PreviewCollisionRegression()
        {
            // 仅在自然结算及读盘完成后测试极端重叠；此宿主没有生产协调器的计分/保存订阅。
            game.GoHub(); yield return null; string saveBefore = JsonUtility.ToJson(game.Repository.Current);
            var fixture = new PreviewCollisionFixture(); report.previewCollisionFixture = fixture;
            var host = new GameObject("自由角度 · 待出手碰撞隔离验收"); var pog = host.AddComponent<PogMatch>(); pog.Configure(game.World, game.Audio);
            try
            {
                pog.Begin(GameCatalog.GetCard("pog_00"), GameCatalog.GetNpc("xiaoyu"), RuleKind.Flip, true, 681, PracticeLayout.Classic, game.World.Environment);
                yield return Wait(() => pog.Phase == MatchPhase.PlayerAim, 8, "preview-fixture-aim");
                FixtureCheck("preview-fixture-zero-anchor", pog.SetPlayerAzimuth(0), "Read the actual zero-degree launch anchor"); Vector3 zeroAnchor = pog.LaunchPosition;
                FixtureCheck("preview-fixture-starts-away", pog.SetPlayerAzimuth(90), "Move visual preview away before arranging the dynamic target");
                var target = pog.Targets.First(d => !d.Claimed);
                foreach (var other in pog.Targets.Where(d => d != target)) foreach (var collider in other.PhysicsColliders) collider.enabled = false;
                fixture.otherTargetCollidersIsolated = true;
                Vector3 position = zeroAnchor; position.y = pog.RestHeight(target, position, Quaternion.identity, target.RestClearance);
                target.transform.SetPositionAndRotation(position, Quaternion.identity); target.Body.position = position; target.Body.rotation = Quaternion.identity;
                target.Body.isKinematic = false; target.Body.linearVelocity = Vector3.zero; target.Body.angularVelocity = Vector3.zero;
                Physics.SyncTransforms(); target.Body.Sleep();
                for (int step = 0; step < 12; step++) yield return new WaitForFixedUpdate();
                Vector3 stablePosition = target.Body.position; float stableDelta = 0f;
                for (int step = 0; step < 4; step++) { yield return new WaitForFixedUpdate(); Vector3 delta = target.Body.position - stablePosition; stableDelta = Mathf.Max(stableDelta, Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z))); stablePosition = target.Body.position; }
                float linearSpeed = target.Body.linearVelocity.magnitude, angularSpeed = target.Body.angularVelocity.magnitude;
                bool dynamicResting = !target.Body.isKinematic && target.PhysicsColliders.All(c => c.enabled) && linearSpeed <= 1e-4f && angularSpeed <= 1e-4f && stableDelta <= 1e-6f;
                fixture.baselineLinearSpeed = linearSpeed; fixture.baselineAngularSpeed = angularSpeed; fixture.baselineStablePositionDelta = stableDelta; fixture.targetRestingAtBaseline = dynamicResting;
                FixtureCheck("preview-fixture-dynamic-resting-target", dynamicResting, "Dynamic=" + !target.Body.isKinematic + ", colliders=" + target.PhysicsColliders.All(c => c.enabled) + ", sleeping=" + target.Body.IsSleeping() + ", linearSpeed=" + linearSpeed.ToString("R") + ", angularSpeed=" + angularSpeed.ToString("R") + ", fourStepMaxDelta=" + stableDelta.ToString("R"));
                fixture.targetBaselinePosition = target.Body.position; fixture.targetBaselineRotation = target.Body.rotation; fixture.targetDynamicAtBaseline = !target.Body.isKinematic;
                fixture.baselinePlayerScore = pog.PlayerScore; fixture.baselineNpcScore = pog.NpcScore; fixture.baselineShotsUsed = pog.ShotsUsed; fixture.baselinePenetrationRecoveries = target.PenetrationRecoveries;
                FixtureCheck("preview-fixture-move-over-target", pog.SetPlayerAzimuth(0), "Visual preview now occupies the dynamic target's horizontal area");
                foreach (Vector2 angles in new[] { Vector2.zero, new Vector2(35, 35), new Vector2(-35, -35), new Vector2(35, -35), new Vector2(-35, 35), Vector2.zero })
                {
                    FixtureCheck("preview-fixture-angle-" + fixture.steps.Count, pog.SetDiscAngles(angles.x, angles.y), "Adjust actual ready pose to " + angles);
                    for (int step = 0; step < 8; step++) yield return new WaitForFixedUpdate();
                    Vector3 delta = target.Body.position - fixture.targetBaselinePosition;
                    Quaternion qa = fixture.targetBaselineRotation.normalized, qb = target.Body.rotation.normalized;
                    var sample = new PreviewCollisionStep { pitch = angles.x, roll = angles.y, targetPosition = target.Body.position, targetRotation = target.Body.rotation,
                        targetDynamic = !target.Body.isKinematic, previewCollidersDisabled = pog.Striker.PhysicsColliders.Count > 0 && pog.Striker.PhysicsColliders.All(c => !c.enabled), playerScore = pog.PlayerScore, npcScore = pog.NpcScore, shotsUsed = pog.ShotsUsed, targetPenetrationRecoveries = target.PenetrationRecoveries,
                        maxPositionComponentDifference = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z)),
                        normalizedQuaternionComponentDifference = Mathf.Min(Mathf.Max(Mathf.Abs(qa.x - qb.x), Mathf.Abs(qa.y - qb.y), Mathf.Abs(qa.z - qb.z), Mathf.Abs(qa.w - qb.w)), Mathf.Max(Mathf.Abs(qa.x + qb.x), Mathf.Abs(qa.y + qb.y), Mathf.Abs(qa.z + qb.z), Mathf.Abs(qa.w + qb.w))) };
                    fixture.steps.Add(sample);
                    FixtureCheck("preview-fixture-no-target-push-" + fixture.steps.Count, sample.targetDynamic && sample.previewCollidersDisabled && sample.maxPositionComponentDifference <= 1e-6f && sample.normalizedQuaternionComponentDifference <= 1e-6f && sample.targetPenetrationRecoveries == fixture.baselinePenetrationRecoveries,
                        "Eight real fixed steps with overlapping preview: " + JsonUtility.ToJson(sample));
                    FixtureCheck("preview-fixture-no-score-or-turn-change-" + fixture.steps.Count, sample.playerScore == fixture.baselinePlayerScore && sample.npcScore == fixture.baselineNpcScore && sample.shotsUsed == fixture.baselineShotsUsed && pog.Phase == MatchPhase.PlayerAim, "Preparing a shot cannot consume a turn or score");
                }
            }
            finally { pog.StopMatch(); Destroy(host); File.WriteAllText(Path.Combine(output, "preview-collision-fixture.json"), JsonUtility.ToJson(fixture, true)); }
            yield return null;
            fixture.saveUnchanged = saveBefore == JsonUtility.ToJson(game.Repository.Current);
            if (!fixture.saveUnchanged) report.errors.Add("preview-fixture-save-unchanged: Dedicated collision fixture changed the natural result or collection");
            fixture.passed = fixture.targetDynamicAtBaseline && fixture.targetRestingAtBaseline && fixture.otherTargetCollidersIsolated && fixture.steps.Count == 6 && fixture.saveUnchanged; report.previewCollisionFixtureValidated = fixture.passed;
            File.WriteAllText(Path.Combine(output, "preview-collision-fixture.json"), JsonUtility.ToJson(fixture, true)); Flush();
        }
        void FixtureCheck(string name, bool passed, string detail)
        {
            report.checks.Add(new CheckResult { name = name, passed = passed, detail = detail });
            if (!passed) report.errors.Add(name + ": " + detail);
            Flush();
        }
        bool SameRestoredBoard(string beforeJson, string afterJson)
        {
            var before = JsonUtility.FromJson<PogMatch.BoardState>(beforeJson);
            var after = JsonUtility.FromJson<PogMatch.BoardState>(afterJson);
            if (before?.discs == null || after?.discs == null || before.discs.Length != after.discs.Length) return false;
            var beforePoses = before.discs; var afterPoses = after.discs;
            // 所有非姿态字段保持严格一致；只消除PhysX保存/恢复四元数时的浮点归一化尾差。
            before.discs = null; after.discs = null;
            report.resumedMetadataIdentical = JsonUtility.ToJson(before) == JsonUtility.ToJson(after);
            Check("resumed-metadata-strictly-identical", report.resumedMetadataIdentical, "All snapshot fields except disc poses match exactly, including score, counters, random state, environment and player setup");
            bool claimedIdentical = true;
            for (int i = 0; i < beforePoses.Length; i++)
            {
                var a = beforePoses[i]; var b = afterPoses[i];
                if (a == null || b == null) return false;
                claimedIdentical &= a.claimed == b.claimed;
                Vector3 delta = a.position - b.position;
                report.resumedMaxPositionComponentDifference = Mathf.Max(report.resumedMaxPositionComponentDifference, Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z)));
                Quaternion qa = a.rotation.normalized, qb = b.rotation.normalized;
                float sameSign = Mathf.Max(Mathf.Abs(qa.x - qb.x), Mathf.Abs(qa.y - qb.y), Mathf.Abs(qa.z - qb.z), Mathf.Abs(qa.w - qb.w));
                float oppositeSign = Mathf.Max(Mathf.Abs(qa.x + qb.x), Mathf.Abs(qa.y + qb.y), Mathf.Abs(qa.z + qb.z), Mathf.Abs(qa.w + qb.w));
                report.resumedMaxNormalizedQuaternionComponentDifference = Mathf.Max(report.resumedMaxNormalizedQuaternionComponentDifference, Mathf.Min(sameSign, oppositeSign));
            }
            Check("resumed-claimed-flags-identical", claimedIdentical, "Every claimed flag remains exactly equal");
            Check("resumed-position-micrometre-bound", report.resumedMaxPositionComponentDifference <= .000001f, "Measured maximum position component difference=" + report.resumedMaxPositionComponentDifference.ToString("R") + " metres");
            Check("resumed-normalized-rotation-bound", report.resumedMaxNormalizedQuaternionComponentDifference <= .000001f, "Measured maximum normalized quaternion component difference=" + report.resumedMaxNormalizedQuaternionComponentDifference.ToString("R") + "; q and -q represent the same orientation");
            return report.resumedMetadataIdentical && claimedIdentical && report.resumedMaxPositionComponentDifference <= .000001f && report.resumedMaxNormalizedQuaternionComponentDifference <= .000001f;
        }
        IEnumerator Capture(string name, bool verifyDecorations = false)
        {
            yield return Tick(); int w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            var frame = new CaptureRecord { name = name, path = Path.Combine(output, name + ".png"), screen = game.Screen.ToString(), location = game.LocationId, width = w, height = h,
                cameraAzimuth = game.World.MatchAzimuth, cameraElevation = game.World.MatchElevation, cameraDistance = game.World.MatchDistance, playerAzimuth = game.Pogs.PlayerAzimuth, tiltPitch = game.Pogs.TiltPitch, tiltRoll = game.Pogs.TiltRoll,
                launchPosition = game.Pogs.LaunchPosition, readyRotation = game.Pogs.ReadyRotation, cameraPosition = game.World.MainCamera.transform.position, cameraRotation = game.World.MainCamera.transform.rotation };
            report.captures.Add(frame);
            var rt = new RenderTexture(w, h, 24); var cam = game.World.MainCamera; var old = cam.targetTexture; var active = RenderTexture.active; Texture2D tex = null;
            try
            {
                rt.Create(); cam.targetTexture = rt; cam.Render(); string error = game.UI.RenderVerificationFrame(rt);
                Check(name + "-native-render", error == null && game.UI.VerifyInputBlocking() == null, error ?? "Native camera, UI and input blocking are valid");
                RenderTexture.active = rt; tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                var pixels = tex.GetPixels32(); var colors = new HashSet<int>();
                for (int i = 0; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 2048)) { var p = pixels[i]; colors.Add((p.r << 16) | (p.g << 8) | p.b); }
                frame.nonBlank = colors.Count > 16; Check(name + "-nonblank", frame.nonBlank, "Native sampled colors=" + colors.Count);
                File.WriteAllBytes(frame.path, tex.EncodeToPNG());
                if (verifyDecorations) { frame.decorationsChecked = true; frame.decorationsClear = VerifyDecorationVisibility(name); }
            }
            finally { cam.targetTexture = old; RenderTexture.active = active; if (tex != null) Destroy(tex); rt.Release(); Destroy(rt); }
            Flush();
        }
        VisualElement Root => game.GetComponentsInChildren<UIDocument>(true).First(d => d.panelSettings != null && d.panelSettings.sortingOrder == 20).rootVisualElement;
        Vector2 PanelToScreen(Vector2 p) => new Vector2(p.x * UnityEngine.Screen.width / Root.worldBound.width, UnityEngine.Screen.height - p.y * UnityEngine.Screen.height / Root.worldBound.height);
        static Vector3 Flat(Vector3 p) { p.y = 0; return p; }
        static bool OnScreen(Vector2 p) => p.x > 8 && p.y > 8 && p.x < UnityEngine.Screen.width - 8 && p.y < UnityEngine.Screen.height - 8;
        static bool Inside(Rect a, Rect b) => a.xMin >= b.xMin - 1 && a.yMin >= b.yMin - 1 && a.xMax <= b.xMax + 1 && a.yMax <= b.yMax + 1;
        static bool IsDisplayed(VisualElement element) { if (element == null) return false; for (var e = element; e != null; e = e.hierarchy.parent) if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden) return false; return true; }
        void ClickName(string name) { var b = Root.Q<Button>(name); Check("button-" + name, b != null && b.enabledInHierarchy && IsDisplayed(b), name); Submit(b); }
        void ClickText(string text) { var b = Root.Query<Button>().ToList().FirstOrDefault(x => x.text == text); Check("button-" + text, b != null && b.enabledInHierarchy, text); Submit(b); }
        static void Submit(Button b) { using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); } }
        IEnumerator Tick() { yield return null; yield return new WaitForSecondsRealtime(.18f); yield return null; }
        IEnumerator Wait(Func<bool> predicate, float seconds, string name) { float end = Time.realtimeSinceStartup + seconds; while (!predicate() && Time.realtimeSinceStartup < end) yield return null; Check("wait-" + name, predicate(), "Deadline=" + seconds); }
        [Serializable] sealed class BodyPose { public Vector3 position, velocity, angular; public Quaternion rotation; }
        string Bodies() => string.Join("|", FindObjectsByType<DiscBody>(FindObjectsSortMode.None).Where(d => d.gameObject.activeInHierarchy).OrderBy(d => d.GetInstanceID()).Select(d => JsonUtility.ToJson(new BodyPose { position = d.Body.position, rotation = d.Body.rotation, velocity = d.Body.linearVelocity, angular = d.Body.angularVelocity })));
        void OnShot(ShotObservation shot) { report.shots.Add(new ShotRecord { physics = shot, message = game.Pogs.Message, feedback = game.Pogs.LastFeedback }); Flush(); }
        void Check(string name, bool passed, string detail) { report.checks.Add(new CheckResult { name = name, passed = passed, detail = detail }); if (!passed) { report.errors.Add(name + ": " + detail); Flush(); throw new InvalidOperationException(name); } }
        void OnLog(string value, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(value + "\n" + stack); }
        static string Arg(string name) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < a.Length; i++) if (a[i] == name) return a[i + 1]; return null; }
        void Update() { if (!finished && Time.realtimeSinceStartup > deadline) { report.errors.Add("Native free-angle probe exceeded 480 seconds"); Complete(false); } }
        void Flush() { report.elapsedSeconds = Time.realtimeSinceStartup - started; File.WriteAllText(Path.Combine(output, "free-angle-experience-report.json"), JsonUtility.ToJson(report, true)); }
        void Complete(bool ok)
        {
            if (finished) return; finished = true; Time.timeScale = 1; report.completed = ok; report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.passed = ok && report.errors.Count == 0 && report.isolatedSave && report.aimControlsRestoredAfterOverlays && (report.uiOnly || report.fullMatchValidated && report.fourDirectionsValidated && report.realTiltValidated && report.pauseFrozen && report.resumedBoard && report.diskReadback && report.legacySetupDefaulted && report.futureSnapshotRejected && report.previewCollisionFixtureValidated && report.cameraLocationsVerified == 2 && report.decorationViewsVerified >= 20);
            Flush(); Application.Quit(report.passed ? 0 : 2);
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; if (game?.Pogs != null) game.Pogs.ShotSettled -= OnShot; }
    }
}
