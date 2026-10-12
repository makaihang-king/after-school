using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>仅在隔离存档中验收伙伴与实际对局；不初始化得分或代替正式物理结果。</summary>
    public sealed class CompanionExperienceProbe : MonoBehaviour
    {
        [Serializable] sealed class CheckResult { public string name, detail; public bool passed; }
        [Serializable] sealed class Frame { public string name, path; public int width, height; public List<CompanionSnapshot> companions; }
        [Serializable] sealed class ShotRecord { public ShotObservation physics; public string message, feedback; }
        [Serializable] sealed class Report
        {
            public bool completed, passed, isolatedSave, mobileUI, uiOnly, fullMatchValidated, pauseFrozen, resumedBoard, diskReadback, forfeitIdentity;
            public string version, engine, platform, savePath, startedUtc, finishedUtc, outcome;
            public int identitiesVerified, matchSeed, playerScore, npcScore;
            public float elapsedSeconds;
            public List<CheckResult> checks = new List<CheckResult>();
            public List<Frame> captures = new List<Frame>();
            public List<ShotRecord> shots = new List<ShotRecord>();
            public List<string> errors = new List<string>();
        }
        readonly Report report = new Report();
        SummerGame game;
        string output;
        float started, deadline;
        bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--smoke-test") && args.Contains("--companion-experience-test") && !args.Contains("--qa-manual-inspection"))
                new GameObject("伙伴体验 · 原生验收").AddComponent<CompanionExperienceProbe>();
        }
        void Awake()
        {
            started = Time.realtimeSinceStartup; deadline = started + 360;
            output = Path.GetFullPath(Arg("--qa-output") ?? Path.Combine(Application.dataPath, "../Logs/QA/Companions"));
            Directory.CreateDirectory(output);
            report.startedUtc = DateTime.UtcNow.ToString("o"); report.engine = Application.unityVersion;
            report.version = Application.version; report.platform = Application.platform.ToString();
            report.mobileUI = Environment.GetCommandLineArgs().Contains("--mobile-ui-test");
            report.uiOnly = Environment.GetCommandLineArgs().Contains("--companion-ui-only");
            Application.logMessageReceived += OnLog;
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
            yield return Wait(() => FindFirstObjectByType<SummerGame>()?.Crowd != null, 12, "startup");
            game = FindFirstObjectByType<SummerGame>(); yield return Tick();
            report.savePath = game.Repository.SavePath;
            report.isolatedSave = Arg("--qa-save") != null && Path.GetFullPath(Path.Combine(Arg("--qa-save"), "collection.json")) == report.savePath;
            Check("isolated-fresh-save", report.isolatedSave && game.Repository.Current.settlements.Count == 0, report.savePath);
            game.Pogs.ShotSettled += OnShot;
            game.ShowLobby(); game.SelectPlayMode(false, false); yield return Tick();
            var identities = new Dictionary<string, CompanionSnapshot>();
            for (int visit = 0; visit < 2; visit++)
            foreach (var npc in GameCatalog.Npcs)
            {
                if (report.mobileUI) { ClickName("mobile-lobby-step-1"); yield return Tick(); }
                if (report.mobileUI && Root.Q<Button>("opponent-" + npc.id) == null)
                {
                    ClickName(npc.id == "xiaoyu" ? "mobile-page-prev" : "mobile-page-next"); yield return Tick();
                }
                ClickName("opponent-" + npc.id);
                var before = game.Crowd.CaptureCompanions(); var a = before[0];
                yield return Tick();
                Check("selected-" + npc.id + "-" + visit, a.npcId == npc.id && !a.isOpponent && before.Select(x => x.npcId).Distinct().Count() == 4, JsonUtility.ToJson(a));
                var greeting = Root.Q<Label>("companion-greeting");
                Check("greeting-" + npc.id + "-" + visit, greeting != null && greeting.text.Contains(CompanionPersonality.Greeting(npc.id)), greeting?.text ?? "missing");
                var summary = Root.Q<ScrollView>("mobile-lobby-summary-copy");
                if (summary != null) { summary.ScrollTo(greeting); yield return Tick(); }
                Check("greeting-readable-" + npc.id + "-" + visit, greeting.resolvedStyle.fontSize >= 16 && greeting.worldBound.width > 80, greeting.worldBound.ToString());
                yield return new WaitForSeconds(.35f);
                var b = game.Crowd.CaptureCompanions()[0];
                Check("gesture-moves-" + npc.id + "-" + visit, b.gesture.EndsWith("-greeting") &&
                    (Quaternion.Angle(a.rightArmRotation, b.rightArmRotation) > 1 || Quaternion.Angle(a.headRotation, b.headRotation) > 1), JsonUtility.ToJson(b));
                if (npc.id == "linlin" || npc.id == "ayan")
                {
                    float peak = 0, end = Time.realtimeSinceStartup + 1f;
                    while (Time.realtimeSinceStartup < end) { peak = Mathf.Max(peak, Quaternion.Angle(Quaternion.identity, game.Crowd.CaptureCompanions()[0].headRotation)); yield return null; }
                    Check("nod-bounded-" + npc.id + "-" + visit, peak < 60f, "Continuously sampled head angle=" + peak);
                }
                if (visit == 0) { identities.Add(npc.id, b); report.identitiesVerified++; }
                else Check("stable-identity-" + npc.id, b.style == identities[npc.id].style && b.shirtColor == identities[npc.id].shirtColor && b.bagColor == identities[npc.id].bagColor, JsonUtility.ToJson(b));
                var actor = game.Crowd.transform.Find("街区伙伴 · 围观的小朋友/" + npc.displayName + " · 微缩儿童");
                Check("actual-model-" + npc.id + "-" + visit, actor != null && actor.GetComponentsInChildren<Collider>().Length == 0 &&
                    actor.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterial != null && r.sharedMaterial.name == npc.displayName + "的衣服" && r.sharedMaterial.color == b.shirtColor), "Live material and collider verification");
                if (visit == 0) yield return Capture("companion-" + npc.id);
            }
            // 模式切换只同步表现，不新增写盘事务。
            long revision = game.Repository.Current.revision;
            game.SelectPlayMode(true, false); yield return Tick();
            Check("solo-context", !game.Crowd.CaptureCompanions().Any(x => x.isOpponent) && Root.Q<Label>("companion-greeting") == null, "Solo mode removes the opponent greeting");
            game.SelectPlayMode(false, false); yield return Tick();
            Check("mode-does-not-write-save", game.Repository.Current.revision == revision, "Mode switches preserve revision");
            game.SelectNpc("dazhi"); game.SelectCard(game.Repository.PlayerCards.First(c => !c.isFoundation).instanceId); game.SelectPlayMode(false, true); yield return Tick();
            if (report.mobileUI)
            {
                var details = Root.Q<Button>("mobile-lobby-details"); var copy = Root.Q<ScrollView>("mobile-lobby-summary-copy"); var start = Root.Q<Button>("start-pogs");
                Check("summary-footer-contained", Inside(details.worldBound, Root.worldBound) && Inside(copy.worldBound, Root.worldBound) && copy.worldBound.yMax <= details.worldBound.yMin + 2 && details.worldBound.yMax <= start.worldBound.yMin + 2, "Scrollable copy and fixed details/start are separated");
                copy.scrollOffset = new Vector2(0, 10000); yield return Tick();
            }
            yield return Capture("lobby-dazhi-real");
            if (report.uiOnly) yield break;
            game.SelectNpc("ayue"); game.SelectedRule = RuleKind.Flip; game.SelectedPracticeLayout = PracticeLayout.Classic; game.RefreshUI(); yield return Tick();
            ClickName("start-pogs"); yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 8, "first-aim"); yield return Tick();
            report.matchSeed = game.ActiveMatch.randomSeed;
            Check("actual-opponent", game.Crowd.CaptureCompanions()[0].npcId == "ayue" && game.Crowd.CaptureCompanions()[0].isOpponent, "Ayue takes the first anchor at school gate");
            yield return Capture("match-ayue-ready");
            int shots = game.Pogs.ShotsUsed;
            var button = Root.Query<Button>().ToList().First(x => x.ClassListContains("shot-button"));
            Vector2 blocked = PanelToScreen(button.worldBound.center);
            game.Pogs.ProcessPointer(blocked, true, true, false, false); game.Pogs.ProcessPointer(blocked + Vector2.down * 80, false, false, true, false);
            Check("ui-blocks-launch", game.UI.BlocksInput(blocked) && game.Pogs.ShotsUsed == shots && !game.Pogs.IsDragging, "A real button cannot begin gameplay input");
            yield return PointerShot(Vector3.left, .28f, ShotKind.Skim);
            yield return new WaitForSeconds(.10f); game.UI.ShowPause(); yield return Tick();
            var poses = game.Crowd.CaptureCompanions(); var bodies = Bodies();
            yield return new WaitForSecondsRealtime(.4f);
            report.pauseFrozen = JsonUtility.ToJson(new SnapshotList { values = poses }) == JsonUtility.ToJson(new SnapshotList { values = game.Crowd.CaptureCompanions() }) && bodies == Bodies();
            Check("pause-freezes-bodies-and-actors", game.UI.PauseVisible && report.pauseFrozen && game.Pogs.Phase == MatchPhase.Flying, "Scaled-time motion and real rigidbodies remain frozen");
            yield return Capture("match-flight-paused"); ClickText("继续玩");
            yield return Wait(() => report.shots.Any(x => x.physics.playerControlled), 22, "first-settle");
            var miss = report.shots.First(x => x.physics.playerControlled);
            Check("quiet-real-miss", !miss.physics.scored && miss.physics.maxMovement > .01f && string.IsNullOrEmpty(miss.message) && string.IsNullOrEmpty(miss.feedback), JsonUtility.ToJson(miss));
            yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 30, "resume-aim");
            string board = game.Pogs.CaptureBoard(); string matchId = game.ActiveMatch.matchId;
            game.UI.ShowPause(); yield return Tick(); ClickText("回到街区"); yield return Tick();
            Check("pending-retained", game.Repository.HasPendingMatch && game.Repository.Current.pendingMatch.matchId == matchId, "Same wager is pending");
            var resume = Root.Query<Button>().ToList().FirstOrDefault(x => x.text.Contains("继续") && x.text.Contains("约战"));
            Check("native-resume-button", resume != null, "Hub exposes resume"); Submit(resume);
            yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 8, "restored-aim"); yield return Tick();
            report.resumedBoard = board == game.Pogs.CaptureBoard();
            Check("exact-board-and-opponent-restored", report.resumedBoard && game.ActiveMatch.matchId == matchId && game.Crowd.CaptureCompanions()[0].npcId == "ayue" && game.Crowd.CaptureCompanions()[0].isOpponent, "snapshot3 and selected opponent restored");
            yield return Capture("match-resumed");
            while (game.Screen != AppScreen.Result)
            {
                yield return Wait(() => game.Screen == AppScreen.Result || game.Pogs.Phase == MatchPhase.PlayerAim, 30, "next-turn");
                if (game.Screen == AppScreen.Result) break;
                var target = game.Pogs.Targets.Where(x => !x.Claimed).OrderBy(x => x.Body.position.z).First();
                Vector3 aim = target.Body.position; Vector3 direction = aim - new Vector3(0, 0, -2.25f); direction.y = 0;
                yield return PointerShot(direction.normalized, .38f + (game.Pogs.ShotsUsed % 3) * .04f, ShotKind.Slap);
                int count = report.shots.Count;
                yield return Wait(() => report.shots.Count > count || game.Screen == AppScreen.Result, 24, "shot-settled");
            }
            report.fullMatchValidated = game.LastSettlement != null && game.LastSettlement.matchId == matchId && !game.Repository.HasPendingMatch && report.shots.Count >= 3;
            report.outcome = game.LastSettlement.outcome.ToString();
            report.playerScore = game.Pogs.PlayerScore; report.npcScore = game.Pogs.NpcScore;
            var resultActor = game.Crowd.CaptureCompanions()[0];
            Check("natural-match-settlement", report.fullMatchValidated && resultActor.npcId == "ayue" && resultActor.gesture != "idle", JsonUtility.ToJson(game.LastSettlement));
            yield return Capture("match-real-result");
            var current = game.Repository.Current; var disk = game.Repository.Reload();
            report.diskReadback = JsonUtility.ToJson(current) == JsonUtility.ToJson(disk);
            Check("settlement-disk-readback", report.diskReadback && disk.settlements.Any(x => x.matchId == matchId), "Exact finalized save reads back from disk");
            // 首页认输按挂起局身份，不能被当前选场或练手模式替代。
            game.GoHub(); game.ShowLobby(); game.SelectNpc("ayue"); game.SelectPlayMode(false, false); game.StartPogs();
            yield return Wait(() => game.Pogs.Phase == MatchPhase.PlayerAim, 8, "forfeit-aim");
            game.GoHub(); game.SelectLocation("home"); game.Forfeit(); yield return Tick();
            report.forfeitIdentity = game.NpcId == "ayue" && !game.Training && game.Crowd.CaptureCompanions()[0].npcId == "ayue" && game.Crowd.CaptureCompanions()[0].gesture.EndsWith("npc-win");
            Check("pending-forfeit-identity", report.forfeitIdentity, "Forfeit after visiting home responds as the actual pending opponent");
            yield return Capture("pending-forfeit-result");
            game.GoHub(); game.SelectLocation("corridor"); game.StartGuidedPractice(); yield return Tick();
            Check("corridor-teaching-companion", game.Crowd.CaptureCompanions()[0].npcId == "xiaoyu" && !game.Crowd.CaptureCompanions().Any(x => x.isOpponent), "Xiaoyu teaches without an opponent or wager");
            yield return Capture("corridor-xiaoyu-teaching"); game.GoHub();
        }
        [Serializable] sealed class SnapshotList { public List<CompanionSnapshot> values; }
        IEnumerator PointerShot(Vector3 direction, float power, ShotKind kind)
        {
            game.Pogs.Shot = kind; game.Pogs.Spin = 0;
            bool planned = false;
            foreach (float z in new[] { .35f, 0f, -.35f, .7f, 1f })
            foreach (float x in new[] { 0f, -.4f, .4f, -.8f, .8f })
            {
                if (planned) continue;
                Vector3 start = new Vector3(x, 0, z), end = start - direction * (power * 2.25f);
                Vector2 a = game.World.MainCamera.WorldToScreenPoint(start), b = game.World.MainCamera.WorldToScreenPoint(end);
                if (!OnScreen(a) || !OnScreen(b) || game.UI.BlocksInput(a) || game.UI.BlocksInput(b)) continue;
                game.Pogs.ProcessPointer(a, true, true, false, false); yield return null;
                game.Pogs.ProcessPointer(b, false, true, false, false); yield return null;
                Check("pointer-power-" + game.Pogs.ShotsUsed, game.Pogs.IsDragging && Mathf.Abs(game.Pogs.Power - power) < .02f, "Production screen drag power=" + game.Pogs.Power);
                game.Pogs.ProcessPointer(b, false, false, true, false);
                Check("pointer-flight-" + game.Pogs.ShotsUsed, game.Pogs.Phase == MatchPhase.Flying, "Production release launches PhysX"); planned = true;
            }
            Check("pointer-plan", planned, "An unobstructed screen gesture was found");
        }
        IEnumerator Capture(string name)
        {
            yield return Tick(); int w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            var frame = new Frame { name = name, path = Path.Combine(output, name + ".png"), width = w, height = h, companions = game.Crowd.CaptureCompanions() }; report.captures.Add(frame);
            var rt = new RenderTexture(w, h, 24); var cam = game.World.MainCamera; var old = cam.targetTexture; var active = RenderTexture.active; Texture2D tex = null;
            try
            {
                rt.Create(); cam.targetTexture = rt; cam.Render(); string error = game.UI.RenderVerificationFrame(rt);
                Check(name + "-native-render", error == null && game.UI.VerifyInputBlocking() == null, error ?? "Native camera, UI and input are valid");
                RenderTexture.active = rt; tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); File.WriteAllBytes(frame.path, tex.EncodeToPNG());
            }
            finally { cam.targetTexture = old; RenderTexture.active = active; if (tex != null) Destroy(tex); rt.Release(); Destroy(rt); }
            Flush();
        }
        VisualElement Root => game.GetComponentsInChildren<UIDocument>(true).First(d => d.panelSettings != null && d.panelSettings.sortingOrder == 20).rootVisualElement;
        Vector2 PanelToScreen(Vector2 p) => new Vector2(p.x * UnityEngine.Screen.width / Root.worldBound.width, UnityEngine.Screen.height - p.y * UnityEngine.Screen.height / Root.worldBound.height);
        static bool OnScreen(Vector2 p) => p.x > 8 && p.y > 8 && p.x < UnityEngine.Screen.width - 8 && p.y < UnityEngine.Screen.height - 8;
        static bool Inside(Rect a, Rect b) => a.xMin >= b.xMin - 1 && a.yMin >= b.yMin - 1 && a.xMax <= b.xMax + 1 && a.yMax <= b.yMax + 1;
        void ClickName(string name) { var b = Root.Q<Button>(name); Check("button-" + name, b != null && b.enabledInHierarchy, name); Submit(b); }
        void ClickText(string text) { var b = Root.Query<Button>().ToList().FirstOrDefault(x => x.text == text); Check("button-" + text, b != null && b.enabledInHierarchy, text); Submit(b); }
        static void Submit(Button button) { using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); } }
        IEnumerator Tick() { yield return null; yield return new WaitForSecondsRealtime(.18f); yield return null; }
        IEnumerator Wait(Func<bool> predicate, float seconds, string name) { float end = Time.realtimeSinceStartup + seconds; while (!predicate() && Time.realtimeSinceStartup < end) yield return null; Check("wait-" + name, predicate(), "Deadline=" + seconds); }
        string Bodies() => string.Join("|", FindObjectsByType<DiscBody>(FindObjectsSortMode.None).Where(d => d.gameObject.activeInHierarchy).OrderBy(d => d.GetInstanceID()).Select(d => JsonUtility.ToJson(new BodyPose { position = d.Body.position, rotation = d.Body.rotation, velocity = d.Body.linearVelocity, angular = d.Body.angularVelocity })));
        [Serializable] sealed class BodyPose { public Vector3 position, velocity, angular; public Quaternion rotation; }
        void OnShot(ShotObservation shot) { report.shots.Add(new ShotRecord { physics = shot, message = game.Pogs.Message, feedback = game.Pogs.LastFeedback }); Flush(); }
        void Check(string name, bool passed, string detail) { report.checks.Add(new CheckResult { name = name, passed = passed, detail = detail }); if (!passed) { report.errors.Add(name + ": " + detail); Flush(); throw new InvalidOperationException(name); } }
        void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(text + "\n" + stack); }
        static string Arg(string name) { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
        void Update() { if (!finished && Time.realtimeSinceStartup > deadline) { report.errors.Add("Native companion probe exceeded 360 seconds"); Complete(false); } }
        void Flush() { report.elapsedSeconds = Time.realtimeSinceStartup - started; File.WriteAllText(Path.Combine(output, "companion-experience-report.json"), JsonUtility.ToJson(report, true)); }
        void Complete(bool ok) { finished = true; Time.timeScale = 1; report.completed = ok; report.finishedUtc = DateTime.UtcNow.ToString("o"); report.passed = ok && report.errors.Count == 0 && report.identitiesVerified == 7 && (report.uiOnly || report.fullMatchValidated && report.pauseFrozen && report.resumedBoard && report.diskReadback && report.forfeitIdentity); Flush(); Application.Quit(report.passed ? 0 : 2); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; if (game?.Pogs != null) game.Pogs.ShotSettled -= OnShot; }
    }
}
