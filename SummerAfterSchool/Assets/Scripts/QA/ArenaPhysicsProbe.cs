using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Childhood
{
    /// <summary>仅 --arena-test 启用：独立场景与玩法组件，记录每个真实 PhysX 步骤。</summary>
    public sealed class ArenaPhysicsProbe : MonoBehaviour
    {
        const float PenetrationLimit = .006f, BoundaryLimit = .012f, BelowSurfaceLimit = .006f;
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        [Serializable] sealed class Report
        {
            public bool completed, passed, standalone, saveFilesUnchanged;
            public string engine, frameLog;
            public float fixedDeltaTime, penetrationLimit, boundaryLimit, belowSurfaceLimit;
            public int locationsVerified, environmentCasesVerified, thinDiscsVerified, thickDiscsVerified, physicsFrames, bodySamples, pogShots, marbleShots, scoringFixtures;
            public List<CaseReport> cases = new List<CaseReport>();
            public List<string> errors = new List<string>(), observations = new List<string>();
        }
        [Serializable] sealed class CaseReport
        {
            public string location, scenario, deepestGroundCollider;
            public bool passed, usedSafetyRecovery;
            public bool settledAtFourSeconds, continuedNaturalTail;
            public int samples, contacts, boundaryRecoveries, penetrationRecoveries;
            public float simulatedSeconds, maxGroundPenetration, maxBodyPenetration, maxBoundaryExcess;
            public float minCenterClearance = float.MaxValue, endSpeed, endAngularSpeed;
            public float naturallySettledAtSeconds, tailStableSeconds;
            public List<BodyState> fourSecondBodies, finalBodies;
        }
        [Serializable] sealed class BodyState
        {
            public string body;
            public Vector3 position, velocity, angularVelocity;
            public Quaternion rotation;
            public float linearSpeed, angularSpeed, upDot;
            public bool sleeping;
            public int boundaryRecoveries, penetrationRecoveries;
        }
        readonly Report report = new Report();
        readonly HashSet<string> emittedFailures = new HashSet<string>(), terrainFeatures = new HashSet<string>();
        readonly Dictionary<string, string> saveBefore = new Dictionary<string, string>();
        string output;
        StreamWriter frames;
        SceneWorld world;
        SummerAudio audioFx;
        PogMatch pog;
        MarbleMatch marbles;
        Transform looseBodies;
        float watchdog;
        int spawnSerial;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Environment.GetCommandLineArgs().Contains("--arena-test"))
                new GameObject("QA · 场地真实物理验证").AddComponent<ArenaPhysicsProbe>();
        }
        void Awake()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/Arena"));
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == "--qa-output") output = Path.GetFullPath(arguments[i + 1]);
            Directory.CreateDirectory(output);
            report.engine = Application.unityVersion;
            report.penetrationLimit = PenetrationLimit; report.boundaryLimit = BoundaryLimit; report.belowSurfaceLimit = BelowSurfaceLimit;
            report.frameLog = "arena-frames.csv";
            frames = new StreamWriter(Path.Combine(output, report.frameLog), false, System.Text.Encoding.UTF8);
            frames.WriteLine("location,scenario,fixedTime,body,x,y,z,qx,qy,qz,qw,upDot,vx,vy,vz,wx,wy,wz,groundDepth,groundCollider,bodyDepth,centerClearance,boundaryExcess,boundaryRecoveries,penetrationRecoveries");
            Application.logMessageReceived += OnLog;
            Time.timeScale = 4; Time.fixedDeltaTime = .005f;
            Physics.defaultSolverIterations = 16; Physics.defaultSolverVelocityIterations = 10;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
            report.fixedDeltaTime = Time.fixedDeltaTime;
            watchdog = Time.realtimeSinceStartup + 900;
            SnapshotSaveFiles(saveBefore);
            var fixedLoop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop().subSystemList.FirstOrDefault(system => system.type == typeof(UnityEngine.PlayerLoop.FixedUpdate));
            if (fixedLoop.subSystemList != null)
                report.observations.Add("Actual Unity FixedUpdate loop: " + string.Join(" > ", fixedLoop.subSystemList.Select(system => system.type == null ? "(native)" : system.type.Name)));
        }
        IEnumerator Start()
        {
            // 显式驱动嵌套迭代器，异常也会落盘，不能把未运行完的测试写成通过。
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object current = null; bool advanced = false, failed = false;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception exception) { report.errors.Add("Probe exception: " + exception); Complete(false); failed = true; }
                if (failed) yield break;
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
                else yield return current;
            }
            if (!finished) Complete(true);
        }
        IEnumerator Run()
        {
            yield return null;
            report.standalone = FindFirstObjectByType<SummerGame>() == null;
            if (!report.standalone)
            {
                report.errors.Add("--arena-test unexpectedly started SummerGame; user-save isolation cannot be guaranteed.");
                yield break;
            }
            audioFx = new GameObject("QA · 独立声景").AddComponent<SummerAudio>();
            world = new GameObject("QA · 独立场景").AddComponent<SceneWorld>();
            pog = new GameObject("QA · 独立打片").AddComponent<PogMatch>(); pog.Configure(world, audioFx);
            pog.PhysicsDiagnostic += value => report.observations.Add(value);
            marbles = new GameObject("QA · 独立玻璃球").AddComponent<MarbleMatch>(); marbles.Configure(world, audioFx);
            marbles.PhysicsDiagnostic += value => report.observations.Add(value);
            looseBodies = new GameObject("QA · 散片测试").transform;
            report.observations.Add("Locations are taken from GameCatalog; home is 外婆家的小院. No SaveRepository, match coordinator or score persistence is instantiated.");
            foreach (var location in GameCatalog.Locations)
            {
                int errorsBefore = report.errors.Count;
                foreach (WeatherKind weather in new[] { WeatherKind.Sunny, WeatherKind.Rainy })
                {
                int environmentErrors = report.errors.Count;
                string scenarioLocation = location.id + "-" + weather;
                world.BuildWorld(new EnvironmentContext { locationId = location.id, visitVariant = 0, day = 1, period = DayPeriod.Afternoon,
                    weather = weather, weatherSeed = 197, geometryVersion = 2,
                    frictionMultiplier = weather == WeatherKind.Rainy && location.id != "shop" && location.id != "corridor" ? .9f : 1f });
                world.SetPogTerrainEnabled(true); world.SetCameraMode(WorldCameraMode.Match, true);
                audioFx.SetContext(location.id, AudioMood.Match); Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                if (world.Terrain == null) { report.errors.Add(location.id + ": no ArenaTerrain was built."); continue; }
                SurveyTerrain(scenarioLocation);
                yield return Capture(scenarioLocation + "-terrain");
                yield return VerifyDrops(scenarioLocation);
                yield return VerifyStack(scenarioLocation);
                yield return VerifyBoundaries(scenarioLocation);
                yield return VerifyFriction(scenarioLocation);
                yield return VerifyPogShot(scenarioLocation, RuleKind.Flip, ShotKind.Slap);
                yield return VerifyPogShot(scenarioLocation, RuleKind.RingOut, ShotKind.Skim);
                if (location.id == "old_tree") yield return VerifyPogShot(scenarioLocation, RuleKind.Flip, ShotKind.Smash);
                yield return VerifyScoreFixture(scenarioLocation, RuleKind.Flip);
                yield return VerifyScoreFixture(scenarioLocation, RuleKind.RingOut);
                // 玻璃球只保留 --legacy-marble-physics 下的独立物理回归，不是 0.4 产品入口。
                if (Environment.GetCommandLineArgs().Contains("--legacy-marble-physics")) yield return VerifyMarbles(scenarioLocation);
                if (report.errors.Count == environmentErrors) report.environmentCasesVerified++;
                }
                if (report.errors.Count == errorsBefore) report.locationsVerified++;
            }
            foreach (string feature in new[] { "井盖", "花纹", "台阶", "起伏" })
                if (!terrainFeatures.Contains(feature)) report.errors.Add("No physical terrain feature verified for " + feature);
            if (report.locationsVerified != GameCatalog.Locations.Length)
                report.errors.Add("Not all catalogue locations passed: " + report.locationsVerified + "/" + GameCatalog.Locations.Length);
            if (report.environmentCasesVerified != GameCatalog.Locations.Length * 2)
                report.errors.Add("Sunny/rainy environment physics coverage incomplete: " + report.environmentCasesVerified);
        }
        void SurveyTerrain(string location)
        {
            var surfaces = world.Terrain.SurfaceColliders.Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy).ToArray();
            if (!world.Terrain.IsTerrainEnabled || surfaces.Length < 2) Fail(location + "/survey", "Pog terrain is disabled or has no raised physical surfaces.");
            float lowest = float.MaxValue, highest = float.MinValue;
            for (float x = -2.8f; x <= 2.8f; x += .35f) for (float z = -2.4f; z <= 2.8f; z += .35f)
            {
                var point = new Vector3(x, 0, z);
                world.Terrain.SampleSurface(point, out float height, out Vector3 normal);
                if (!Finite(height) || normal.sqrMagnitude < .5f) Fail(location + "/survey", "Surface sampling returned invalid height/normal.");
                lowest = Mathf.Min(lowest, height); highest = Mathf.Max(highest, height);
            }
            if (highest - lowest < .025f) Fail(location + "/survey", "Terrain has no measured height variation greater than 0.025 m.");
            foreach (var collider in surfaces)
            {
                var material = collider.sharedMaterial;
                foreach (string feature in new[] { "井盖", "花纹", "台阶", "起伏" })
                    if (collider.name.Contains(feature)) terrainFeatures.Add(feature);
                report.observations.Add(location + " collider=" + collider.name + " type=" + collider.GetType().Name + " center=" + collider.bounds.center.ToString("F3") +
                    " top=" + collider.bounds.max.y.ToString("F4", Invariant) + " dynamicFriction=" + (material == null ? "default" : material.dynamicFriction.ToString("F3", Invariant)));
            }
            report.observations.Add(location + " sampledHeightRange=" + lowest.ToString("F4", Invariant) + ".." + highest.ToString("F4", Invariant) + " activeSurfaceColliders=" + surfaces.Length);
        }
        IEnumerator VerifyDrops(string location)
        {
            var points = new[] { new Vector3(0, 0, .55f), new Vector3(-.83f, 0, 1.2f), new Vector3(.83f, 0, .25f) };
            for (int i = 0; i < points.Length; i++)
            {
                var position = points[i]; position.y = world.SurfaceHeight(position) + 1.6f;
                var disc = Spawn(position, Quaternion.Euler(47 + i * 13, 19, 34 + i * 27), i == 0 ? "paper" : i == 1 ? "rubber" : "cardboard");
                AssertDiscGeometry(location, disc);
                disc.Body.linearVelocity = new Vector3(i == 0 ? 1.2f : .1f, -35, .15f);
                disc.Body.angularVelocity = new Vector3(21, 28, -18);
                yield return Observe(location, "rotating-drop-" + i, () => new[] { disc.Body }, 3, true);
                if (!disc.Quiet) Fail(location + "/rotating-drop-" + i, "Dropped disc did not settle in 3 simulated seconds.");
                if (i == 0) report.thinDiscsVerified++;
                if (i == 1) report.thickDiscsVerified++;
                if (i == 1) yield return Capture(location + "-disc-on-feature");
                ClearLoose(); yield return null;
            }
            // 从靠左的平坦空地出发；旧位置在树下斜砖的边缘，片边会被测试人为嵌进砖里。
            var skim = Spawn(new Vector3(-3.1f, world.SurfaceHeight(new Vector3(-3.1f, 0, .55f)) + .065f, .55f), Quaternion.Euler(-3, 0, 0));
            skim.Body.linearVelocity = new Vector3(40, 0, 0); skim.Body.angularVelocity = new Vector3(0, 34, 0);
            yield return Observe(location, "high-speed-terrain-traverse", () => new[] { skim.Body }, 3, true);
            ClearLoose(); yield return null;
        }
        IEnumerator VerifyStack(string location)
        {
            int trials = location == "playground-Sunny" ? 3 : 1;
            for (int trial = 1; trial <= trials; trial++)
            {
                // 井盖外环也有实体厚度；堆片放在完整平坦空地，不跨半径 .735 的外环。
                const float stackZ = -.85f;
                float floor = world.SurfaceHeight(new Vector3(0, 0, stackZ));
                var stack = new List<Rigidbody>();
                for (int i = 0; i < 4; i++)
                {
                    var disc = Spawn(new Vector3(i * .018f, floor + .04f + i * .072f, stackZ), Quaternion.identity); stack.Add(disc.Body);
                }
                var hitter = Spawn(new Vector3(-1.7f, floor + .085f, stackZ), Quaternion.Euler(8, 0, 0));
                hitter.Body.linearVelocity = new Vector3(35, .2f, 0); hitter.Body.angularVelocity = new Vector3(0, 12, 7); stack.Add(hitter.Body);
                string scenario = "four-disc-stack-impact" + (trials > 1 ? "-trial-" + trial : "");
                var result = NewCase(location, scenario);
                float start = Time.fixedTime;
                while (Time.fixedTime - start < 4)
                { yield return new WaitForFixedUpdate(); Sample(result, stack, true); }
                result.fourSecondBodies = CaptureBodyStates(stack);
                result.settledAtFourSeconds = StackWithinSettlementLimits(stack);
                RecordStackBodies(result, "four-seconds", result.fourSecondBodies);
                if (!result.settledAtFourSeconds)
                {
                    result.continuedNaturalTail = true;
                    float stableStart = -1;
                    // 生产也在飞行 8 秒后才加阻尼。此处只增加真实观测，不改变片的阻尼、姿态或睡眠。
                    while (Time.fixedTime - start < 8)
                    {
                        yield return new WaitForFixedUpdate(); Sample(result, stack, true);
                        if (StackWithinSettlementLimits(stack))
                        {
                            if (stableStart < 0) stableStart = Time.fixedTime;
                            result.tailStableSeconds = Time.fixedTime - stableStart;
                            if (result.tailStableSeconds >= .35f)
                            { result.naturallySettledAtSeconds = stableStart - start; break; }
                        }
                        else { stableStart = -1; result.tailStableSeconds = 0; }
                    }
                }
                else result.naturallySettledAtSeconds = Time.fixedTime - start;
                result.simulatedSeconds = Time.fixedTime - start;
                result.finalBodies = CaptureBodyStates(stack);
                RecordStackBodies(result, "final", result.finalBodies);
                if (!StackWithinSettlementLimits(stack))
                    Fail(location + "/" + scenario, "Disc stack/impact did not naturally settle after 8 simulated seconds (linear > 0.15 m/s or angular > 0.8 rad/s).");
                FinishCase(result);
                yield return Capture(location + "-stack-impact" + (trials > 1 ? "-trial-" + trial : ""));
                ClearLoose(); yield return null;
            }
        }
        static bool StackWithinSettlementLimits(IList<Rigidbody> bodies)
        { return bodies.All(body => body.linearVelocity.magnitude <= .15f && body.angularVelocity.magnitude <= .8f); }
        static List<BodyState> CaptureBodyStates(IList<Rigidbody> bodies)
        {
            return bodies.Select(body =>
            {
                var disc = body.GetComponent<DiscBody>();
                return new BodyState { body = body.name, position = body.position, rotation = body.rotation,
                    velocity = body.linearVelocity, angularVelocity = body.angularVelocity,
                    linearSpeed = body.linearVelocity.magnitude, angularSpeed = body.angularVelocity.magnitude,
                    upDot = Vector3.Dot(body.rotation * Vector3.up, Vector3.up), sleeping = body.IsSleeping(),
                    boundaryRecoveries = disc == null ? 0 : disc.BoundaryRecoveries,
                    penetrationRecoveries = disc == null ? 0 : disc.PenetrationRecoveries };
            }).ToList();
        }
        void RecordStackBodies(CaseReport result, string checkpoint, IList<BodyState> bodies)
        {
            foreach (var body in bodies)
                report.observations.Add(result.location + "/" + result.scenario + " " + checkpoint + " body=" + body.body +
                    " position=" + body.position.ToString("F6", Invariant) + " upDot=" + F(body.upDot) +
                    " linear=" + F(body.linearSpeed) + " angular=" + F(body.angularSpeed) + " sleeping=" + body.sleeping +
                    " recovery=" + body.boundaryRecoveries + "/" + body.penetrationRecoveries);
        }
        IEnumerator VerifyBoundaries(string location)
        {
            Vector3 center = new Vector3(0, 0, .2f);
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
                new Vector3(1, 0, 1).normalized, new Vector3(-1, 0, 1).normalized, new Vector3(1, 0, -1).normalized, new Vector3(-1, 0, -1).normalized };
            for (int i = 0; i < directions.Length; i++)
            {
                var disc = Spawn(center + Vector3.up * (world.SurfaceHeight(center) + .065f), Quaternion.identity);
                disc.Body.linearVelocity = directions[i] * 40;
                yield return Observe(location, "ground-boundary-" + i, () => new[] { disc.Body }, 1.5f, true);
                report.observations.Add(location + " ground-boundary-" + i + " physicalRecoveryCounts=" + disc.BoundaryRecoveries + "/" + disc.PenetrationRecoveries);
                ClearLoose(); yield return null;
            }
            var airborne = Spawn(center + Vector3.up * 1.1f, Quaternion.Euler(20, 13, 60));
            airborne.Body.linearVelocity = Vector3.right * 45; airborne.Body.angularVelocity = new Vector3(0, 28, 11);
            yield return Observe(location, "airborne-boundary-recovery", () => new[] { airborne.Body }, 1.5f, false);
            if (airborne.BoundaryRecoveries < 1) Fail(location + "/airborne-boundary-recovery", "Airborne disc never exercised the explicit high-flight boundary recovery.");
            report.observations.Add(location + " airborne-boundary recovery=" + airborne.BoundaryRecoveries + "; this is an explicit safeguard check, not proof of physical wall contact.");
            ClearLoose(); yield return null;
        }
        IEnumerator VerifyFriction(string location)
        {
            var points = new[] { new Vector3(0, 0, -.9f), new Vector3(-.83f, 0, 1.2f), new Vector3(.83f, 0, .25f) };
            for (int i = 0; i < points.Length; i++)
            {
                var position = points[i]; position.y = world.SurfaceHeight(position) + .034f;
                var disc = Spawn(position, Quaternion.identity);
                disc.Body.constraints = RigidbodyConstraints.FreezeRotation;
                disc.Body.linearDamping = 0; disc.Body.angularDamping = 0;
                yield return Observe(location, "friction-seat-" + i, () => new[] { disc.Body }, .25f, true);
                Vector3 start = disc.Body.position;
                disc.Body.linearVelocity = Vector3.forward; disc.Body.WakeUp();
                yield return Observe(location, "friction-slide-" + i, () => new[] { disc.Body }, .1f, true);
                float ending = new Vector2(disc.Body.linearVelocity.x, disc.Body.linearVelocity.z).magnitude;
                float movement = Vector3.Distance(start, disc.Body.position);
                if (ending > .99f || movement < .005f) Fail(location + "/friction-slide-" + i, "Actual zero-damping material slide did not both move and slow down: distance=" + movement + " endSpeed=" + ending);
                report.observations.Add(location + " physical-friction point=" + points[i] + " initialSpeed=1 finalSpeed=" + ending.ToString("F4", Invariant) +
                    " distance=" + movement.ToString("F4", Invariant) + " duration=0.1s; rotation is locked and body damping=0 to isolate contact friction.");
                ClearLoose(); yield return null;
            }
        }
        IEnumerator VerifyPogShot(string location, RuleKind rule, ShotKind kind)
        {
            string label = "pog-" + rule + "-" + kind;
            pog.Begin(GameCatalog.Cards[0], null, rule, true, 97, PracticeLayout.Classic);
            yield return WaitFor(() => pog.Phase == MatchPhase.PlayerAim, 5, location + "/" + label + " initial aim");
            if (pog.Phase != MatchPhase.PlayerAim) { pog.StopMatch(); yield return null; yield break; }
            ValidateRing(pog, rule == RuleKind.RingOut ? "粉笔比赛圈" : null, location + "/" + label);
            int strikerTargetContacts = 0;
            foreach (var disc in pog.Targets)
                disc.Hit += (hit, speed) =>
                {
                    var other = DiscBody.FromCollider(hit.LastHitCollider);
                    if (other != null && !other.IsTarget) strikerTargetContacts++;
                };
            // 擦地命中验收选地面最低且靠左的目标。按 z 的微小落稳差排序会误选校门抬高水泥砖上的片，
            // 真实路沿挡住擦地片是地形效果，不能作为“擦地碰撞失效”的证据。
            var target = kind == ShotKind.Skim
                ? pog.Targets.OrderBy(t => world.SurfaceHeight(t.Body.position)).ThenBy(t => t.Body.position.x).ThenBy(t => t.Body.position.z).First()
                : pog.Targets.OrderBy(t => t.Body.position.z).First();
            report.observations.Add(location + " directed shot target=" + target.ArtIndex + " position=" + target.Body.position.ToString("F4") + " surface=" + world.SurfaceHeight(target.Body.position).ToString("F4", Invariant));
            Vector3 delta = target.Body.position - new Vector3(0, 0, -2.25f); delta.y = 0;
            float power = kind == ShotKind.Skim ? .92f : Mathf.Clamp01((delta.magnitude - 1.25f) / 3.3f);
            pog.Launch(delta, power, .65f, kind);
            if (pog.Phase != MatchPhase.Flying || pog.ShotsUsed != 1) Fail(location + "/" + label, "Launch did not enter Flying or consume one shot.");
            yield return ObserveUntilSettled(location, label, pog, () => pog.Phase);
            if (pog.Phase == MatchPhase.Flying || string.IsNullOrEmpty(pog.CaptureBoard())) Fail(location + "/" + label, "Directed real shot never reached a stable capturable board.");
            else report.pogShots++;
            if (kind == ShotKind.Skim && strikerTargetContacts == 0)
                Fail(location + "/" + label, "Directed high-power skim never physically contacted a target disc.");
            report.observations.Add(location + " real-shot rule=" + rule + " kind=" + kind + " score=" + pog.PlayerScore + " phase=" + pog.Phase + " shots=" + pog.ShotsUsed + " actualStrikerTargetContacts=" + strikerTargetContacts);
            yield return Capture(location + "-" + label); pog.StopMatch(); Time.timeScale = 4; yield return null;
        }
        IEnumerator VerifyScoreFixture(string location, RuleKind rule)
        {
            string label = "score-fixture-" + rule;
            pog.Begin(GameCatalog.Cards[0], null, rule, true, 97);
            yield return WaitFor(() => pog.Phase == MatchPhase.PlayerAim, 5, location + "/" + label);
            if (pog.Phase != MatchPhase.PlayerAim) { pog.StopMatch(); yield return null; yield break; }
            var targets = pog.Targets.ToArray();
            for (int i = 0; i < targets.Length; i++)
            {
                // 各目标沿发射带分开摆放，保证得分 fixture 没有跨台阶或井盖的人为初始穿透。
                var position = new Vector3(-1.45f + i * .72f, 0, -1.8f); Quaternion rotation = Quaternion.identity;
                if (rule == RuleKind.RingOut && i >= 2) position = new Vector3((i - 3) * .72f, 0, -.7f);
                if (i == 0)
                {
                    if (rule == RuleKind.Flip) rotation = Quaternion.Euler(180, 0, 0);
                    else position = new Vector3(1.85f + targets[i].Radius + .06f, 0, .55f);
                }
                if (i == 1 && rule == RuleKind.RingOut) position = new Vector3(-(1.85f + .1f), 0, .55f);
                position.y = FixtureRestHeight(targets[i], position, rotation);
                Place(targets[i].Body, position, rotation); targets[i].Body.constraints = RigidbodyConstraints.FreezeAll;
            }
            Physics.SyncTransforms(); pog.Launch(Vector3.back, 0, 0, ShotKind.Skim);
            yield return ObserveUntilSettled(location, label, pog, () => pog.Phase);
            if (pog.Phase == MatchPhase.Flying || pog.PlayerScore != 1 || !targets[0].Claimed || targets[1].Claimed)
                Fail(location + "/" + label, "Controlled rule fixture should claim exactly the face-down/fully outside disc, not the partial ring-out disc. score=" + pog.PlayerScore);
            else report.scoringFixtures++;
            report.observations.Add(location + " " + label + " score=" + pog.PlayerScore + "; scoring pose was deliberately initialized and frozen. This checks settled rule semantics; physical shots are reported separately.");
            pog.StopMatch(); Time.timeScale = 4; yield return null;
        }
        IEnumerator VerifyMarbles(string location)
        {
            marbles.Begin(97, MarbleLayout.Classic);
            yield return WaitFor(() => marbles.Phase == MatchPhase.PlayerAim, 5, location + "/marble aim");
            if (world.Terrain.IsTerrainEnabled) Fail(location + "/marble", "Pog raised terrain remained enabled for the existing flat marble rules.");
            ValidateRing(marbles, "玻璃球比赛粉笔圈", location + "/marble");
            if (marbles.Phase == MatchPhase.PlayerAim) marbles.Launch(Vector3.forward, .9f);
            if (marbles.Phase != MatchPhase.Flying || marbles.ShotsRemaining != 5) Fail(location + "/marble", "Marble launch did not enter Flying or consume exactly one shot.");
            yield return ObserveUntilSettled(location, "marble-real-shot", marbles, () => marbles.Phase, false);
            if (marbles.Phase == MatchPhase.Flying || string.IsNullOrEmpty(marbles.CaptureBoard())) Fail(location + "/marble", "Marble real shot did not settle to a capturable board.");
            else report.marbleShots++;
            report.observations.Add(location + " marble real-shot score=" + marbles.Score + " phase=" + marbles.Phase + " remaining=" + marbles.ShotsRemaining);
            yield return Capture(location + "-marbles");
            marbles.StopMatch(); world.SetPogTerrainEnabled(true); Time.timeScale = 4; yield return null;
            if (!world.Terrain.IsTerrainEnabled) Fail(location + "/marble", "Pog terrain was not restored after marble regression.");
        }
        void ValidateRing(Component component, string ringName, string label)
        {
            if (ringName == null) return;
            var ring = component.GetComponentsInChildren<LineRenderer>().FirstOrDefault(l => l.name == ringName);
            if (ring == null || ring.positionCount < 32) { Fail(label, "A complete scoring ring was not drawn."); return; }
            float minimum = float.MaxValue, maximum = float.MinValue;
            for (int i = 0; i < ring.positionCount; i++)
            {
                Vector3 position = ring.GetPosition(i); if (!ring.useWorldSpace) position = ring.transform.TransformPoint(position);
                float clearance = position.y - world.SurfaceHeight(position);
                minimum = Mathf.Min(minimum, clearance); maximum = Mathf.Max(maximum, clearance);
                if (clearance < .008f || clearance > .065f) Fail(label, "Scoring-ring vertex does not follow the terrain: index=" + i + " clearance=" + clearance);
            }
            report.observations.Add(label + " ring measured clearances=" + minimum.ToString("F4", Invariant) + ".." + maximum.ToString("F4", Invariant) + " vertices=" + ring.positionCount);
        }
        IEnumerator ObserveUntilSettled(string location, string scenario, Component host, Func<MatchPhase> phase, bool testBounds = true)
        {
            var result = NewCase(location, scenario);
            float start = Time.fixedTime, deadline = Time.realtimeSinceStartup + 22;
            while (phase() == MatchPhase.Flying && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                Sample(result, host.GetComponentsInChildren<Rigidbody>().Where(b => b.gameObject.activeInHierarchy && !b.isKinematic).ToArray(), testBounds);
            }
            result.simulatedSeconds = Time.fixedTime - start;
            if (phase() == MatchPhase.Flying) Fail(location + "/" + scenario, "Real match did not settle within 22 wall seconds.");
            FinishCase(result);
        }
        IEnumerator Observe(string location, string scenario, Func<IList<Rigidbody>> bodies, float duration, bool testBounds)
        {
            var result = NewCase(location, scenario); float start = Time.fixedTime;
            while (Time.fixedTime - start < duration) { yield return new WaitForFixedUpdate(); Sample(result, bodies(), testBounds); }
            result.simulatedSeconds = Time.fixedTime - start; FinishCase(result);
        }
        CaseReport NewCase(string location, string scenario)
        {
            var result = new CaseReport { location = location, scenario = scenario }; report.cases.Add(result); return result;
        }
        void FinishCase(CaseReport result)
        {
            string prefix = result.location + "/" + result.scenario;
            result.passed = !emittedFailures.Any(value => value.StartsWith(prefix + "|", StringComparison.Ordinal));
            result.usedSafetyRecovery = result.boundaryRecoveries > 0 || result.penetrationRecoveries > 0;
            if (result.samples == 0) { Fail(prefix, "No actual physics steps were observed."); result.passed = false; }
            frames.Flush();
            report.observations.Add(prefix + " fixedSamples=" + result.samples + " duration=" + result.simulatedSeconds.ToString("F3", Invariant) +
                " maxGroundPen=" + result.maxGroundPenetration.ToString("F5", Invariant) + " maxDiscPen=" + result.maxBodyPenetration.ToString("F5", Invariant) +
                " recovery=" + result.boundaryRecoveries + "/" + result.penetrationRecoveries + " passed=" + result.passed);
        }
        void Sample(CaseReport result, IList<Rigidbody> bodies, bool testBounds)
        {
            report.physicsFrames++;
            // 只汇总当前物理步的全体片；不能被最后一个静止的 hitter 覆盖为零。
            result.endSpeed = result.endAngularSpeed = 0;
            var surfaces = world.Terrain.SurfaceColliders.Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy).ToArray();
            string label = result.location + "/" + result.scenario;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i]; if (body == null || !body.gameObject.activeInHierarchy || body.isKinematic) continue;
                var colliders = body.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && c.attachedRigidbody == body).ToArray();
                float groundDepth = 0, bodyDepth = 0, excess = 0;
                string groundColliderName = "";
                foreach (var collider in colliders)
                {
                    ColliderPose(collider, body, out Vector3 position, out Quaternion rotation);
                    foreach (var surface in surfaces)
                        if (surface.bounds.SqrDistance(body.position) < 1f && Physics.ComputePenetration(collider, position, rotation, surface, surface.transform.position, surface.transform.rotation, out _, out float depth))
                        {
                            if (depth > groundDepth) { groundDepth = depth; groundColliderName = surface.name; }
                            result.contacts++;
                        }
                    for (int j = i + 1; j < bodies.Count; j++)
                    {
                        var other = bodies[j];
                        if (other == null || !other.gameObject.activeInHierarchy || Vector3.SqrMagnitude(body.position - other.position) > 1.5f) continue;
                        foreach (var otherCollider in other.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && c.attachedRigidbody == other))
                        {
                            ColliderPose(otherCollider, other, out Vector3 otherPosition, out Quaternion otherRotation);
                            if (Physics.ComputePenetration(collider, position, rotation, otherCollider, otherPosition, otherRotation, out _, out float depth)) bodyDepth = Mathf.Max(bodyDepth, depth);
                        }
                    }
                    if (testBounds && collider is BoxCollider box)
                    {
                        Vector3 half = Vector3.Scale(box.size * .5f, box.transform.lossyScale);
                        Vector3 boxCenter = position + rotation * Vector3.Scale(box.center, box.transform.lossyScale);
                        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 vertex = boxCenter + rotation * new Vector3(half.x * x, half.y * y, half.z * z);
                            excess = Mathf.Max(excess, vertex.x - ArenaTerrain.MaxX, ArenaTerrain.MinX - vertex.x, vertex.z - ArenaTerrain.MaxZ, ArenaTerrain.MinZ - vertex.z);
                        }
                    }
                }
                float clearance = body.position.y - world.SurfaceHeight(body.position);
                var disc = body.GetComponent<DiscBody>();
                int boundaryRecoveries = disc == null ? 0 : disc.BoundaryRecoveries, penetrationRecoveries = disc == null ? 0 : disc.PenetrationRecoveries;
                result.samples++; report.bodySamples++;
                if (groundDepth > result.maxGroundPenetration) { result.maxGroundPenetration = groundDepth; result.deepestGroundCollider = groundColliderName; }
                result.maxBodyPenetration = Mathf.Max(result.maxBodyPenetration, bodyDepth);
                result.maxBoundaryExcess = Mathf.Max(result.maxBoundaryExcess, excess); result.minCenterClearance = Mathf.Min(result.minCenterClearance, clearance);
                result.boundaryRecoveries = Mathf.Max(result.boundaryRecoveries, boundaryRecoveries); result.penetrationRecoveries = Mathf.Max(result.penetrationRecoveries, penetrationRecoveries);
                result.endSpeed = Mathf.Max(result.endSpeed, body.linearVelocity.magnitude);
                result.endAngularSpeed = Mathf.Max(result.endAngularSpeed, body.angularVelocity.magnitude);
                if (!Finite(body.position.x) || !Finite(body.position.y) || !Finite(body.position.z)) Fail(label, "Body position is not finite.");
                float allowedPenetration = disc == null ? PenetrationLimit : Mathf.Min(PenetrationLimit, disc.Height * .4f);
                if (groundDepth > allowedPenetration) Fail(label, "Ground penetration exceeded " + allowedPenetration.ToString("F5", Invariant) + " m: " + groundDepth.ToString("F5", Invariant) + " body=" + body.name);
                if (bodyDepth > allowedPenetration) Fail(label, "Disc/body penetration exceeded " + allowedPenetration.ToString("F5", Invariant) + " m: " + bodyDepth.ToString("F5", Invariant) + " body=" + body.name);
                if (clearance < -BelowSurfaceLimit) Fail(label, "Body center passed below terrain by more than 0.006 m: " + clearance.ToString("F5", Invariant));
                if (testBounds && excess > BoundaryLimit) Fail(label, "Actual collider vertices escaped arena by more than 0.012 m: " + excess.ToString("F5", Invariant));
                Vector3 p = body.position, v = body.linearVelocity, w = body.angularVelocity;
                Quaternion q = body.rotation;
                frames.WriteLine(string.Join(",", new[] { result.location, result.scenario, F(Time.fixedTime), body.name.Replace(',', '_'), F(p.x), F(p.y), F(p.z), F(q.x), F(q.y), F(q.z), F(q.w), F(Vector3.Dot(q * Vector3.up, Vector3.up)), F(v.x), F(v.y), F(v.z), F(w.x), F(w.y), F(w.z), F(groundDepth), groundColliderName.Replace(',', '_'), F(bodyDepth), F(clearance), F(excess), boundaryRecoveries.ToString(), penetrationRecoveries.ToString() }));
            }
        }
        static void ColliderPose(Collider collider, Rigidbody body, out Vector3 position, out Quaternion rotation)
        {
            // 使用刚体物理姿态，避免插值 Transform 落后造成虚假的穿模报告。
            Vector3 local = body.transform.InverseTransformPoint(collider.transform.position);
            position = body.position + body.rotation * Vector3.Scale(local, body.transform.lossyScale);
            rotation = body.rotation * Quaternion.Inverse(body.transform.rotation) * collider.transform.rotation;
        }
        DiscBody Spawn(Vector3 position, Quaternion rotation, string prototypeId = "cardboard")
        {
            var prototype = GameCatalog.GetPrototype(prototypeId);
            var disc = DiscBody.Spawn(looseBodies, 1, position, prototype.massGrams / 4.8f, prototype.thicknessMeters / .0027f,
                prototype.grip / .64f, Mathf.Clamp01(prototype.flipEase - .5f)); Place(disc.Body, position, rotation);
            disc.name = "QA圆片_" + (++spawnSerial);
            disc.Arena = world.Terrain;
            disc.ConstrainToArena(world.Terrain); Physics.SyncTransforms(); return disc;
        }
        void AssertDiscGeometry(string location, DiscBody disc)
        {
            var mesh = disc.GetComponent<MeshFilter>().sharedMesh;
            if (Mathf.Abs(mesh.bounds.size.y - disc.Height) > .00001f || disc.PhysicsColliders.Count != 8)
                Fail(location + "/thin-geometry", "Visible height or primitive collider count does not match the physical disc.");
            foreach (var shape in disc.PhysicsColliders)
                if (!(shape is BoxCollider box) || Mathf.Abs(box.size.y - disc.Height) > .00001f || box.contactOffset >= disc.Height * .2f)
                    Fail(location + "/thin-geometry", "Collider height/contact shell is inconsistent with thin geometry.");
            float expected = disc.Body.mass * (3f * disc.Radius * disc.Radius + disc.Height * disc.Height) / 12f;
            if (Mathf.Abs(disc.Body.inertiaTensor.x - expected) > .00001f || Mathf.Abs(disc.Body.inertiaTensor.z - expected) > .00001f)
                Fail(location + "/thin-geometry", "Compound box shapes changed the intended circular inertia.");
            report.observations.Add(location + " thinGeometry height=" + disc.Height.ToString("F5", Invariant) + " contactOffset=" + disc.ContactTolerance.ToString("F5", Invariant));
        }
        static void Place(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            body.interpolation = RigidbodyInterpolation.None; body.transform.SetPositionAndRotation(position, rotation);
            body.position = position; body.rotation = rotation; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.WakeUp();
        }
        float FixtureRestHeight(DiscBody disc, Vector3 position, Quaternion rotation)
        {
            // 冻结的规则 fixture 不允许靠 PhysX 之后排穿；先保证整个实体片边避开所有砖/台阶。
            float height = world.SurfaceHeight(position) + disc.HalfHeight + .004f;
            foreach (var collider in disc.PhysicsColliders)
            {
                if (!(collider is BoxCollider box)) continue;
                Quaternion boxRotation = rotation * box.transform.localRotation;
                Vector3 half = box.size * .5f;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 offset = boxRotation * (box.center + new Vector3(x * half.x, y * half.y, z * half.z));
                    height = Mathf.Max(height, world.SurfaceHeight(position + offset) - offset.y + .004f);
                }
            }
            return height;
        }
        void ClearLoose()
        {
            if (looseBodies == null) return;
            for (int i = looseBodies.childCount - 1; i >= 0; i--) { var child = looseBodies.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
        }
        IEnumerator WaitFor(Func<bool> ready, float timeout, string label)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!ready()) Fail(label, "Timed out after " + timeout + " wall seconds.");
        }
        IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame();
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32); target.Create();
            var previousTarget = world.MainCamera.targetTexture; var previousActive = RenderTexture.active;
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                world.MainCamera.targetTexture = target; world.MainCamera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, filename + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                world.MainCamera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                target.Release(); Destroy(target); Destroy(texture);
            }
        }
        void Update()
        {
            // 仅缩短墙钟时间；固定步长、真实物理求解和每步采样仍保持不变。
            if (!finished && Time.timeScale > 0) Time.timeScale = 4;
            if (!finished && Time.realtimeSinceStartup > watchdog)
            { report.errors.Add("Arena test exceeded the 480-second watchdog; incomplete physics coverage."); Complete(false); }
        }
        void Fail(string label, string reason)
        {
            string category = reason.Split(':')[0];
            if (emittedFailures.Add(label + "|" + category)) report.errors.Add(label + ": " + reason);
        }
        void OnLog(string value, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add("Unity: " + value + "\n" + stack);
        }
        static void SnapshotSaveFiles(Dictionary<string, string> destination)
        {
            string directory = Path.Combine(Application.persistentDataPath, "SummerAfterSchool"); if (!Directory.Exists(directory)) return;
            using (var digest = SHA256.Create()) foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                destination[path] = Convert.ToBase64String(digest.ComputeHash(File.ReadAllBytes(path)));
        }
        void Complete(bool completed)
        {
            if (finished) return; finished = true; report.completed = completed;
            var after = new Dictionary<string, string>(); SnapshotSaveFiles(after);
            report.saveFilesUnchanged = saveBefore.Count == after.Count && saveBefore.All(pair => after.TryGetValue(pair.Key, out string value) && value == pair.Value);
            if (!report.saveFilesUnchanged) report.errors.Add("Real persistent save files changed during standalone Arena test.");
            report.passed = completed && report.errors.Count == 0;
            foreach (var result in report.cases)
                result.passed = result.samples > 0 && !emittedFailures.Any(value => value.StartsWith(result.location + "/" + result.scenario + "|", StringComparison.Ordinal));
            frames?.Flush(); frames?.Dispose(); frames = null;
            File.WriteAllText(Path.Combine(output, "arena-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("SUMMER_ARENA_COMPLETE completed=" + report.completed + " passed=" + report.passed + " errors=" + report.errors.Count +
                " fixedFrames=" + report.physicsFrames + " pogShots=" + report.pogShots + " marbleShots=" + report.marbleShots);
            Application.Quit(report.passed ? 0 : 1);
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static string F(float value) { return value.ToString("F6", Invariant); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; frames?.Dispose(); }
    }
}
