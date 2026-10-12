using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>隔离存档中的原生教学验收。所有出手均走和鼠标/触屏一致的屏幕拖拽入口。</summary>
    public sealed class GuidedExperienceProbe : MonoBehaviour
    {
        [Serializable] sealed class Assertion
        {
            public string name, detail;
            public bool passed;
            public float elapsedSeconds;
        }
        [Serializable] sealed class Gesture
        {
            public string name, stage, view;
            public Vector3 aimPoint, direction, startWorld, endWorld;
            public Vector2 startScreen, endScreen;
            public float requestedPower, actualPower;
            public bool startBlocked, endBlocked, beganDragging, cancelled, released;
            public int shotsBefore, shotsAfter;
        }
        [Serializable] sealed class Attempt
        {
            public string name, stage, view, feedback, action;
            public int ordinal, shotsAtSettle, attemptsAtSettle;
            public bool stepCompleted, awaitingRetry, cancelledOnce, heldAtSettle, heldUntilAction;
            public float resultHeldSeconds;
            public Gesture gesture;
            public ShotObservation shot;
        }
        [Serializable] sealed class ElementObservation
        {
            public string name, kind, text, classes;
            public Rect bounds;
            public float fontSize;
            public bool enabled, fullyVisible;
        }
        [Serializable] sealed class Frame
        {
            public string name, path, stage, view, renderError, inputError;
            public int width, height, uniqueSampleColors;
            public bool rendered, nonBlank;
            public Rect rootBounds;
            public List<ElementObservation> elements = new List<ElementObservation>();
        }
        [Serializable] sealed class BodyState
        {
            public string name;
            public bool target, kinematic;
            public Vector3 position, linearVelocity, angularVelocity;
            public Quaternion rotation;
        }
        [Serializable] sealed class PauseObservation
        {
            public string phaseBefore, phasePaused, phaseResumed;
            public float pausedSeconds, maxPositionDelta, maxRotationDelta, maxLinearVelocityDelta, maxAngularVelocityDelta;
            public bool frozen, resumed;
            public List<BodyState> before = new List<BodyState>(), after = new List<BodyState>();
        }
        [Serializable] sealed class Report
        {
            public bool completed, passed, isolatedSave, enteredByNativeUI, clickDidNotLaunch, uiBlockedLaunch;
            public bool failureHeld, cancelRecorded, cancellationPreserved, pausedFlightFrozen, continuedFlight;
            public bool dragStagePassed, slapStagePassed, skimStagePassed, allCompleted;
            public bool walletUnchanged, cardsUnchanged, timeUnchanged, pendingUnchanged, onlyCompletionMarkerChanged, diskReadbackVerified;
            public int obliqueGestures, topDownGestures, settledShots;
            public string engine, platform, version, savePath, startedUtc, finishedUtc, saveBefore, saveAfter;
            public float elapsedSeconds;
            public List<Assertion> assertions = new List<Assertion>();
            public List<Gesture> gestures = new List<Gesture>();
            public List<Attempt> attempts = new List<Attempt>();
            public List<Frame> captures = new List<Frame>();
            public List<PauseObservation> pauses = new List<PauseObservation>();
            public List<string> actions = new List<string>(), errors = new List<string>(), diagnostics = new List<string>();
        }

        readonly Report report = new Report();
        SummerGame game;
        string output;
        float started, watchdog, nextFlush;
        bool finished;
        GameSave baseline;
        Attempt activeAttempt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (arguments.Contains("--smoke-test") && arguments.Contains("--guided-experience-test"))
                new GameObject("出手教学 · 原生验收").AddComponent<GuidedExperienceProbe>();
        }
        void Awake()
        {
            started = Time.realtimeSinceStartup; watchdog = started + 360; nextFlush = started + 15;
            output = Path.GetFullPath(Argument("--qa-output") ?? Path.Combine(Application.dataPath, "../Logs/QA/GuidedExperience"));
            Directory.CreateDirectory(output); Application.runInBackground = true;
            report.engine = Application.unityVersion; report.platform = Application.platform.ToString(); report.version = Application.version;
            report.startedUtc = DateTime.UtcNow.ToString("O"); Application.logMessageReceived += OnLog;
        }
        void OnLog(string value, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(value + "\n" + stack);
        }
        IEnumerator Start()
        {
            // 展开嵌套协程以捕获异常，避免半途中断后留下看似成功的报告。
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object current = null; bool advanced = false, failed = false;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception exception) { report.errors.Add("Guided probe exception: " + exception); Complete(false); failed = true; }
                if (failed) yield break;
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
                else yield return current;
            }
            if (!finished) Complete(true);
        }
        IEnumerator Run()
        {
            yield return WaitFor(() => FindFirstObjectByType<SummerGame>()?.UI != null, 12, "startup");
            game = FindFirstObjectByType<SummerGame>();
            yield return UiTick();
            string expectedDirectory = Path.GetFullPath(Argument("--qa-save") ?? Path.Combine(Application.dataPath, "../Logs/QA/save-v04"));
            report.savePath = Path.GetFullPath(game.Repository.SavePath);
            string normalSave = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "SummerAfterSchool-v2/collection.json"));
            report.isolatedSave = string.Equals(report.savePath, Path.Combine(expectedDirectory, "collection.json"), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(report.savePath, normalSave, StringComparison.OrdinalIgnoreCase);
            Check("isolated-save", report.isolatedSave && !game.Repository.HasPendingMatch && !game.Repository.GuidedPracticeCompleted,
                "Fresh isolated save required: " + report.savePath, true);
            baseline = game.Repository.Current; report.saveBefore = JsonUtility.ToJson(baseline);
            yield return Capture("00-guided-hub");
            ClickNamed("open-guided-practice"); yield return WaitForAim(); yield return new WaitForSecondsRealtime(1.2f);
            report.enteredByNativeUI = game.Screen == AppScreen.Pogs && game.GuidedPracticeMode && game.GuidedPractice != null && !game.Repository.HasPendingMatch;
            Check("native-ui-entry", report.enteredByNativeUI, "open-guided-practice enters Pogs without pending match", true);
            Check("manual-step-progression", !game.GuidedPractice.AutoAdvance, "Tutorial requires the result action before advancing", true);
            game.Pogs.ShotSettled += OnSettled; game.Pogs.PhysicsDiagnostic += OnDiagnostic;
            yield return Capture("01-guided-drag-ready");

            // 先验收点击及 UI 阻挡，再故意未取消出手，留下真实失败盘面。
            yield return VerifyClickAndBlock();
            yield return Shoot("drag-before-cancel-failure", new Vector3(-1, 0, .12f).normalized, .12f, false);
            Check("uncancelled-shot-fails", game.GuidedPractice.Progress.awaitingRetry && !game.GuidedPractice.StepComplete,
                "The real shot without prior cancellation must wait for retry", true);
            report.failureHeld = activeAttempt.heldUntilAction;
            yield return ClickResultAction(false);

            // 两种相机模式各三次真实屏幕蓄力/取消，逐次比较世界计划与实际读到的力道。
            for (int mode = 0; mode < 2; mode++)
            {
                if (game.World.IsTopDownMatchView != (mode == 1)) { ClickNamed("match-view-toggle"); yield return new WaitForSecondsRealtime(1.2f); }
                for (int sample = 0; sample < 3; sample++)
                {
                    float power = .22f + sample * .11f;
                    var direction = new Vector3((sample - 1) * .24f, 0, 1).normalized;
                    var gesture = Plan("mapping-" + mode + "-" + sample, direction, power);
                    BeginGesture(gesture); yield return null;
                    HoldGesture(gesture); yield return null;
                    Check(gesture.name + "-power", Mathf.Abs(game.Pogs.Power - power) < .018f,
                        "requested=" + power + " actual=" + game.Pogs.Power, true);
                    game.Pogs.ProcessPointer(gesture.endScreen, false, false, false, true);
                    gesture.actualPower = power; gesture.cancelled = true; gesture.shotsAfter = game.Pogs.ShotsUsed;
                    Check(gesture.name + "-cancel", !game.Pogs.IsDragging && gesture.shotsBefore == gesture.shotsAfter,
                        "Cancelled screen drag leaves shots unchanged", true);
                    if (mode == 0) report.obliqueGestures++; else report.topDownGestures++;
                }
                yield return Capture(mode == 0 ? "02-guided-oblique-aim" : "03-guided-topdown-aim");
            }
            report.cancelRecorded = game.GuidedPractice.Progress.cancelledOnce;
            Check("cancel-recorded", report.cancelRecorded, "AimCancelled observed by the real tutorial", true);
            // 一次正常拖拽出手；在实际飞行中点击暂停，验证刚体位置和两种速度冻结。
            yield return Shoot("drag-after-cancel-success", Vector3.forward, .35f, true);
            report.dragStagePassed = game.GuidedPractice.StepComplete && game.GuidedPractice.Step == GuidedPracticeStep.DragAndCancel;
            report.cancellationPreserved = game.GuidedPractice.Progress.cancelledOnce;
            Check("drag-cancel-completed", report.dragStagePassed && report.cancellationPreserved,
                "The cancellation remains recorded through the real settled drag shot", true);
            yield return ClickResultAction(true);
            Check("slap-step-entered", game.GuidedPractice.Step == GuidedPracticeStep.SlapFlip, "Native continue action enters step 2", true);
            yield return Capture("05-guided-slap-ready");

            ClickShot(ShotKind.Slap);
            yield return Shoot("slap-low-power-failure", AimDirection(game.Pogs.GuidedAimPoint), .12f, false);
            Check("slap-low-power-fails", game.GuidedPractice.Progress.awaitingRetry && !game.GuidedPractice.StepComplete,
                "A real weak slap leaves its failed result available for retry", true);
            yield return ClickResultAction(false);
            // 仅改变真实拖拽的落点与距离。不移动片、不恢复伪造盘面、不修改物理参数或计分。
            float[] offsets = { 0, -.10f, .10f, -.18f, .18f, 0, -.25f, .25f, -.08f, .08f, 0, -.15f };
            float[] powerOffsets = { 0, 0, 0, -.035f, -.035f, .04f, 0, 0, .055f, .055f, -.065f, .025f };
            for (int trial = 0; trial < offsets.Length && !game.GuidedPractice.StepComplete; trial++)
            {
                if (game.World.IsTopDownMatchView != (trial % 2 == 0)) { ClickNamed("match-view-toggle"); yield return new WaitForSecondsRealtime(1.1f); }
                Vector3 aimPoint = game.Pogs.GuidedAimPoint + Vector3.right * offsets[trial];
                float power = Mathf.Clamp(game.Pogs.GuidedSuggestedPower + powerOffsets[trial], .26f, .77f);
                ClickShot(ShotKind.Slap);
                yield return Shoot("slap-physical-trial-" + (trial + 1).ToString("00"), AimDirection(aimPoint), power, false, aimPoint);
                if (!game.GuidedPractice.StepComplete) yield return ClickResultAction(false);
            }
            report.slapStagePassed = game.GuidedPractice.StepComplete && game.GuidedPractice.Step == GuidedPracticeStep.SlapFlip &&
                activeAttempt.shot != null && activeAttempt.shot.flipped && activeAttempt.shot.normalizedPower >= .25f && activeAttempt.shot.normalizedPower <= .78f;
            Check("real-medium-slap-flip", report.slapStagePassed, "Medium slap must produce a settled PhysX flip", true);
            yield return ClickResultAction(true);
            Check("skim-step-entered", game.GuidedPractice.Step == GuidedPracticeStep.SkimRingOut, "Native continue action enters step 3", true);
            yield return Capture("06-guided-skim-ready");
            for (int trial = 0; trial < 6 && !game.GuidedPractice.StepComplete; trial++)
            {
                if (game.World.IsTopDownMatchView != (trial % 2 == 0)) { ClickNamed("match-view-toggle"); yield return new WaitForSecondsRealtime(1.1f); }
                Vector3 aimPoint = game.Pogs.GuidedAimPoint + Vector3.right * (trial == 0 ? 0 : trial % 2 == 0 ? .055f : -.055f);
                ClickShot(ShotKind.Skim);
                yield return Shoot("skim-physical-trial-" + (trial + 1).ToString("00"), AimDirection(aimPoint), Mathf.Clamp01(game.Pogs.GuidedSuggestedPower + trial * .025f), false, aimPoint);
                if (!game.GuidedPractice.StepComplete) yield return ClickResultAction(false);
            }
            report.skimStagePassed = game.GuidedPractice.StepComplete && game.GuidedPractice.Step == GuidedPracticeStep.SkimRingOut && activeAttempt.shot != null && activeAttempt.shot.ringOut;
            Check("real-complete-ring-out", report.skimStagePassed, "Skim must settle with the whole target outside the ring", true);
            yield return Capture("07-guided-three-steps-result");
            yield return ClickResultAction(true);
            report.allCompleted = game.GuidedPractice.StepComplete && game.GuidedPractice.Step == GuidedPracticeStep.SkimRingOut &&
                game.Repository.GuidedPracticeCompleted && game.Screen == AppScreen.Hub && !game.GuidedPracticeMode;
            Check("three-steps-completed", report.allCompleted, "Real third-step ShotObservation saves completion and its native result action returns to the hub", true);
            yield return UiTick(); yield return Capture("08-guided-completed");
            VerifySave();
        }

        IEnumerator VerifyClickAndBlock()
        {
            var click = Plan("click-without-pull", Vector3.forward, .25f);
            int before = game.Pogs.ShotsUsed;
            game.Pogs.ProcessPointer(click.startScreen, true, true, false, false);
            click.beganDragging = game.Pogs.IsDragging;
            game.Pogs.ProcessPointer(click.startScreen, false, false, true, false);
            click.actualPower = game.Pogs.Power; click.released = true; click.shotsAfter = game.Pogs.ShotsUsed;
            report.clickDidNotLaunch = click.beganDragging && !game.Pogs.IsDragging && game.Pogs.ShotsUsed == before && game.Pogs.Phase == MatchPhase.PlayerAim;
            Check("click-does-not-shoot", report.clickDidNotLaunch, "Screen click without a pull does not enter flight", true);
            var button = Buttons().FirstOrDefault(item => item.ClassListContains("shot-button") && item.enabledInHierarchy);
            Check("ui-block-button-present", button != null, "Native shot button is present", true);
            Vector2 blocked = ScreenPoint(button.worldBound.center);
            var entry = new Gesture { name = "ui-button-blocked-drag", stage = Stage, view = View, startScreen = blocked,
                endScreen = blocked + Vector2.down * 80, startBlocked = game.UI.BlocksInput(blocked), shotsBefore = before };
            report.gestures.Add(entry);
            game.Pogs.ProcessPointer(entry.startScreen, true, true, false, false); entry.beganDragging = game.Pogs.IsDragging;
            game.Pogs.ProcessPointer(entry.endScreen, false, true, false, false);
            game.Pogs.ProcessPointer(entry.endScreen, false, false, true, false);
            entry.released = true; entry.shotsAfter = game.Pogs.ShotsUsed;
            report.uiBlockedLaunch = entry.startBlocked && !entry.beganDragging && entry.shotsAfter == before;
            Check("native-ui-blocks-shot", report.uiBlockedLaunch, "Dragging from a native UI button never starts arena aiming", true);
            yield return null;
        }
        IEnumerator Shoot(string name, Vector3 direction, float power, bool pause, Vector3 aimPoint = default)
        {
            yield return WaitForAim();
            var gesture = Plan(name, direction, power, aimPoint);
            activeAttempt = new Attempt { name = name, stage = Stage, view = View, ordinal = report.attempts.Count + 1, gesture = gesture };
            report.attempts.Add(activeAttempt);
            BeginGesture(gesture); yield return null; HoldGesture(gesture); yield return null;
            gesture.actualPower = game.Pogs.Power;
            Check(name + "-screen-power", Mathf.Abs(gesture.actualPower - power) < .018f, "requested=" + power + " actual=" + gesture.actualPower, true);
            game.Pogs.ProcessPointer(gesture.endScreen, false, false, true, false);
            gesture.released = true; gesture.shotsAfter = game.Pogs.ShotsUsed;
            Check(name + "-real-flight", game.Pogs.Phase == MatchPhase.Flying && gesture.shotsAfter == gesture.shotsBefore + 1,
                "Production pointer release starts exactly one real PhysX shot", true);
            if (pause) { yield return new WaitForSecondsRealtime(.10f); yield return VerifyPause(); }
            yield return WaitFor(() => activeAttempt.shot != null && game.Pogs.Phase != MatchPhase.Flying, 22, name + " settle");
            yield return UiTick();
            activeAttempt.feedback = game.GuidedPractice.Progress.feedback;
            activeAttempt.stepCompleted = game.GuidedPractice.StepComplete; activeAttempt.awaitingRetry = game.GuidedPractice.Progress.awaitingRetry;
            activeAttempt.cancelledOnce = game.GuidedPractice.Progress.cancelledOnce; activeAttempt.attemptsAtSettle = game.GuidedPractice.Progress.attempts;
            activeAttempt.heldAtSettle = game.Pogs.GuidedResultHeld; activeAttempt.shotsAtSettle = game.Pogs.ShotsUsed;
            float heldStarted = Time.realtimeSinceStartup;
            yield return Capture(name + "-settled");
            yield return new WaitForSecondsRealtime(.65f);
            activeAttempt.resultHeldSeconds = Time.realtimeSinceStartup - heldStarted;
            activeAttempt.heldUntilAction = game.Pogs.GuidedResultHeld && game.Pogs.Phase == MatchPhase.Waiting &&
                game.Pogs.ShotsUsed == activeAttempt.shotsAtSettle && Stage == activeAttempt.stage;
            Check(name + "-result-stays", activeAttempt.heldAtSettle && activeAttempt.heldUntilAction,
                "Settled native result remains in Waiting until guided-action", true);
            Check(name + "-actual-observation", activeAttempt.shot.playerControlled && activeAttempt.shot.maxMovement > .03f,
                "Physics movement=" + activeAttempt.shot.maxMovement + " targetContacts=" + activeAttempt.shot.targetCollisionCount, true);
        }
        IEnumerator ClickResultAction(bool continueStep)
        {
            var action = Buttons().FirstOrDefault(button => button.name == "guided-action");
            Check("result-action-present-" + report.attempts.Count, action != null && action.enabledInHierarchy && FullyVisible(action.worldBound, MainRoot.worldBound),
                "Native guided-action is visible and enabled", true);
            if (activeAttempt != null) activeAttempt.action = action.text;
            var stage = game.GuidedPractice.Step; bool cancelled = game.GuidedPractice.Progress.cancelledOnce;
            Click(action); yield return UiTick();
            if (!continueStep)
            {
                yield return WaitForAim();
                Check("retry-keeps-stage-" + report.attempts.Count, game.GuidedPractice.Step == stage && !game.GuidedPractice.Progress.awaitingRetry,
                    "Retry rebuilds only the failed stage", true);
                Check("retry-keeps-cancellation-" + report.attempts.Count, !cancelled || game.GuidedPractice.Progress.cancelledOnce,
                    "Retry preserves the action already learned", true);
            }
            else if (stage != GuidedPracticeStep.SkimRingOut) yield return WaitForAim();
        }
        Gesture Plan(string name, Vector3 direction, float power, Vector3 aimPoint = default)
        {
            direction.y = 0; direction.Normalize();
            var entry = new Gesture { name = name, stage = Stage, view = View, aimPoint = aimPoint, direction = direction,
                requestedPower = power, shotsBefore = game.Pogs.ShotsUsed };
            // 选空地作为手势起点，手势决定方向而不是把物理出手片搬过去。
            foreach (float z in new[] { .35f, 0f, -.35f, .70f, 1f })
            foreach (float x in new[] { 0f, -.40f, .40f, -.80f, .80f })
            {
                Vector3 start = new Vector3(x, 0, z), end = start - direction * (power * 2.25f);
                Vector3 startProjected = game.World.MainCamera.WorldToScreenPoint(start), endProjected = game.World.MainCamera.WorldToScreenPoint(end);
                Vector2 a = startProjected, b = endProjected;
                if (startProjected.z <= 0 || endProjected.z <= 0 || !InViewport(a) || !InViewport(b) || game.UI.BlocksInput(a) || game.UI.BlocksInput(b)) continue;
                entry.startWorld = start; entry.endWorld = end; entry.startScreen = a; entry.endScreen = b;
                entry.startBlocked = game.UI.BlocksInput(a); entry.endBlocked = game.UI.BlocksInput(b);
                report.gestures.Add(entry); return entry;
            }
            throw new InvalidOperationException("No unobstructed native screen drag for " + name + " power=" + power + " view=" + View);
        }
        void BeginGesture(Gesture entry)
        {
            game.Pogs.ProcessPointer(entry.startScreen, true, true, false, false); entry.beganDragging = game.Pogs.IsDragging;
            Check(entry.name + "-unblocked-start", entry.beganDragging && !entry.startBlocked && !entry.endBlocked,
                "start=" + entry.startScreen + " end=" + entry.endScreen + " view=" + entry.view, true);
        }
        void HoldGesture(Gesture entry) { game.Pogs.ProcessPointer(entry.endScreen, false, true, false, false); entry.actualPower = game.Pogs.Power; }
        Vector3 AimDirection(Vector3 target) { Vector3 delta = target - new Vector3(0, 0, -2.25f); delta.y = 0; return delta.normalized; }
        void OnSettled(ShotObservation shot) { report.settledShots++; if (activeAttempt != null) activeAttempt.shot = shot; }
        void OnDiagnostic(string detail) { report.diagnostics.Add(detail); }

        IEnumerator VerifyPause()
        {
            var pause = new PauseObservation { phaseBefore = game.Pogs.Phase.ToString() }; report.pauses.Add(pause);
            Check("pause-starts-in-flight", game.Pogs.Phase == MatchPhase.Flying, "Pause operates during actual PhysX flight", true);
            ClickText("歇一会儿"); pause.phasePaused = game.Pogs.Phase.ToString(); pause.before = Bodies();
            Check("native-pause-stops-time", game.UI.PauseVisible && Time.timeScale == 0, "Native pause button sets timeScale=0", true);
            float time = Time.realtimeSinceStartup; yield return new WaitForSecondsRealtime(.55f);
            pause.after = Bodies(); pause.pausedSeconds = Time.realtimeSinceStartup - time;
            foreach (var a in pause.before)
            {
                var b = pause.after.FirstOrDefault(body => body.name == a.name && body.target == a.target);
                Check("paused-body-present-" + a.name, b != null, "Paused body remains alive", true);
                pause.maxPositionDelta = Mathf.Max(pause.maxPositionDelta, Vector3.Distance(a.position, b.position));
                pause.maxRotationDelta = Mathf.Max(pause.maxRotationDelta, Quaternion.Angle(a.rotation, b.rotation));
                pause.maxLinearVelocityDelta = Mathf.Max(pause.maxLinearVelocityDelta, Vector3.Distance(a.linearVelocity, b.linearVelocity));
                pause.maxAngularVelocityDelta = Mathf.Max(pause.maxAngularVelocityDelta, Vector3.Distance(a.angularVelocity, b.angularVelocity));
            }
            pause.frozen = pause.maxPositionDelta < .00001f && pause.maxRotationDelta < .001f && pause.maxLinearVelocityDelta < .00001f && pause.maxAngularVelocityDelta < .00001f;
            report.pausedFlightFrozen = pause.frozen;
            Check("paused-flight-frozen", pause.frozen, JsonUtility.ToJson(pause), true);
            yield return Capture("04-guided-flight-paused");
            ClickText("继续玩"); pause.phaseResumed = game.Pogs.Phase.ToString();
            pause.resumed = !game.UI.PauseVisible && Time.timeScale > 0 && game.Pogs.Phase == MatchPhase.Flying;
            report.continuedFlight = pause.resumed; Check("native-resume-same-flight", pause.resumed, "Resume keeps the same shot in Flying", true);
        }
        List<BodyState> Bodies()
        {
            return FindObjectsByType<DiscBody>(FindObjectsSortMode.None).Where(d => d.Body != null && d.gameObject.activeInHierarchy)
                .Select(d => new BodyState { name = d.name, target = d.IsTarget, kinematic = d.Body.isKinematic, position = d.Body.position,
                    rotation = d.Body.rotation, linearVelocity = d.Body.linearVelocity, angularVelocity = d.Body.angularVelocity }).ToList();
        }
        void VerifySave()
        {
            var after = game.Repository.Current; report.saveAfter = JsonUtility.ToJson(after);
            report.walletUnchanged = baseline.glassMarbles == after.glassMarbles;
            report.cardsUnchanged = JsonUtility.ToJson(new CardList { cards = baseline.cards }) == JsonUtility.ToJson(new CardList { cards = after.cards });
            report.timeUnchanged = baseline.progress.day == after.progress.day && baseline.progress.period == after.progress.period;
            report.pendingUnchanged = JsonUtility.ToJson(baseline.pendingMatch) == JsonUtility.ToJson(after.pendingMatch);
            report.onlyCompletionMarkerChanged = SaveWithoutMarker(baseline) == SaveWithoutMarker(after);
            report.diskReadbackVerified = report.saveAfter == JsonUtility.ToJson(game.Repository.Reload()) && game.Repository.GuidedPracticeCompleted;
            Check("save-wallet-and-cards", report.walletUnchanged && report.cardsUnchanged, "Complete tutorial preserves wallet and every collectible instance", true);
            Check("save-time-and-pending", report.timeUnchanged && report.pendingUnchanged, "Complete tutorial preserves day/period and pending state", true);
            Check("save-only-completion-marker", report.onlyCompletionMarkerChanged, "All save fields except completion marker/revision/update metadata match", true);
            Check("save-disk-readback", report.diskReadbackVerified, "Repository.Reload reads the exact completed save from disk", true);
        }
        [Serializable] sealed class CardList { public List<CardInstance> cards; }
        static string SaveWithoutMarker(GameSave value)
        {
            var copy = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(value));
            copy.revision = 0; copy.updatedUtc = ""; copy.progress.guidedPracticeCompleted = false; return JsonUtility.ToJson(copy);
        }
        IEnumerator Capture(string name)
        {
            yield return null;
            int width = UnityEngine.Screen.width, height = UnityEngine.Screen.height;
            var frame = new Frame { name = name, path = Path.Combine(output, name + ".png"), stage = Stage, view = View,
                width = width, height = height, rootBounds = MainRoot.worldBound };
            report.captures.Add(frame);
            Check(name + "-viewport", width > 1 && height > 1, "Native viewport " + width + "x" + height, true);
            foreach (var element in MainRoot.Query<TextElement>().ToList().Where(e => e.visible && e.worldBound.width > 0 && e.worldBound.height > 0))
                frame.elements.Add(new ElementObservation { name = element.name, kind = element.GetType().Name, text = element.text,
                    classes = string.Join(",", element.GetClasses()), bounds = element.worldBound, fontSize = element.resolvedStyle.fontSize,
                    enabled = element.enabledInHierarchy, fullyVisible = FullyVisible(element.worldBound, MainRoot.worldBound) });
            if (game.GuidedPracticeMode)
            {
                string feedback = game.GuidedPractice.Progress.feedback;
                var prompt = frame.elements.FirstOrDefault(e => e.text == feedback || e.text == feedback.Replace("右键取消一次", "点“取消蓄力”一次"));
                Check(name + "-readable-guide", prompt != null && prompt.fullyVisible && prompt.fontSize >= 16,
                    "Native prompt=" + feedback + " found=" + (prompt != null), true);
            }
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var camera = game.World.MainCamera; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                target.Create(); camera.targetTexture = target; camera.Render();
                frame.renderError = game.UI.RenderVerificationFrame(target); frame.inputError = game.UI.VerifyInputBlocking();
                RenderTexture.active = target; texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32(); var colors = new HashSet<int>();
                for (int i = 0; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 2048))
                { var pixel = pixels[i]; if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) colors.Add((pixel.r << 16) | (pixel.g << 8) | pixel.b); }
                frame.uniqueSampleColors = colors.Count; frame.nonBlank = colors.Count > 16; frame.rendered = frame.renderError == null;
                File.WriteAllBytes(frame.path, texture.EncodeToPNG());
                Check(name + "-native-render", frame.rendered && frame.nonBlank, "UI render=" + frame.renderError + " colors=" + colors.Count, true);
                Check(name + "-input-blocks", frame.inputError == null, "UI input=" + frame.inputError, true);
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; if (texture != null) Destroy(texture); target.Release(); Destroy(target); }
        }
        VisualElement MainRoot => game.GetComponentsInChildren<UIDocument>(true).First(document => document.panelSettings != null && document.panelSettings.sortingOrder == 20).rootVisualElement;
        string Stage => game != null && game.GuidedPractice != null ? game.GuidedPractice.Step.ToString() : game?.Screen.ToString() ?? "startup";
        string View => game != null && game.World.IsTopDownMatchView ? "top-down" : "oblique";
        List<Button> Buttons() => MainRoot.Query<Button>().ToList().Where(button => button.visible && button.panel != null && button.worldBound.width > 1 && button.worldBound.height > 1).ToList();
        void ClickNamed(string name) { var button = Buttons().FirstOrDefault(b => b.name == name && b.enabledInHierarchy); Check("button-" + name, button != null, "Native button=" + name, true); Click(button); }
        void ClickText(string text) { var button = Buttons().FirstOrDefault(b => b.text == text && b.enabledInHierarchy); Check("button-" + text, button != null, "Native button text=" + text, true); Click(button); }
        void ClickShot(ShotKind kind)
        {
            var button = Buttons().FirstOrDefault(b => b.enabledInHierarchy && b.ClassListContains("shot-button") && b.text.Contains(GameCatalog.ShotName(kind)));
            Check("shot-button-" + kind, button != null, "Native shot selector=" + kind, true); Click(button);
            Check("shot-selected-" + kind, game.Pogs.Shot == kind, "UI selected=" + game.Pogs.Shot, true);
        }
        void Click(Button button)
        {
            report.actions.Add(Stage + " / " + View + " / NavigationSubmitEvent name=" + button.name + " text=" + button.text);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        Vector2 ScreenPoint(Vector2 panel) => new Vector2(panel.x * UnityEngine.Screen.width / MainRoot.worldBound.width,
            UnityEngine.Screen.height - panel.y * UnityEngine.Screen.height / MainRoot.worldBound.height);
        static bool FullyVisible(Rect element, Rect viewport) => element.xMin >= viewport.xMin - 1 && element.yMin >= viewport.yMin - 1 && element.xMax <= viewport.xMax + 1 && element.yMax <= viewport.yMax + 1;
        static bool InViewport(Vector2 point) => point.x > 8 && point.y > 8 && point.x < UnityEngine.Screen.width - 8 && point.y < UnityEngine.Screen.height - 8;
        IEnumerator UiTick() { yield return null; yield return new WaitForSecondsRealtime(.22f); yield return null; }
        IEnumerator WaitForAim() { yield return WaitFor(() => game.Screen == AppScreen.Pogs && game.Pogs.Phase == MatchPhase.PlayerAim, 8, "PlayerAim"); yield return UiTick(); }
        IEnumerator WaitFor(Func<bool> predicate, float seconds, string name)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Check("wait-" + name, predicate(), "Deadline=" + seconds + " seconds", true);
        }
        void Check(string name, bool passed, string detail, bool fatal = false)
        {
            report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail, elapsedSeconds = Time.realtimeSinceStartup - started });
            if (passed) return;
            report.errors.Add(name + ": " + detail); Flush();
            if (fatal) throw new InvalidOperationException(name + ": " + detail);
        }
        static string Argument(string name) { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
        void Update()
        {
            if (finished) return;
            if (Time.realtimeSinceStartup > watchdog) { report.errors.Add("Guided real-time watchdog reached 360 seconds; unfinished steps are failures."); Complete(false); }
            else if (Time.realtimeSinceStartup > nextFlush) { nextFlush = Time.realtimeSinceStartup + 15; Flush(); }
        }
        void Flush() { report.elapsedSeconds = Time.realtimeSinceStartup - started; File.WriteAllText(Path.Combine(output, "guided-experience-report.json"), JsonUtility.ToJson(report, true)); }
        void Complete(bool completed)
        {
            if (finished) return; finished = true; Time.timeScale = 1;
            report.completed = completed; report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.passed = completed && report.errors.Count == 0 && report.isolatedSave && report.enteredByNativeUI && report.clickDidNotLaunch && report.uiBlockedLaunch &&
                report.failureHeld && report.cancelRecorded && report.cancellationPreserved && report.pausedFlightFrozen && report.continuedFlight &&
                report.obliqueGestures >= 3 && report.topDownGestures >= 3 && report.dragStagePassed && report.slapStagePassed && report.skimStagePassed && report.allCompleted &&
                report.onlyCompletionMarkerChanged && report.diskReadbackVerified;
            Flush(); Debug.Log("GUIDED_EXPERIENCE " + (report.passed ? "PASS" : "FAIL") + " report=" + Path.Combine(output, "guided-experience-report.json"));
            Application.Quit(report.passed ? 0 : 2);
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (game?.Pogs != null) { game.Pogs.ShotSettled -= OnSettled; game.Pogs.PhysicsDiagnostic -= OnDiagnostic; }
        }
    }
}
